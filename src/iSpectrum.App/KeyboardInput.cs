// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core;
using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// Turns the Mac keyboard into Spectrum key presses, whatever the host layout.
/// </summary>
/// <remarks>
/// Typed characters are translated: the host tells which character a key produced (" is
/// Shift+2 on a Swiss keyboard), and the Spectrum keys that type it (Symbol Shift + P) are held
/// for as long as that host key stays down. Shift alone is Caps Shift and Control alone is Symbol
/// Shift, as games expect. While Control is down, letters and digits map by position instead
/// (Raylib names keys after US QWERTY, the Spectrum's layout), so every raw combination stays
/// reachable. Option is left to the host, which uses it to type characters.
/// </remarks>
internal sealed class KeyboardInput
{
    private const int MaxEvents = 16;
    private const int MaxHeld = 16;

    /// <summary>Keys that type no character, with the Spectrum keys they stand for.</summary>
    private static readonly (KeyboardKey Host, SpectrumKey Key, SpectrumKey? Shift)[] SpecialKeys =
    [
        (KeyboardKey.Enter, SpectrumKey.Enter, null),
        (KeyboardKey.KpEnter, SpectrumKey.Enter, null),
        (KeyboardKey.Backspace, SpectrumKey.D0, SpectrumKey.CapsShift),       // DELETE
        (KeyboardKey.Escape, SpectrumKey.Space, SpectrumKey.CapsShift),       // BREAK
        (KeyboardKey.Tab, SpectrumKey.SymbolShift, SpectrumKey.CapsShift),    // extended mode
        (KeyboardKey.Left, SpectrumKey.D5, SpectrumKey.CapsShift),
        (KeyboardKey.Down, SpectrumKey.D6, SpectrumKey.CapsShift),
        (KeyboardKey.Up, SpectrumKey.D7, SpectrumKey.CapsShift),
        (KeyboardKey.Right, SpectrumKey.D8, SpectrumKey.CapsShift),
        (KeyboardKey.F1, SpectrumKey.D1, SpectrumKey.CapsShift),              // EDIT
        (KeyboardKey.F2, SpectrumKey.D2, SpectrumKey.CapsShift),              // CAPS LOCK
        (KeyboardKey.F3, SpectrumKey.D3, SpectrumKey.CapsShift),              // TRUE VIDEO
        (KeyboardKey.F4, SpectrumKey.D4, SpectrumKey.CapsShift),              // INV. VIDEO
        (KeyboardKey.F5, SpectrumKey.D5, SpectrumKey.CapsShift),
        (KeyboardKey.F6, SpectrumKey.D6, SpectrumKey.CapsShift),
        (KeyboardKey.F7, SpectrumKey.D7, SpectrumKey.CapsShift),
        (KeyboardKey.F8, SpectrumKey.D8, SpectrumKey.CapsShift),
        (KeyboardKey.F9, SpectrumKey.D9, SpectrumKey.CapsShift),              // GRAPHICS
    ];

    /// <summary>Position-based keys, used only while Control is down.</summary>
    private static readonly (KeyboardKey Host, SpectrumKey Key)[] PositionalKeys =
    [
        (KeyboardKey.A, SpectrumKey.A), (KeyboardKey.B, SpectrumKey.B), (KeyboardKey.C, SpectrumKey.C),
        (KeyboardKey.D, SpectrumKey.D), (KeyboardKey.E, SpectrumKey.E), (KeyboardKey.F, SpectrumKey.F),
        (KeyboardKey.G, SpectrumKey.G), (KeyboardKey.H, SpectrumKey.H), (KeyboardKey.I, SpectrumKey.I),
        (KeyboardKey.J, SpectrumKey.J), (KeyboardKey.K, SpectrumKey.K), (KeyboardKey.L, SpectrumKey.L),
        (KeyboardKey.M, SpectrumKey.M), (KeyboardKey.N, SpectrumKey.N), (KeyboardKey.O, SpectrumKey.O),
        (KeyboardKey.P, SpectrumKey.P), (KeyboardKey.Q, SpectrumKey.Q), (KeyboardKey.R, SpectrumKey.R),
        (KeyboardKey.S, SpectrumKey.S), (KeyboardKey.T, SpectrumKey.T), (KeyboardKey.U, SpectrumKey.U),
        (KeyboardKey.V, SpectrumKey.V), (KeyboardKey.W, SpectrumKey.W), (KeyboardKey.X, SpectrumKey.X),
        (KeyboardKey.Y, SpectrumKey.Y), (KeyboardKey.Z, SpectrumKey.Z),
        (KeyboardKey.Zero, SpectrumKey.D0), (KeyboardKey.One, SpectrumKey.D1), (KeyboardKey.Two, SpectrumKey.D2),
        (KeyboardKey.Three, SpectrumKey.D3), (KeyboardKey.Four, SpectrumKey.D4), (KeyboardKey.Five, SpectrumKey.D5),
        (KeyboardKey.Six, SpectrumKey.D6), (KeyboardKey.Seven, SpectrumKey.D7), (KeyboardKey.Eight, SpectrumKey.D8),
        (KeyboardKey.Nine, SpectrumKey.D9), (KeyboardKey.Space, SpectrumKey.Space),
    ];

