// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;
using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// The ULA: port 0xFE (keyboard, border and speaker) and the video output.
/// </summary>
/// <remarks>
/// The frame is rendered at its end. The screen area is drawn from the memory as it is then; the
/// border follows the beam: each border change is stamped with the CPU's T-state count, and each
/// border pixel takes the colour in effect when the beam drew it, which shows the stripes of tape
/// loading. Drawing the screen area line by line too comes with contention (milestone 7).
/// </remarks>
public sealed class Ula : IIo
{
    public const int BorderLeft = 32;
    public const int BorderTop = 32;
    public const int FrameWidth = ScreenLayout.Width + (2 * BorderLeft);
    public const int FrameHeight = ScreenLayout.Height + (2 * BorderTop);

    /// <summary>T-state at which the beam draws the first pixel of the screen area (48K).</summary>
    public const int FirstPixelTState = 14336;

    /// <summary>T-states per scan line; the beam draws 2 pixels per T-state.</summary>
    public const int LineTStates = 224;

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

    private Z80Cpu? _cpu;

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
        }
    }

    /// <summary>FrameWidth x FrameHeight pixels, row by row, in <see cref="Palette"/> format.</summary>
    public ReadOnlySpan<uint> FrameBuffer => _frameBuffer;

    /// <summary>
    /// Any even port selects the ULA: bits 0-4 are the keyboard half-rows selected by the high
    /// byte of the port address, bit 6 is the EAR input (the tape signal while it plays, 1
    /// otherwise), bits 5 and 7 read 1. Odd ports have nothing behind them and read 0xFF.
    /// </summary>
    public byte In(ushort port)
    {
        if ((port & 1) != 0)
        {
            return 0xFF;
        }

        Tape.AdvanceTo(_cpu?.TStates ?? 0, Beeper);
        var ear = !Tape.IsPlaying || Tape.Level ? 0x40 : 0;
        return (byte)(0xA0 | ear | Keyboard.Read((byte)(port >> 8)));
    }

    /// <summary>Gives the ULA the CPU whose T-state count stamps border and speaker changes.</summary>
    public void Connect(Z80Cpu cpu) => _cpu = cpu;

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

        Beeper.SetLevel(time, (value & 0x10) != 0);
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

    /// <summary>Renders the frame, then starts the next one: FLASH counter, border changes.</summary>
    public void EndFrame(ReadOnlySpan<byte> memory)
    {
        Render(memory);
        _frameCount++;
        _frameStartBorder = _border;
        _borderChangeCount = 0;
    }

    private void Render(ReadOnlySpan<byte> memory)
    {
        var flashInverted = (_frameCount / FlashPeriod % 2) == 1;

        // Border pixels are visited in beam order, so the changes are walked once, in order.
        var change = 0;
        var border = Palette.Colors[_frameStartBorder];

        for (var row = 0; row < FrameHeight; row++)
        {
            var line = _frameBuffer.AsSpan(row * FrameWidth, FrameWidth);
            var y = row - BorderTop;
            var lineStart = FirstPixelTState + ((long)y * LineTStates);

            if (y is < 0 or >= ScreenLayout.Height)
            {
                PaintBorder(line, 0, FrameWidth, lineStart, ref change, ref border);
                continue;
            }

            PaintBorder(line, 0, BorderLeft, lineStart, ref change, ref border);

            for (var x = 0; x < ScreenLayout.Width; x += 8)
            {
                var pixels = memory[ScreenLayout.PixelAddress(x, y)];
                var attribute = memory[ScreenLayout.AttributeAddress(x, y)];
                var bright = (attribute & 0x40) != 0;
                var ink = Palette.Colors[Palette.Index(attribute & 0x07, bright)];
                var paper = Palette.Colors[Palette.Index((attribute >> 3) & 0x07, bright)];

                if ((attribute & 0x80) != 0 && flashInverted)
                {
                    (ink, paper) = (paper, ink);
                }

                var cell = line.Slice(BorderLeft + x, 8);
                for (var bit = 0; bit < 8; bit++)
                {
                    cell[bit] = (pixels & (0x80 >> bit)) != 0 ? ink : paper;
                }
            }

            PaintBorder(line, BorderLeft + ScreenLayout.Width, BorderLeft, lineStart, ref change, ref border);
        }
    }

    /// <summary>
    /// Paints <paramref name="count"/> border pixels from <paramref name="first"/>, each with the
    /// colour in effect when the beam reached it. <paramref name="lineStart"/> is the T-state of
    /// the line's first screen-area pixel; the left border comes before it.
    /// </summary>
    private void PaintBorder(Span<uint> line, int first, int count, long lineStart, ref int change, ref uint color)
    {
        for (var x = first; x < first + count; x++)
        {
            var time = lineStart + ((x - BorderLeft) >> 1);
            while (change < _borderChangeCount && _borderChangeTimes[change] <= time)
            {
                color = Palette.Colors[_borderChangeColors[change]];
                change++;
            }

            line[x] = color;
        }
    }
}
