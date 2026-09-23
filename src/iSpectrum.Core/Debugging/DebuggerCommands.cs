// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Debugging;

/// <summary>
/// The debugger's command line: short text commands, with addresses written as numbers or
/// symbols. Also keeps what the views show (memory and disassembly addresses) and describes
/// stops in words, with symbol names.
/// </summary>
public sealed class DebuggerCommands(Debugger debugger)
{
    public const string Help =
        "s step  n next  u out  c continue  p pause  b ADDR breakpoint  w ADDR watch writes  " +
        "pr/pw PORT watch port  clear  m ADDR memory  d [ADDR] disassembly";

    /// <summary>The symbols used to read and name addresses (empty when none are loaded).</summary>
    public SymbolTable Symbols { get; set; } = new();

    /// <summary>The first address the memory view shows.</summary>
    public ushort MemoryAddress { get; private set; } = 0x4000;

    /// <summary>The first address the disassembly shows, or null to follow PC.</summary>
    public ushort? DisassemblyAddress { get; private set; }

    /// <summary>Runs one command line and returns what to tell the user.</summary>
    public string Execute(string line)
    {
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var argument = parts.Length > 1 ? parts[1] : null;
        switch (parts[0].ToLowerInvariant())
        {
            case "s" or "step":
                debugger.StepInto();
                return string.Empty;
            case "n" or "next":
                debugger.StepOver();
                return string.Empty;
            case "u" or "out":
                debugger.StepOut();
                return string.Empty;
            case "c" or "continue":
                debugger.Resume();
                return string.Empty;
            case "p" or "pause":
                debugger.Pause();
                return string.Empty;
            case "b" or "break":
                return WithAddress(argument, address =>
                {
                    debugger.ToggleBreakpoint(address);
                    return $"Breakpoint {(debugger.IsBreakpoint(address) ? "set" : "removed")} at {Name(address)}";
                });
            case "w" or "watch":
                return WithAddress(argument, address =>
                {
                    debugger.WatchWrites(address, !debugger.IsWatchingWrites(address));
                    return $"Writes to {Name(address)} {(debugger.IsWatchingWrites(address) ? "watched" : "no longer watched")}";
                });
            case "pr" or "pw":
                return WithAddress(argument, port =>
                {
                    // A port up to $FF matches on its low byte: the ULA answers every even port.
                    var mask = port <= 0xFF ? (ushort)0x00FF : (ushort)0xFFFF;
                    var reads = parts[0] == "pr";
                    debugger.WatchPort(new PortWatch(mask, port, Reads: reads, Writes: !reads));
                    return $"Port {(mask == 0xFF ? $"$xx{port:X2}" : $"${port:X4}")} {(reads ? "reads" : "writes")} watched";
                });
            case "clear":
                debugger.ClearAll();
                return "All breakpoints and watches removed";
            case "m" or "mem":
                return WithAddress(argument, address =>
                {
                    MemoryAddress = address;
                    return string.Empty;
                });
            case "d" or "dis":
                if (argument is null)
                {
                    DisassemblyAddress = null;
                    return "Disassembly follows PC";
                }

                return WithAddress(argument, address =>
                {
                    DisassemblyAddress = address;
                    return string.Empty;
                });
            case "?" or "h" or "help":
                return Help;
            default:
                return $"Unknown command '{parts[0]}' (? for help)";
        }
    }

    /// <summary>The symbol for an address, or the address in hexadecimal.</summary>
    public string Name(ushort address) => Symbols.NameAt(address) ?? $"${address:X4}";

    /// <summary>A stop in words, for the status line.</summary>
    public string Describe(DebugStop stop) => stop.Kind switch
    {
        DebugStopKind.Paused => "PAUSED",
        DebugStopKind.Stepped => "PAUSED",
        DebugStopKind.Breakpoint => $"BREAK AT {Name(stop.Address)}",
        DebugStopKind.MemoryWrite => $"WRITE ${stop.Value:X2} -> {Name(stop.Address)} BY {Name(stop.InstructionAddress)}",
        DebugStopKind.PortRead => $"IN ${stop.Value:X2} <- ${stop.Address:X4} BY {Name(stop.InstructionAddress)}",
        _ => $"OUT ${stop.Value:X2} -> ${stop.Address:X4} BY {Name(stop.InstructionAddress)}",
    };

    private string WithAddress(string? argument, Func<ushort, string> action)
    {
        if (argument is null)
        {
            return "An address is needed";
        }

        return Symbols.TryParseAddress(argument, out var address)
            ? action(address)
            : $"Unknown address '{argument}'";
    }
}
