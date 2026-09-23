// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// The menu of a TZX select block: the tape waits while the user picks where it goes on, with
/// the arrows and Enter, a digit, or a click. Drawn like the About box, in the Spectrum's font.
/// </summary>
internal sealed class SelectBox(SpectrumFont font)
{
    private const int Scale = 2;
    private const int CharSize = SpectrumFont.GlyphSize * Scale;
    private const int LineHeight = CharSize + 6;
    private const int Padding = 24;

    /// <summary>Longer choices are cut, so that the box fits in the window.</summary>
    private const int MaxTextLength = 40;

    private const string Title = "Tape menu";
    private const string Hint = "Enter chooses, Esc goes on";

    /// <summary>The choices start this many lines down, below the title and a blank line.</summary>
    private const int FirstChoiceLine = 2;

    private static readonly Color Paper = new(215, 215, 215, 255);
    private static readonly Color Ink = Color.Black;
    private static readonly Color Highlight = new(0, 0, 215, 255);
    private static readonly Color HintColor = new(90, 90, 90, 255);
    private static readonly Color Shade = new(0, 0, 0, 160);

    private string[] _lines = [];
    private int _selected;

    /// <summary>Set on the redraw that opens the box, so that a click already under way does not choose.</summary>
    private bool _opening;

    public bool IsOpen { get; private set; }

    public void Open(IReadOnlyList<string> texts)
    {
        _lines = new string[texts.Count];
        for (var i = 0; i < texts.Count; i++)
        {
            var text = texts[i].Length > MaxTextLength ? texts[i][..MaxTextLength] : texts[i];
            _lines[i] = i < 9 ? $"{i + 1} {text}" : $"  {text}";
        }

        _selected = 0;
        IsOpen = true;
        _opening = true;
    }

    public void Close() => IsOpen = false;

    /// <summary>
    /// Takes the keyboard and the mouse while open. Returns the choice made on this redraw, and
    /// closes the box: the index of a choice, or -1 to go on without one (Esc); null while the
    /// user is still choosing.
    /// </summary>
    public int? Update(int top, int width, int height)
    {
        int? choice = null;
        for (var code = Raylib.GetKeyPressed(); code != 0; code = Raylib.GetKeyPressed())
        {
            switch ((KeyboardKey)code)
            {
                case KeyboardKey.Up:
                    _selected = (_selected + _lines.Length - 1) % _lines.Length;
                    break;
                case KeyboardKey.Down:
                    _selected = (_selected + 1) % _lines.Length;
                    break;
                case KeyboardKey.Enter or KeyboardKey.KpEnter:
                    choice ??= _selected;
                    break;
                case KeyboardKey.Escape:
                    choice ??= -1;
                    break;
            }
        }

        // Digits come as characters, whatever the keyboard layout.
        for (var c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed())
        {
            if (c is >= '1' and <= '9' && c - '1' < _lines.Length)
            {
                choice ??= c - '1';
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !_opening)
        {
            var (x, y, boxWidth, _) = Layout(top, width, height);
            var mouse = Raylib.GetMousePosition();
            var line = ((int)mouse.Y - y - Padding) / LineHeight - FirstChoiceLine;
            if (mouse.X >= x && mouse.X < x + boxWidth && mouse.Y >= y + Padding && line >= 0 && line < _lines.Length)
            {
                choice ??= line;
            }
        }

        _opening = false;
        if (choice is not null)
        {
            IsOpen = false;
        }

        return choice;
    }

    /// <summary>Darkens the area below the menu bar, then draws the box in its middle.</summary>
    public void Draw(int top, int width, int height)
    {
        Raylib.DrawRectangle(0, top, width, height, Shade);

        var (x, y, boxWidth, boxHeight) = Layout(top, width, height);
        Raylib.DrawRectangle(x, y, boxWidth, boxHeight, Paper);
        Raylib.DrawRectangleLines(x, y, boxWidth, boxHeight, Ink);

        font.Draw(Title, x + ((boxWidth - (Title.Length * CharSize)) / 2), y + Padding, Scale, Highlight);
        for (var i = 0; i < _lines.Length; i++)
        {
            var lineY = y + Padding + ((FirstChoiceLine + i) * LineHeight);
            if (i == _selected)
            {
                // The chosen line in inverse video, as the 128K's menus show it.
                Raylib.DrawRectangle(x + Padding - 4, lineY - 3, boxWidth - (2 * Padding) + 8, LineHeight, Highlight);
            }

            font.Draw(_lines[i], x + Padding, lineY, Scale, i == _selected ? Paper : Ink);
        }

        var hintY = y + Padding + ((FirstChoiceLine + _lines.Length + 1) * LineHeight);
        font.Draw(Hint, x + ((boxWidth - (Hint.Length * CharSize)) / 2), hintY, Scale, HintColor);
    }

    private (int X, int Y, int Width, int Height) Layout(int top, int width, int height)
    {
        var columns = Math.Max(Title.Length, Hint.Length);
        foreach (var line in _lines)
        {
            columns = Math.Max(columns, line.Length);
        }

        // Title, blank line, the choices, blank line, hint.
        var lines = FirstChoiceLine + _lines.Length + 2;
        var boxWidth = (columns * CharSize) + (2 * Padding);
        var boxHeight = (lines * LineHeight) + (2 * Padding);
        return ((width - boxWidth) / 2, top + ((height - boxHeight) / 2), boxWidth, boxHeight);
    }
}
