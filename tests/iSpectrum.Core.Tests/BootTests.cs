// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Text;

namespace iSpectrum.Core.Tests;

/// <summary>Boots the real 48K ROM and reads back what it prints.</summary>
public class BootTests
{
    [Fact]
    public void Rom_BootsToTheCopyrightMessage()
    {
        var spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")));

        // The ROM tests the RAM first; the message appears after about 85 frames (1.7 s).
        for (var frame = 0; frame < 150; frame++)
        {
            spectrum.RunFrame();
        }

        Assert.Equal("© 1982 Sinclair Research Ltd", ScreenText.ReadRow(spectrum.Memory.Contents, 23).TrimEnd());
        Assert.Equal(7, spectrum.Ula.Border);
    }
}

/// <summary>Reads text off the screen by matching each 8x8 cell against the ROM font.</summary>
internal static class ScreenText
{
    /// <summary>The ROM character set: 96 characters from space (0x20) to (C) (0x7F), 8 bytes each.</summary>
    private const int FontAddress = 0x3D00;

    public static string ReadRow(ReadOnlySpan<byte> memory, int row)
    {
        var text = new StringBuilder(32);
        for (var column = 0; column < 32; column++)
        {
            text.Append(ReadCell(memory, column, row));
        }

        return text.ToString();
    }

    private static char ReadCell(ReadOnlySpan<byte> memory, int column, int row)
    {
        Span<byte> cell = stackalloc byte[8];
        for (var line = 0; line < 8; line++)
        {
            cell[line] = memory[ScreenLayout.PixelAddress(column * 8, (row * 8) + line)];
        }

        for (var code = 0x20; code <= 0x7F; code++)
        {
            if (cell.SequenceEqual(memory.Slice(FontAddress + ((code - 0x20) * 8), 8)))
            {
                return code == 0x7F ? '©' : (char)code;
            }
        }

        return '?';
    }
}