    private readonly KeyboardKey[] _pressed = new KeyboardKey[MaxEvents];
    private readonly int[] _characters = new int[MaxEvents];

    /// <summary>Translated characters, held while the host key that typed them is down.</summary>
    private readonly (KeyboardKey Host, SpectrumKey Key, SpectrumKey? Shift)[] _held =
        new (KeyboardKey, SpectrumKey, SpectrumKey?)[MaxHeld];

    private int _heldCount;

    /// <summary>Reads this frame's host events and sets the Spectrum matrix accordingly.</summary>
    public void Update(Keyboard keyboard)
    {
        var pressedCount = ReadPressedKeys();
        var characterCount = ReadCharacters();
        var control = IsDown(KeyboardKey.LeftControl) || IsDown(KeyboardKey.RightControl);
        var command = IsDown(KeyboardKey.LeftSuper) || IsDown(KeyboardKey.RightSuper);

        ReleaseCharacters();

        // Command shortcuts belong to macOS; with Control, keys map by position instead.
        if (!control && !command)
        {
            HoldCharacters(pressedCount, characterCount);
        }

        keyboard.ReleaseAll();

        foreach (var (host, key, shift) in SpecialKeys)
        {
            if (IsDown(host))
            {
                Press(keyboard, key, shift);
            }
        }

        var hostShift = IsDown(KeyboardKey.LeftShift) || IsDown(KeyboardKey.RightShift);

        if (control)
        {
            keyboard.SetKey(SpectrumKey.SymbolShift, true);
            keyboard.SetKey(SpectrumKey.CapsShift, hostShift);
            foreach (var (host, key) in PositionalKeys)
            {
                if (IsDown(host))
                {
                    keyboard.SetKey(key, true);
                }
            }
        }
        else if (hostShift && _heldCount == 0)
        {
            // Shift alone is Caps Shift; while it types a character (Shift+2 = "), it is not.
            keyboard.SetKey(SpectrumKey.CapsShift, true);
        }

        for (var i = 0; i < _heldCount; i++)
        {
            Press(keyboard, _held[i].Key, _held[i].Shift);
        }
    }

    /// <summary>Keys pressed this frame that may type a character (not modifiers, not special keys).</summary>
    private int ReadPressedKeys()
    {
        var count = 0;
        for (var code = Raylib.GetKeyPressed(); code != 0; code = Raylib.GetKeyPressed())
        {
            var key = (KeyboardKey)code;
            if (count < MaxEvents && !IsModifier(key) && !IsSpecial(key))
            {
                _pressed[count++] = key;
            }
        }

        return count;
    }

    private int ReadCharacters()
    {
        var count = 0;
        for (var c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed())
        {
            if (count < MaxEvents)
            {
                _characters[count++] = c;
            }
        }

        return count;
    }

    /// <summary>Forgets the characters whose host key has been released.</summary>
    private void ReleaseCharacters()
    {
        var kept = 0;
        for (var i = 0; i < _heldCount; i++)
        {
            if (IsDown(_held[i].Host))
            {
                _held[kept++] = _held[i];
            }
        }

        _heldCount = kept;
    }

    /// <summary>
    /// Pairs each new character with the key that typed it, from the most recent backwards: a
    /// dead key (^ on a Swiss keyboard) is pressed without typing anything, and key repeat types
    /// characters without a new press, so the two lists do not always line up from the start.
    /// A character is held for at least this frame, even if its key is already up again.
    /// </summary>
    private void HoldCharacters(int pressedCount, int characterCount)
    {
        for (int c = characterCount - 1, k = pressedCount - 1; c >= 0 && k >= 0; c--, k--)
        {
            if (_characters[c] <= char.MaxValue
                && SpectrumCharacters.TryGetKeys((char)_characters[c], out var key, out var shift)
                && _heldCount < MaxHeld)
            {
                _held[_heldCount++] = (_pressed[k], key, shift);
            }
        }
    }

    private static void Press(Keyboard keyboard, SpectrumKey key, SpectrumKey? shift)
    {
        keyboard.SetKey(key, true);
        if (shift is { } shiftKey)
        {
            keyboard.SetKey(shiftKey, true);
        }
    }

    private static bool IsDown(KeyboardKey key) => Raylib.IsKeyDown(key);

    private static bool IsModifier(KeyboardKey key) => key is KeyboardKey.LeftShift or KeyboardKey.RightShift
        or KeyboardKey.LeftControl or KeyboardKey.RightControl or KeyboardKey.LeftAlt or KeyboardKey.RightAlt
        or KeyboardKey.LeftSuper or KeyboardKey.RightSuper or KeyboardKey.CapsLock;

    private static bool IsSpecial(KeyboardKey key)
    {
        foreach (var special in SpecialKeys)
        {
            if (special.Host == key)
            {
                return true;
            }
        }

        return false;
    }
}
