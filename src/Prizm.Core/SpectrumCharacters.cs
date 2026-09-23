// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>
/// Which Spectrum keys type a given character, as printed on the 48K keyboard: letters and digits
/// directly, capitals with Caps Shift, the red symbols with Symbol Shift, and the few symbols
/// printed under the keys ([ ] { } ~ | \ ©) with Symbol Shift in extended mode.
/// </summary>
public static class SpectrumCharacters
{
    /// <summary>
    /// Gets the key for <paramref name="c"/> and the shift key to hold with it, if any.
    /// Returns false for characters the keyboard cannot type in one combination.
    /// </summary>
    public static bool TryGetKeys(char c, out SpectrumKey key, out SpectrumKey? shift)
    {
        shift = null;

        if (c is >= 'a' and <= 'z')
        {
            key = Letter(c - 'a');
            return true;
        }

        if (c is >= 'A' and <= 'Z')
        {
            key = Letter(c - 'A');
            shift = SpectrumKey.CapsShift;
            return true;
        }

        if (c is >= '0' and <= '9')
        {
            key = Digit(c - '0');
            return true;
        }

        if (c == ' ')
        {
            key = SpectrumKey.Space;
            return true;
        }

        SpectrumKey? symbol = c switch
        {
            '!' => SpectrumKey.D1,
            '@' => SpectrumKey.D2,
            '#' => SpectrumKey.D3,
            '$' => SpectrumKey.D4,
            '%' => SpectrumKey.D5,
            '&' => SpectrumKey.D6,
            '\'' => SpectrumKey.D7,
            '(' => SpectrumKey.D8,
            ')' => SpectrumKey.D9,
            '_' => SpectrumKey.D0,
            '≤' => SpectrumKey.Q,
            '≠' => SpectrumKey.W,
            '≥' => SpectrumKey.E,
            '<' => SpectrumKey.R,
            '>' => SpectrumKey.T,
            ';' => SpectrumKey.O,
            '"' => SpectrumKey.P,
            '^' or '↑' => SpectrumKey.H,
            '-' => SpectrumKey.J,
            '+' => SpectrumKey.K,
            '=' => SpectrumKey.L,
            ':' => SpectrumKey.Z,
            '£' => SpectrumKey.X,
            '?' => SpectrumKey.C,
            '/' => SpectrumKey.V,
            '*' => SpectrumKey.B,
            ',' => SpectrumKey.N,
            '.' => SpectrumKey.M,
            _ => null,
        };

        key = symbol.GetValueOrDefault();
        if (symbol is null)
        {
            return false;
        }

        shift = SpectrumKey.SymbolShift;
        return true;
    }

    /// <summary>
    /// Gets the key for a character typed in extended mode: Caps Shift + Symbol Shift first (the
    /// E cursor), then Symbol Shift and <paramref name="key"/>. The ROM returns to the L cursor
    /// on its own after the character.
    /// </summary>
    public static bool TryGetExtendedKey(char c, out SpectrumKey key)
    {
        SpectrumKey? extended = c switch
        {
            '[' => SpectrumKey.Y,
            ']' => SpectrumKey.U,
            '{' => SpectrumKey.F,
            '}' => SpectrumKey.G,
            '~' => SpectrumKey.A,
            '|' => SpectrumKey.S,
            '\\' => SpectrumKey.D,
            '©' => SpectrumKey.P,
            _ => null,
        };

        key = extended.GetValueOrDefault();
        return extended is not null;
    }

    /// <summary>The strokes that type <paramref name="c"/> in extended mode, for <see cref="AutoTyper"/>; null if it is not such a character.</summary>
    public static SpectrumKey[][]? ExtendedStrokes(char c) => TryGetExtendedKey(c, out var key)
        ? [[SpectrumKey.CapsShift, SpectrumKey.SymbolShift], [SpectrumKey.SymbolShift, key]]
        : null;

    private static SpectrumKey Letter(int index) => index switch
    {
        0 => SpectrumKey.A, 1 => SpectrumKey.B, 2 => SpectrumKey.C, 3 => SpectrumKey.D,
        4 => SpectrumKey.E, 5 => SpectrumKey.F, 6 => SpectrumKey.G, 7 => SpectrumKey.H,
        8 => SpectrumKey.I, 9 => SpectrumKey.J, 10 => SpectrumKey.K, 11 => SpectrumKey.L,
        12 => SpectrumKey.M, 13 => SpectrumKey.N, 14 => SpectrumKey.O, 15 => SpectrumKey.P,
        16 => SpectrumKey.Q, 17 => SpectrumKey.R, 18 => SpectrumKey.S, 19 => SpectrumKey.T,
        20 => SpectrumKey.U, 21 => SpectrumKey.V, 22 => SpectrumKey.W, 23 => SpectrumKey.X,
        24 => SpectrumKey.Y, _ => SpectrumKey.Z,
    };

    private static SpectrumKey Digit(int value) => value switch
    {
        0 => SpectrumKey.D0, 1 => SpectrumKey.D1, 2 => SpectrumKey.D2, 3 => SpectrumKey.D3,
        4 => SpectrumKey.D4, 5 => SpectrumKey.D5, 6 => SpectrumKey.D6, 7 => SpectrumKey.D7,
        8 => SpectrumKey.D8, _ => SpectrumKey.D9,
    };
}
