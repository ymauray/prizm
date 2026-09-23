// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;
using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// The ULA: port 0xFE (keyboard, border and speaker) and the video output.
/// </summary>
/// <remarks>
/// The picture follows the beam. Border changes are stamped with the CPU's T-state count, and
/// each border pixel takes the colour in effect when the beam drew it (the stripes of tape
/// loading). The screen area is drawn lazily: just before the CPU writes to the screen memory,
/// everything the beam has already drawn is rendered from the memory as it was, so changes made
/// while the beam is on screen appear only below it. The rest is drawn at the end of the frame.
/// A group of 8 pixels shows the writes made up to the T-state of its first pixel.
/// </remarks>
public sealed class Ula : IIo, IScreenWriteObserver
{
    public const int BorderLeft = 32;
    public const int BorderTop = 32;
    public const int FrameWidth = ScreenLayout.Width + (2 * BorderLeft);
    public const int FrameHeight = ScreenLayout.Height + (2 * BorderTop);

    /// <summary>T-state at which the beam draws the first pixel of the screen area (48K).</summary>
    public const int FirstPixelTState = 14336;

    /// <summary>T-states per scan line; the beam draws 2 pixels per T-state.</summary>
    public const int LineTStates = 224;

    /// <summary>T-states between the CPU's call to In and the moment the Z80 latches the data.</summary>
    private const int IoDataLatchDelay = 3;

    /// <summary>FLASH attributes swap ink and paper every 16 frames.</summary>
    private const int FlashPeriod = 16;

    /// <summary>Changes kept per frame; tape loading makes about 80, border effects a few hundred.</summary>
    private const int MaxBorderChanges = 4096;

    private readonly uint[] _frameBuffer = new uint[FrameWidth * FrameHeight];
    private readonly long[] _borderChangeTimes = new long[MaxBorderChanges];
    private readonly byte[] _borderChangeColors = new byte[MaxBorderChanges];
    private int _borderChangeCount;
    private int _frameStartBorder;
    private int _border;
    private int _frameCount;

    /// <summary>Bit 4 of the last write to port 0xFE: the speaker, and the EAR input when no tape plays.</summary>
    private bool _speakerHigh;

    // Rendering position in beam order: the next pixel to draw, the next border change to
    // apply, and the border colour in effect there.
    private int _row;
    private int _column;
    private int _nextBorderChange;
    private uint _borderColor = Palette.Colors[0];
    private bool _flashInverted;

    private Z80Cpu? _cpu;
    private Memory48K? _memory;

    /// <summary>The key matrix read through port 0xFE.</summary>
    public Keyboard Keyboard { get; } = new();

    /// <summary>The speaker, driven by bit 4 of port 0xFE, which also plays the tape signal.</summary>
    public Beeper Beeper { get; } = new();

    /// <summary>The tape deck, whose signal is read on bit 6 of port 0xFE (EAR).</summary>
    public TapePlayer Tape { get; } = new();

    /// <summary>
    /// Border colour, 0-7, as last written to port 0xFE. Setting it directly (snapshot loading)
    /// colours the whole border of the current frame.
    /// </summary>
    public int Border
    {
        get => _border;
        set
        {
            _border = value & 0x07;
            _frameStartBorder = _border;
            _borderChangeCount = 0;
            _nextBorderChange = 0;
            _borderColor = Palette.Colors[_border];
        }
    }

    /// <summary>FrameWidth x FrameHeight pixels, row by row, in <see cref="Palette"/> format.</summary>
    public ReadOnlySpan<uint> FrameBuffer => _frameBuffer;

    /// <summary>
    /// Any even port selects the ULA: bits 0-4 are the keyboard half-rows selected by the high
    /// byte of the port address, bit 6 is the EAR input, bits 5 and 7 read 1. Odd ports have
    /// nothing behind them: they read the floating bus.
    /// </summary>
    /// <remarks>
    /// EAR is the tape signal while it plays. Otherwise it follows bit 4 of the last write to
    /// port 0xFE, as on an issue 3 board (the most common): the ROM leaves that bit at 0, so
    /// reading port 0xFE with no key down gives 0xBF, the value z80test expects.
    /// </remarks>
    public byte In(ushort port)
    {
        if ((port & 1) != 0)
        {
            // The CPU calls In 1 T-state into its 4 T-state I/O cycle; the Z80 latches the data
            // at the end of it, 3 T-states later.
            return FloatingBus((_cpu?.TStates ?? 0) + IoDataLatchDelay);
        }

        Tape.AdvanceTo(_cpu?.TStates ?? 0, Beeper);
        var ear = (Tape.IsPlaying ? Tape.Level : _speakerHigh) ? 0x40 : 0;
        return (byte)(0xA0 | ear | Keyboard.Read((byte)(port >> 8)));
    }

    /// <summary>
    /// Gives the ULA the CPU whose T-state count stamps border and speaker changes, and the
    /// memory it reads the screen from.
    /// </summary>
    public void Connect(Z80Cpu cpu, Memory48K memory)
    {
        _cpu = cpu;
        _memory = memory;
    }

