// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Text;

namespace iSpectrum.Core.Tests;

/// <summary>Boots the real ROM and drives the machine like a user at the keyboard.</summary>
internal static class TestMachine
{
    public static void TypeText(this Spectrum spectrum, string text)
    {
        foreach (var c in text)
        {
            Assert.True(SpectrumCharacters.TryGetKeys(c, out var key, out var shift), $"Cannot type '{c}'");
            if (shift is { } shiftKey)
            {
                Type(spectrum, shiftKey, key);
            }
            else
            {
                Type(spectrum, key);
            }
        }
    }

    public static byte[] Rom(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", name));

    /// <summary>A 128K, powered on but not yet booted.</summary>
    public static Spectrum128 New128() => new(Rom("128-0.rom"), Rom("128-1.rom"));

    public static Spectrum48 Boot()
    {
        var spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")));
        RunFrames(spectrum, 150);
        return spectrum;
    }

    /// <summary>
    /// Holds the keys for 5 frames, then releases them for 5 frames: long enough for the ROM,
    /// which scans the keyboard once per frame, and short of its auto-repeat delay.
    /// </summary>
    public static void Type(this Spectrum spectrum, params SpectrumKey[] keys)
    {
        foreach (var key in keys)
        {
            spectrum.Keyboard.SetKey(key, true);
        }

        RunFrames(spectrum, 5);
        spectrum.Keyboard.ReleaseAll();
        RunFrames(spectrum, 5);
    }

    public static void RunFrames(this Spectrum spectrum, int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            spectrum.RunFrame();
        }
    }
}

/// <summary>Reads text off the screen by matching each 8x8 cell against the ROM font.</summary>
internal static class ScreenText
{
    /// <summary>The ROM character set: 96 characters from space (0x20) to (C) (0x7F), 8 bytes each.</summary>
    private const int FontAddress = 0x3D00;

    private const int FontLength = 96 * 8;

    /// <summary>Reads a row of a 48K, whose 64 KB <paramref name="memory"/> holds both the screen and the font.</summary>
    public static string ReadRow(ReadOnlySpan<byte> memory, int row) =>
        ReadRow(memory[ScreenLayout.BitmapAddress..], memory.Slice(FontAddress, FontLength), row);

    /// <summary>Reads a row of a 128K's displayed screen, with the font of ROM 1 (48K BASIC).</summary>
    public static string ReadRow(Spectrum128 spectrum, int row) =>
        ReadRow(spectrum.Memory.Screen, TestMachine.Rom("128-1.rom").AsSpan(FontAddress, FontLength), row);

    /// <param name="screen">The displayed bank: the screen at its start (see <see cref="SpectrumMemory.Screen"/>).</param>
    public static string ReadRow(ReadOnlySpan<byte> screen, ReadOnlySpan<byte> font, int row)
    {
        var text = new StringBuilder(32);
        for (var column = 0; column < 32; column++)
        {
            text.Append(ReadCell(screen, font, column, row));
        }

        return text.ToString();
    }

    private static char ReadCell(ReadOnlySpan<byte> screen, ReadOnlySpan<byte> font, int column, int row)
    {
        Span<byte> cell = stackalloc byte[8];
        for (var line = 0; line < 8; line++)
        {
            cell[line] = screen[ScreenLayout.PixelAddress(column * 8, (row * 8) + line) - ScreenLayout.BitmapAddress];
        }

        for (var code = 0x20; code <= 0x7F; code++)
        {
            if (cell.SequenceEqual(font.Slice((code - 0x20) * 8, 8)))
            {
                return code == 0x7F ? '©' : (char)code;
            }
        }

        return '?';
    }
}
