// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using System.Text;
using Prizm.Core.Debugging;
using Raylib_cs;

namespace Prizm.App;

/// <summary>
/// The debugger on screen, in the Spectrum's font: status, registers, disassembly and
/// breakpoints in a column on the right; memory and the command line under the picture.
/// A click on a disassembly line toggles its breakpoint; while the machine is paused, the
/// keyboard types commands (see <see cref="DebuggerCommands"/>).
/// </summary>
internal sealed class DebuggerPanel(Debugger debugger, DebuggerCommands commands, SpectrumFont font)
{
    /// <summary>Glyphs drawn twice their size: 16 pixels, 40 characters per 640-pixel column.</summary>
    public const int Scale = 2;

    public const int CharWidth = SpectrumFont.GlyphSize * Scale;
    public const int Columns = 40;
    public const int ColumnWidth = Columns * CharWidth;

    private const int LineHeight = CharWidth + 2;
    private const int MemoryBytesPerLine = 8;

    private static readonly Color Background = new(24, 24, 24, 255);
    private static readonly Color Text = new(215, 215, 215, 255);
    private static readonly Color Dim = new(110, 110, 110, 255);
    private static readonly Color Heading = new(0, 215, 0, 255);
    private static readonly Color Label = new(215, 215, 0, 255);
    private static readonly Color Breakpoint = new(255, 60, 60, 255);
    private static readonly Color CurrentLine = new(0, 110, 110, 255);
    private static readonly Color Bright = Color.White;

    /// <summary>Screen rows of the disassembly lines drawn last, to find the one clicked.</summary>
    private readonly List<(int Top, ushort Address)> _instructionRows = [];

    private readonly StringBuilder _commandLine = new();

    /// <summary>Where the column was drawn last, for clicks.</summary>
    private int _columnX;
    private string _message = "? for help";

    /// <summary>Whether the keyboard goes to the command line rather than to the Spectrum.</summary>
    public bool HasKeyboard => debugger.IsPaused;