    /// <summary>
    /// What a read from a port nobody answers returns on the 48K: the byte the ULA is reading at
    /// <paramref name="tStates"/>. In each group of 8 T-states of a screen line, counted from the
    /// line's first pixel (T-state 14336 for the first line), it reads a pixel byte at +3, its
    /// attribute at +4, the next pixel byte at +5 and its attribute at +6; the rest of the time,
    /// and outside the screen, the bus reads 0xFF.
    /// </summary>
    /// <remarks>
    /// Layout as in FUSE (spectrum_unattached_port). Where the Z80 samples it within an IN is set
    /// by Richard Butler's timing tests 35 to 37, which only pass with the end of the I/O cycle.
    /// </remarks>
    public byte FloatingBus(long tStates)
    {
        var sinceScreen = tStates - FirstPixelTState;
        if (_memory is null || sinceScreen < 0)
        {
            return 0xFF;
        }

        var y = (int)(sinceScreen / LineTStates);
        var position = (int)(sinceScreen % LineTStates);
        if (y >= ScreenLayout.Height || position >= 128)
        {
            return 0xFF;
        }

        var x = (position / 8) * 16;
        return (position % 8) switch
        {
            3 => _memory.Read(ScreenLayout.PixelAddress(x, y)),
            4 => _memory.Read(ScreenLayout.AttributeAddress(x, y)),
            5 => _memory.Read(ScreenLayout.PixelAddress(x + 8, y)),
            6 => _memory.Read(ScreenLayout.AttributeAddress(x + 8, y)),
            _ => 0xFF,
        };
    }

    /// <summary>Contended I/O points wait like contended memory (see <see cref="Contention48K"/>).</summary>
    public int ContentionDelay(ushort port, long tStates) => Contention48K.Delay(tStates);

    /// <summary>
    /// Draws what the beam has shown so far, before the CPU changes the screen memory. A pixel
    /// drawn at the very T-state of the write shows the new value: the real ULA reads its bytes a
    /// little before it displays them.
    /// </summary>
    void IScreenWriteObserver.BeforeScreenWrite(ReadOnlySpan<byte> memory) => RenderUpTo((_cpu?.TStates ?? 0) - 1, memory);

    /// <summary>
    /// Bits 0-2 set the border, bit 4 the speaker. Bit 3 (MIC) also reaches the speaker on real
    /// machines, but only faintly; it is ignored here.
    /// </summary>
    public void Out(ushort port, byte value)
    {
        if ((port & 1) != 0)
        {
            return;
        }

        var time = _cpu?.TStates ?? 0;
        var color = value & 0x07;

        // The tape's edges up to now must reach the beeper before this speaker change.
        Tape.AdvanceTo(time, Beeper);

        if (color != _border)
        {
            // Past the limit, later changes of this frame are drawn as they were not there.
            if (_borderChangeCount < MaxBorderChanges)
            {
                _borderChangeTimes[_borderChangeCount] = time;
                _borderChangeColors[_borderChangeCount] = (byte)color;
                _borderChangeCount++;
            }

            _border = color;
        }

        _speakerHigh = (value & 0x10) != 0;
        Beeper.SetLevel(time, _speakerHigh);
    }

    /// <summary>
    /// Ends the sound of the frame: plays the tape up to <paramref name="now"/> (the CPU may have
    /// run past the end of the frame), then closes the beeper's frame and makes times relative
    /// to the next one.
    /// </summary>
    public void EndFrameSound(long now, int frameTStates)
    {
        Tape.AdvanceTo(now, Beeper);
        Beeper.EndFrame(frameTStates);
        Tape.EndFrame(frameTStates);
    }

    /// <summary>Finishes drawing the frame, then starts the next one: FLASH counter, border changes.</summary>
    public void EndFrame(ReadOnlySpan<byte> memory)
    {
        RenderUpTo(long.MaxValue, memory);

        _frameCount++;
        _frameStartBorder = _border;
        _borderChangeCount = 0;
        _nextBorderChange = 0;
        _borderColor = Palette.Colors[_border];
        _flashInverted = (_frameCount / FlashPeriod % 2) == 1;
        _row = 0;
        _column = 0;
    }

    /// <summary>
    /// Draws, in beam order, every pixel the beam reaches at or before <paramref name="time"/>:
    /// border pixels one by one, screen pixels by groups of 8 (4 T-states).
    /// </summary>
    private void RenderUpTo(long time, ReadOnlySpan<byte> memory)
    {
        for (; _row < FrameHeight; _row++, _column = 0)
        {
            var y = _row - BorderTop;
            var lineStart = FirstPixelTState + ((long)y * LineTStates);
            var screenLine = y is >= 0 and < ScreenLayout.Height;
            var line = _frameBuffer.AsSpan(_row * FrameWidth, FrameWidth);

            while (_column < FrameWidth)
            {
                var pixelTime = lineStart + ((_column - BorderLeft) >> 1);
                if (pixelTime > time)
                {
                    return;
                }

                var x = _column - BorderLeft;
                if (screenLine && x is >= 0 and < ScreenLayout.Width)
                {
                    DrawCell(line.Slice(_column, 8), x, y, memory);
                    _column += 8;
                }
                else
                {
                    while (_nextBorderChange < _borderChangeCount && _borderChangeTimes[_nextBorderChange] <= pixelTime)
                    {
                        _borderColor = Palette.Colors[_borderChangeColors[_nextBorderChange]];
                        _nextBorderChange++;
                    }

                    line[_column] = _borderColor;
                    _column++;
                }
            }
        }
    }

    private void DrawCell(Span<uint> cell, int x, int y, ReadOnlySpan<byte> memory)
    {
        var pixels = memory[ScreenLayout.PixelAddress(x, y)];
        var attribute = memory[ScreenLayout.AttributeAddress(x, y)];
        var bright = (attribute & 0x40) != 0;
        var ink = Palette.Colors[Palette.Index(attribute & 0x07, bright)];
        var paper = Palette.Colors[Palette.Index((attribute >> 3) & 0x07, bright)];

        if ((attribute & 0x80) != 0 && _flashInverted)
        {
            (ink, paper) = (paper, ink);
        }

        for (var bit = 0; bit < 8; bit++)
        {
            cell[bit] = (pixels & (0x80 >> bit)) != 0 ? ink : paper;
        }
    }
}
