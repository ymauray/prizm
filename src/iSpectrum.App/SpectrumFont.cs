// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Numerics;
using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// The Spectrum's own character set, read from the 48K ROM: a fixed-width 8x8 font for the
/// debugger's columns of addresses and bytes, with no font file to add. Characters outside
/// space to (C) are drawn as '?'.
/// </summary>
internal sealed class SpectrumFont : IDisposable
{
    public const int GlyphSize = 8;

    /// <summary>The character set in the 48K ROM: 96 characters from space, 8 bytes each.</summary>
    private const int FontAddress = 0x3D00;

    private const int CharacterCount = 96;

    private readonly Texture2D _atlas;

    public SpectrumFont(ReadOnlySpan<byte> rom48)
    {
        // White glyphs on a transparent background, tinted when drawn.
        var image = Raylib.GenImageColor(CharacterCount * GlyphSize, GlyphSize, Color.Blank);
        for (var character = 0; character < CharacterCount; character++)
        {
            for (var row = 0; row < GlyphSize; row++)
            {
                var bits = rom48[FontAddress + (character * GlyphSize) + row];
                for (var column = 0; column < GlyphSize; column++)
                {
                    if ((bits & (0x80 >> column)) != 0)
                    {
                        Raylib.ImageDrawPixel(ref image, (character * GlyphSize) + column, row, Color.White);
                    }
                }
            }
        }

        _atlas = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
    }

    /// <summary>Draws <paramref name="text"/> with each 8x8 glyph scaled by <paramref name="scale"/>.</summary>
    public void Draw(ReadOnlySpan<char> text, int x, int y, int scale, Color color)
    {
        var size = GlyphSize * scale;
        for (var i = 0; i < text.Length; i++)
        {
            var source = new Rectangle(Index(text[i]) * GlyphSize, 0, GlyphSize, GlyphSize);
            var destination = new Rectangle(x + (i * size), y, size, size);
            Raylib.DrawTexturePro(_atlas, source, destination, Vector2.Zero, 0, color);
        }
    }

    public void Dispose() => Raylib.UnloadTexture(_atlas);

    private static int Index(char c) => c switch
    {
        '©' => CharacterCount - 1,
        >= ' ' and <= '~' => c - ' ',
        _ => '?' - ' ',
    };
}
