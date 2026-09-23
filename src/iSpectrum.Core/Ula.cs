// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// The ULA: port 0xFE (keyboard and border; the beeper comes with milestone 5) and
/// the video output. The whole frame is rendered at once at the end of each frame; line-by-line
/// rendering, needed for border effects, comes with contention (milestone 7).
/// </summary>
public sealed class Ula : IIo
{
    public const int BorderLeft = 32;
    public const int BorderTop = 32;
    public const int FrameWidth = ScreenLayout.Width + (2 * BorderLeft);
    public const int FrameHeight = ScreenLayout.Height + (2 * BorderTop);

    /// <summary>FLASH attributes swap ink and paper every 16 frames.</summary>
    private const int FlashPeriod = 16;

    private readonly uint[] _frameBuffer = new uint[FrameWidth * FrameHeight];
    private int _frameCount;

    /// <summary>The key matrix read through port 0xFE.</summary>
    public Keyboard Keyboard { get; } = new();

    private int _border;

    /// <summary>Border colour, 0-7: bits 0-2 of a write to port 0xFE, or restored from a snapshot.</summary>
    public int Border
    {
        get => _border;
        set => _border = value & 0x07;
    }

    /// <summary>FrameWidth x FrameHeight pixels, row by row, in <see cref="Palette"/> format.</summary>
    public ReadOnlySpan<uint> FrameBuffer => _frameBuffer;

    /// <summary>
    /// Any even port selects the ULA: bits 0-4 are the keyboard half-rows selected by the high
    /// byte of the port address. Bits 5 and 7 read 1; bit 6 (EAR) reads 1 too until the tape
    /// input exists. Odd ports have nothing behind them and read 0xFF.
    /// </summary>
    public byte In(ushort port) =>
        (port & 1) == 0 ? (byte)(0xE0 | Keyboard.Read((byte)(port >> 8))) : (byte)0xFF;

    public void Out(ushort port, byte value)
    {
        if ((port & 1) == 0)
        {
            Border = value;
        }
    }

    /// <summary>Renders the frame from the current memory, then advances the FLASH counter.</summary>
    public void EndFrame(ReadOnlySpan<byte> memory)
    {
        Render(memory);
        _frameCount++;
    }

    private void Render(ReadOnlySpan<byte> memory)
    {
        var border = Palette.Colors[Border];
        var flashInverted = (_frameCount / FlashPeriod % 2) == 1;

        for (var row = 0; row < FrameHeight; row++)
        {
            var line = _frameBuffer.AsSpan(row * FrameWidth, FrameWidth);
            var y = row - BorderTop;

            if (y is < 0 or >= ScreenLayout.Height)
            {
                line.Fill(border);
                continue;
            }

            line[..BorderLeft].Fill(border);
            line[(BorderLeft + ScreenLayout.Width)..].Fill(border);

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
        }
    }
}
