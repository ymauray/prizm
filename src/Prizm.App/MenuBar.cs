// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using System.Numerics;
using Raylib_cs;

namespace Prizm.App;

/// <summary>
/// An entry of a menu, with its keyboard shortcut (Cmd + <see cref="Key"/>, with Shift if
/// <see cref="Shift"/>); an empty label is a separator.
/// </summary>
internal sealed record MenuItem(string Label, KeyboardKey Key, Action Execute, Func<bool>? IsChecked = null, bool Shift = false)
{
    public static readonly MenuItem Separator = new(string.Empty, KeyboardKey.Null, () => { });

    public bool IsSeparator => Label.Length == 0;

    /// <summary>The shortcut as shown in the menu, built once.</summary>
    public string Shortcut { get; } = Key switch
    {
        KeyboardKey.Null => string.Empty,
        >= KeyboardKey.Zero and <= KeyboardKey.Nine => $"Cmd+{(Shift ? "Shift+" : string.Empty)}{Key - KeyboardKey.Zero}",
        _ => $"Cmd+{(Shift ? "Shift+" : string.Empty)}{Key}",
    };
}

internal sealed record Menu(string Title, MenuItem[] Items);

/// <summary>
/// A menu bar drawn at the top of the window with Raylib's own font, used with the mouse; each
/// entry also answers its Cmd shortcut. Temporary, until a front-end with native menus.
/// </summary>
/// <remarks>Styled after the 128K's menu: black bar, white text, cyan highlight.</remarks>
internal sealed class MenuBar
{
    public const int Height = 28;

    private const int FontSize = 20;
    private const int TextTop = (Height - FontSize) / 2;
    private const int TitlePadding = 14;
    private const int ItemHeight = 28;
    private const int ItemPadding = 12;
    private const int CheckWidth = 22;
    private const int ShortcutGap = 40;
    private const int SeparatorHeight = 10;

    private static readonly Color Bar = Color.Black;
    private static readonly Color BarText = Color.RayWhite;
    private static readonly Color Highlight = new(0, 215, 215, 255);
    private static readonly Color Panel = new(215, 215, 215, 255);
    private static readonly Color PanelText = Color.Black;

    /// <summary>The red, yellow, green and cyan stripes of the Spectrum logo.</summary>
    private static readonly Color[] Stripes = [new(215, 0, 0, 255), new(215, 215, 0, 255), new(0, 215, 0, 255), Highlight];

    private readonly Menu[] _menus;
    private readonly int[] _titleX;
    private readonly int[] _titleWidth;
    private readonly int[] _panelWidth;

    /// <summary>The open menu, or -1.</summary>
    private int _open = -1;

    public MenuBar(Menu[] menus)
    {
        _menus = menus;
        _titleX = new int[menus.Length];
        _titleWidth = new int[menus.Length];
        _panelWidth = new int[menus.Length];

        var x = 0;
        for (var i = 0; i < menus.Length; i++)
        {
            _titleX[i] = x;
            _titleWidth[i] = Raylib.MeasureText(menus[i].Title, FontSize) + (2 * TitlePadding);
            x += _titleWidth[i];

            var labels = 0;
            var shortcuts = 0;
            foreach (var item in menus[i].Items)
            {
                labels = Math.Max(labels, Raylib.MeasureText(item.Label, FontSize));
                shortcuts = Math.Max(shortcuts, Raylib.MeasureText(item.Shortcut, FontSize));
            }

            _panelWidth[i] = CheckWidth + labels + ShortcutGap + shortcuts + (2 * ItemPadding);
        }
    }

    /// <summary>Whether a menu is open (the mouse then belongs to the menus).</summary>
    public bool IsOpen => _open >= 0;