    public void Update()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var mouse = Raylib.GetMousePosition();
            foreach (var (top, address) in _instructionRows)
            {
                if (mouse.X >= _columnX && mouse.X < _columnX + ColumnWidth && mouse.Y >= top && mouse.Y < top + LineHeight)
                {
                    debugger.ToggleBreakpoint(address);
                    break;
                }
            }
        }

        if (HasKeyboard)
        {
            ReadCommandLine();
        }
    }

    /// <summary>Status, registers, disassembly and breakpoints, in the column at <paramref name="x"/>.</summary>
    public void DrawColumn(int x, int y, int height)
    {
        _columnX = x;
        Raylib.DrawRectangle(x, y, ColumnWidth, height, Background);
        var row = 0;
        var rows = height / LineHeight;

        var status = debugger.IsPaused ? commands.Describe(debugger.LastStop) : "RUNNING";
        Line(x, y, row++, status, debugger.IsPaused ? Breakpoint : Heading);
        row++;

        var cpu = debugger.Machine.Cpu;
        Line(x, y, row++, $"AF {cpu.A:X2}{cpu.F:X2}   AF' {cpu.A_:X2}{cpu.F_:X2}", Text);
        Line(x, y, row++, $"BC {cpu.B:X2}{cpu.C:X2}   BC' {cpu.B_:X2}{cpu.C_:X2}", Text);
        Line(x, y, row++, $"DE {cpu.D:X2}{cpu.E:X2}   DE' {cpu.D_:X2}{cpu.E_:X2}", Text);
        Line(x, y, row++, $"HL {cpu.H:X2}{cpu.L:X2}   HL' {cpu.H_:X2}{cpu.L_:X2}", Text);
        Line(x, y, row++, $"IX {cpu.IX:X4}   IY  {cpu.IY:X4}", Text);
        Line(x, y, row++, $"SP {cpu.SP:X4}   PC  {cpu.PC:X4}", Text);
        Line(x, y, row++, $"I {cpu.I:X2}  R {cpu.R:X2}  IM {cpu.IM}  {(cpu.IFF1 ? "EI" : "DI")}  T {cpu.TStates}", Text);
        DrawFlags(x, y, row++, cpu.F);
        row++;

        Line(x, y, row++, "DISASSEMBLY", Heading);
        var disassemblyRows = rows - row - 5;
        DrawDisassembly(x, y, ref row, row + disassemblyRows, cpu.PC);
        row++;

        Line(x, y, row++, "BREAKPOINTS AND WATCHES", Heading);
        Line(x, y, row++, Join("b", debugger.Breakpoints), Text);
        Line(x, y, row++, Join("w", debugger.WatchedWrites), Text);
        Line(x, y, row, string.Join(" ", debugger.PortWatches.Select(PortText)), Text);
    }

    /// <summary>Memory, then the command line and its answer, in the area at <paramref name="x"/>.</summary>
    public void DrawBottom(int x, int y, int height)
    {
        Raylib.DrawRectangle(x, y, ColumnWidth, height, Background);
        var rows = height / LineHeight;
        var row = 0;

        Line(x, y, row++, $"MEMORY {commands.Name(commands.MemoryAddress)}", Heading);
        var memory = debugger.Machine.Memory;
        for (var address = commands.MemoryAddress; row < rows - 2; row++)
        {
            var hex = new StringBuilder($"{address:X4} ");
            var chars = new StringBuilder(MemoryBytesPerLine);
            for (var i = 0; i < MemoryBytesPerLine; i++, address++)
            {
                var value = memory.Read(address);
                hex.Append($"{value:X2} ");
                chars.Append(value is >= 0x20 and < 0x7F ? (char)value : '.');
            }

            Line(x, y, row, hex.Append(chars).ToString(), Text);
        }

        var prompt = HasKeyboard ? $">{_commandLine}_" : "> (Cmd+P pauses, to type commands)";
        Line(x, y, row++, prompt, HasKeyboard ? Bright : Dim);
        Line(x, y, row, _message, Label);
    }

    private void DrawDisassembly(int x, int y, ref int row, int lastRow, ushort pc)
    {
        _instructionRows.Clear();
        var address = commands.DisassemblyAddress ?? pc;
        var symbols = commands.Symbols;

        while (row < lastRow)
        {
            if (symbols.NameAt(address) is { } name)
            {
                Line(x, y, row++, name + ":", Label);
                if (row >= lastRow)
                {
                    break;
                }
            }

            var instruction = debugger.Disassemble(address, symbols.NameAt);
            var top = y + (row * LineHeight);
            if (address == pc)
            {
                Raylib.DrawRectangle(x, top, ColumnWidth, LineHeight, CurrentLine);
            }

            if (debugger.IsBreakpoint(address))
            {
                font.Draw("*", x, top, Scale, Breakpoint);
            }

            Line(x, y, row, $"  {address:X4} {instruction.Text}", Text);
            _instructionRows.Add((top, address));
            row++;
            address = (ushort)(address + instruction.Length);
        }
    }

    /// <summary>The flags, each letter bright when set.</summary>
    private void DrawFlags(int x, int y, int row, byte flags)
    {
        const string Names = "SZ5H3PNC";
        Line(x, y, row, "F", Text);
        for (var bit = 0; bit < 8; bit++)
        {
            var set = (flags & (0x80 >> bit)) != 0;
            font.Draw(Names.AsSpan(bit, 1), x + ((3 + (bit * 2)) * CharWidth), y + (row * LineHeight), Scale, set ? Bright : Dim);
        }
    }

    private void ReadCommandLine()
    {
        for (var c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed())
        {
            if (c is >= ' ' and < 0x7F && _commandLine.Length < Columns - 2)
            {
                _commandLine.Append((char)c);
            }
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && _commandLine.Length > 0)
        {
            _commandLine.Length--;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _commandLine.Clear();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            _message = commands.Execute(_commandLine.ToString());
            _commandLine.Clear();
        }
    }

    private string Join(string command, IEnumerable<ushort> addresses) =>
        command + " " + string.Join(" ", addresses.Select(commands.Name));

    private static string PortText(PortWatch watch) =>
        (watch.Reads ? "pr " : "pw ") + (watch.Mask == 0xFF ? $"$xx{watch.Value:X2}" : $"${watch.Value:X4}");

    /// <summary>One line of text, cut to the column's width.</summary>
    private void Line(int x, int y, int row, string text, Color color) =>
        font.Draw(text.AsSpan(0, Math.Min(text.Length, Columns)), x, y + (row * LineHeight), Scale, color);
}
