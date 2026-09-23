// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// Help > About iSpectrum: the licence, and the copyright notice Amstrad asks for with the ROMs
/// (roms/README.md). Drawn over the picture in the Spectrum's own font, which has the ©.
/// </summary>
internal sealed class AboutBox(SpectrumFont font)
{
    private const int Scale = 2;
    private const int CharSize = SpectrumFont.GlyphSize * Scale;
    private const int LineHeight = CharSize + 6;
    private const int Padding = 24;

    private static readonly Color Paper = new(215, 215, 215, 255);
    private static readonly Color Ink = Color.Black;
    private static readonly Color Title = new(0, 0, 215, 255);
    private static readonly Color Hint = new(90, 90, 90, 255);

    /// <summary>Laid over the picture behind the box, which would otherwise share its grey.</summary>
    private static readonly Color Shade = new(0, 0, 0, 160);

    private static readonly string[] Lines =
    [
        "iSpectrum",
        "ZX Spectrum 48K and 128K emulator",
        string.Empty,
        "Copyright © 2026 The iSpectrum contributors",
        "Free software under the GNU GPL, version 2 or",
        "later. It comes with ABSOLUTELY NO WARRANTY.",
        string.Empty,
        "Spectrum ROMs © 1982-1986 Sinclair Research",
        "Ltd, copyright Amstrad. Amstrad allows their",
        "distribution but keeps the copyright: the ROMs",
        "may not be sold nor built into hardware.",
        string.Empty,
        "Built with raylib and Raylib-cs (zlib licence).",
        "AY volume table from ayumi, by Peter Sovietov",
        "(MIT licence).",
        string.Empty,
        "Esc, Enter or a click closes this box",
    ];

    private static readonly int Columns = Lines.Max(line => line.Length);

    /// <summary>Set on the redraw that opens the box, whose click (on the menu) must not close it.</summary>
    private bool _opening;

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _opening = true;
    }

    /// <summary>
    /// Takes the keyboard while open: the host's key and character events are used up here, so
    /// that none reaches the Spectrum once the box is closed.
    /// </summary>
    public void Update()
    {
        var close = Raylib.IsMouseButtonPressed(MouseButton.Left) && !_opening;
        _opening = false;
        for (var code = Raylib.GetKeyPressed(); code != 0; code = Raylib.GetKeyPressed())
        {
            close |= (KeyboardKey)code is KeyboardKey.Escape or KeyboardKey.Enter or KeyboardKey.KpEnter;
        }

        while (Raylib.GetCharPressed() != 0)
        {
        }

        if (close)
        {
            IsOpen = false;
        }
    }

    /// <summary>Darkens the area below the menu bar, then draws the box in its middle.</summary>
    public void Draw(int top, int width, int height)
    {
        Raylib.DrawRectangle(0, top, width, height, Shade);

        var boxWidth = (Columns * CharSize) + (2 * Padding);
        var boxHeight = (Lines.Length * LineHeight) + (2 * Padding);
        var x = (width - boxWidth) / 2;
        var y = top + ((height - boxHeight) / 2);

        Raylib.DrawRectangle(x, y, boxWidth, boxHeight, Paper);
        Raylib.DrawRectangleLines(x, y, boxWidth, boxHeight, Ink);

        for (var i = 0; i < Lines.Length; i++)
        {
            var line = Lines[i];
            var color = i == 0 ? Title : i == Lines.Length - 1 ? Hint : Ink;

            // The title and the closing hint are centred, the rest aligned left.
            var left = i == 0 || i == Lines.Length - 1
                ? x + ((boxWidth - (line.Length * CharSize)) / 2)
                : x + Padding;
            font.Draw(line, left, y + Padding + (i * LineHeight), Scale, color);
        }
    }
}