    /// <summary>Runs the entry whose Cmd shortcut was just pressed, if any.</summary>
    public void HandleShortcuts()
    {
        if (!Raylib.IsKeyDown(KeyboardKey.LeftSuper) && !Raylib.IsKeyDown(KeyboardKey.RightSuper))
        {
            return;
        }

        var shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        foreach (var menu in _menus)
        {
            foreach (var item in menu.Items)
            {
                if (item.Key != KeyboardKey.Null && item.Shift == shift && Raylib.IsKeyPressed(item.Key))
                {
                    _open = -1;
                    item.Execute();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Mouse handling: a click on a title opens or closes its menu, moving over another title
    /// switches to it, a click on an entry runs it, and a click anywhere else closes the menu.
    /// </summary>
    public void Update()
    {
        var mouse = Raylib.GetMousePosition();
        var clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
        var title = TitleAt(mouse);

        if (title >= 0)
        {
            if (clicked)
            {
                _open = _open == title ? -1 : title;
            }
            else if (_open >= 0)
            {
                _open = title;
            }

            return;
        }

        if (_open < 0 || !clicked)
        {
            return;
        }

        var item = ItemAt(mouse);
        _open = -1;
        if (item is { IsSeparator: false })
        {
            item.Execute();
        }
    }

    public void Draw(int windowWidth)
    {
        Raylib.DrawRectangle(0, 0, windowWidth, Height, Bar);
        DrawStripes(windowWidth);

        for (var i = 0; i < _menus.Length; i++)
        {
            var open = i == _open;
            if (open)
            {
                Raylib.DrawRectangle(_titleX[i], 0, _titleWidth[i], Height, Highlight);
            }

            Raylib.DrawText(_menus[i].Title, _titleX[i] + TitlePadding, TextTop, FontSize, open ? PanelText : BarText);
        }

        if (_open >= 0)
        {
            DrawPanel(_open, Raylib.GetMousePosition());
        }
    }

    private void DrawPanel(int index, Vector2 mouse)
    {
        var menu = _menus[index];
        var x = _titleX[index];
        var width = _panelWidth[index];
        var height = PanelHeight(menu);

        Raylib.DrawRectangle(x, Height, width, height, Panel);
        Raylib.DrawRectangleLines(x, Height, width, height, Color.Black);

        var hovered = ItemAt(mouse);
        var y = Height;
        foreach (var item in menu.Items)
        {
            if (item.IsSeparator)
            {
                Raylib.DrawLine(x + ItemPadding, y + (SeparatorHeight / 2), x + width - ItemPadding, y + (SeparatorHeight / 2), Color.Gray);
                y += SeparatorHeight;
                continue;
            }

            if (ReferenceEquals(item, hovered))
            {
                Raylib.DrawRectangle(x + 1, y, width - 2, ItemHeight, Highlight);
            }

            var textY = y + ((ItemHeight - FontSize) / 2);
            if (item.IsChecked?.Invoke() == true)
            {
                DrawCheck(x + ItemPadding, y);
            }

            Raylib.DrawText(item.Label, x + ItemPadding + CheckWidth, textY, FontSize, PanelText);
            var shortcutWidth = Raylib.MeasureText(item.Shortcut, FontSize);
            Raylib.DrawText(item.Shortcut, x + width - ItemPadding - shortcutWidth, textY, FontSize, Color.DarkGray);
            y += ItemHeight;
        }
    }

    /// <summary>A tick drawn with lines: Raylib's default font has no check mark.</summary>
    private static void DrawCheck(int x, int y)
    {
        var baseline = y + (ItemHeight / 2);
        Raylib.DrawLineEx(new Vector2(x + 2, baseline), new Vector2(x + 6, baseline + 5), 2, PanelText);
        Raylib.DrawLineEx(new Vector2(x + 6, baseline + 5), new Vector2(x + 14, baseline - 6), 2, PanelText);
    }

    /// <summary>The logo's stripes, slanted, at the right of the bar.</summary>
    private static void DrawStripes(int windowWidth)
    {
        const int StripeWidth = 14;
        var x = windowWidth - (Stripes.Length * StripeWidth) - 20;
        foreach (var color in Stripes)
        {
            Raylib.DrawTriangle(
                new Vector2(x + StripeWidth, 0), new Vector2(x, Height), new Vector2(x + StripeWidth, Height), color);
            Raylib.DrawTriangle(
                new Vector2(x + StripeWidth, 0), new Vector2(x + StripeWidth, Height), new Vector2(x + (2 * StripeWidth), 0), color);
            x += StripeWidth;
        }
    }

    private int TitleAt(Vector2 mouse)
    {
        if (mouse.Y < 0 || mouse.Y >= Height)
        {
            return -1;
        }

        for (var i = 0; i < _menus.Length; i++)
        {
            if (mouse.X >= _titleX[i] && mouse.X < _titleX[i] + _titleWidth[i])
            {
                return i;
            }
        }

        return -1;
    }

    private MenuItem? ItemAt(Vector2 mouse)
    {
        if (_open < 0 || mouse.X < _titleX[_open] || mouse.X >= _titleX[_open] + _panelWidth[_open])
        {
            return null;
        }

        var y = Height;
        foreach (var item in _menus[_open].Items)
        {
            var height = item.IsSeparator ? SeparatorHeight : ItemHeight;
            if (mouse.Y >= y && mouse.Y < y + height)
            {
                return item;
            }

            y += height;
        }

        return null;
    }

    private static int PanelHeight(Menu menu)
    {
        var height = 0;
        foreach (var item in menu.Items)
        {
            height += item.IsSeparator ? SeparatorHeight : ItemHeight;
        }

        return height;
    }
}
