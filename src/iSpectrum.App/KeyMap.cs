// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core;
using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// Maps host keys to the Spectrum matrix by physical position: Raylib names keys after the US
/// QWERTY layout, which is also the Spectrum's, so each host key stands for the Spectrum key in
/// the same place. A few host keys press two Spectrum keys, as the Spectrum+ keyboard did.
/// </summary>
internal static class KeyMap
{
    private static readonly (KeyboardKey Host, SpectrumKey Key, SpectrumKey? Shift)[] Map =
    [
        (KeyboardKey.A, SpectrumKey.A, null), (KeyboardKey.B, SpectrumKey.B, null),
        (KeyboardKey.C, SpectrumKey.C, null), (KeyboardKey.D, SpectrumKey.D, null),
        (KeyboardKey.E, SpectrumKey.E, null), (KeyboardKey.F, SpectrumKey.F, null),
        (KeyboardKey.G, SpectrumKey.G, null), (KeyboardKey.H, SpectrumKey.H, null),
        (KeyboardKey.I, SpectrumKey.I, null), (KeyboardKey.J, SpectrumKey.J, null),
        (KeyboardKey.K, SpectrumKey.K, null), (KeyboardKey.L, SpectrumKey.L, null),
        (KeyboardKey.M, SpectrumKey.M, null), (KeyboardKey.N, SpectrumKey.N, null),
        (KeyboardKey.O, SpectrumKey.O, null), (KeyboardKey.P, SpectrumKey.P, null),
        (KeyboardKey.Q, SpectrumKey.Q, null), (KeyboardKey.R, SpectrumKey.R, null),
        (KeyboardKey.S, SpectrumKey.S, null), (KeyboardKey.T, SpectrumKey.T, null),
        (KeyboardKey.U, SpectrumKey.U, null), (KeyboardKey.V, SpectrumKey.V, null),
        (KeyboardKey.W, SpectrumKey.W, null), (KeyboardKey.X, SpectrumKey.X, null),
        (KeyboardKey.Y, SpectrumKey.Y, null), (KeyboardKey.Z, SpectrumKey.Z, null),
        (KeyboardKey.Zero, SpectrumKey.D0, null), (KeyboardKey.One, SpectrumKey.D1, null),
        (KeyboardKey.Two, SpectrumKey.D2, null), (KeyboardKey.Three, SpectrumKey.D3, null),
        (KeyboardKey.Four, SpectrumKey.D4, null), (KeyboardKey.Five, SpectrumKey.D5, null),
        (KeyboardKey.Six, SpectrumKey.D6, null), (KeyboardKey.Seven, SpectrumKey.D7, null),
        (KeyboardKey.Eight, SpectrumKey.D8, null), (KeyboardKey.Nine, SpectrumKey.D9, null),
        (KeyboardKey.Enter, SpectrumKey.Enter, null), (KeyboardKey.KpEnter, SpectrumKey.Enter, null),
        (KeyboardKey.Space, SpectrumKey.Space, null),

        // Shift keys: Shift is Caps Shift; Control and Option are Symbol Shift.
        (KeyboardKey.LeftShift, SpectrumKey.CapsShift, null),
        (KeyboardKey.RightShift, SpectrumKey.CapsShift, null),
        (KeyboardKey.LeftControl, SpectrumKey.SymbolShift, null),
        (KeyboardKey.RightControl, SpectrumKey.SymbolShift, null),
        (KeyboardKey.LeftAlt, SpectrumKey.SymbolShift, null),
        (KeyboardKey.RightAlt, SpectrumKey.SymbolShift, null),

        // Shortcuts: DELETE (Caps Shift + 0), BREAK (Caps Shift + Space), cursors (Caps Shift + 5-8).
        (KeyboardKey.Backspace, SpectrumKey.D0, SpectrumKey.CapsShift),
        (KeyboardKey.Escape, SpectrumKey.Space, SpectrumKey.CapsShift),
        (KeyboardKey.Left, SpectrumKey.D5, SpectrumKey.CapsShift),
        (KeyboardKey.Down, SpectrumKey.D6, SpectrumKey.CapsShift),
        (KeyboardKey.Up, SpectrumKey.D7, SpectrumKey.CapsShift),
        (KeyboardKey.Right, SpectrumKey.D8, SpectrumKey.CapsShift),
    ];

    /// <summary>Sets the matrix from the host keys currently held down.</summary>
    public static void Apply(Keyboard keyboard)
    {
        keyboard.ReleaseAll();

        foreach (var (host, key, shift) in Map)
        {
            if (!Raylib.IsKeyDown(host))
            {
                continue;
            }

            keyboard.SetKey(key, true);
            if (shift is { } shiftKey)
            {
                keyboard.SetKey(shiftKey, true);
            }
        }
    }
}
