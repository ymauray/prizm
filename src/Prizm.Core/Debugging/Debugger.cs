// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Z80;

namespace Prizm.Core.Debugging;

/// <summary>Why the debugger stopped the machine.</summary>
public enum DebugStopKind
{
    /// <summary>The user paused it.</summary>
    Paused,

    /// <summary>A single step, a step over or a step out has finished.</summary>
    Stepped,

    /// <summary>PC reached an execution breakpoint (the instruction there has not run yet).</summary>
    Breakpoint,

    /// <summary>An instruction wrote to a watched address (it has run).</summary>
    MemoryWrite,

    /// <summary>An instruction read a watched port (it has run).</summary>
    PortRead,

    /// <summary>An instruction wrote to a watched port (it has run).</summary>
    PortWrite,
}

/// <summary>
/// A stop, with what caused it: for watchpoints, the address or port, the value, and the address
/// of the instruction that made the access.
/// </summary>
public readonly record struct DebugStop(DebugStopKind Kind, ushort Address = 0, byte Value = 0, ushort InstructionAddress = 0);

/// <summary>A port watchpoint: ports with (port &amp; Mask) == Value, on reads, writes or both.</summary>
public readonly record struct PortWatch(ushort Mask, ushort Value, bool Reads, bool Writes);

/// <summary>
/// Pauses a machine, steps it one instruction at a time, and stops it on breakpoints (PC reaches
/// an address) and watchpoints (a write to an address, an access to a port).
/// </summary>
/// <remarks>
/// While attached, the debugger runs the machine instead of the front-end: <see cref="RunFrame"/>
/// replaces <see cref="Spectrum.RunFrame"/>. Step over and step out do not block: they set a
/// temporary stop and let the machine run, frame after frame, until it is reached.
/// </remarks>
public sealed class Debugger : IBusWatch
{
    private const int AddressSpace = 0x10000;

    private Spectrum _machine;
    private readonly bool[] _breakpoints = new bool[AddressSpace];
    private readonly bool[] _watchedWrites = new bool[AddressSpace];
    private readonly List<PortWatch> _portWatches = [];

    /// <summary>A watchpoint hit during the instruction being run, if any.</summary>
    private DebugStop? _hit;

    /// <summary>Set when resuming at a breakpoint, so that the machine can leave it.</summary>
    private bool _leavingBreakpoint;

    /// <summary>Step over: stop when PC reaches this address with the stack back at this level.</summary>
    private (ushort Address, ushort StackPointer)? _runUntil;

    /// <summary>Step out: stop after a return that brings SP above this level.</summary>
    private ushort? _returnAbove;

    /// <summary>Address of the instruction being run, for watchpoint reports.</summary>
    private ushort _instructionAddress;

    public Debugger(Spectrum machine)
    {
        _machine = machine;
        machine.Watch(this);
    }

    public Spectrum Machine => _machine;

    public bool IsPaused { get; private set; }

    /// <summary>Why the machine last stopped.</summary>
    public DebugStop LastStop { get; private set; }

    /// <summary>The execution breakpoints, in address order.</summary>
    public IEnumerable<ushort> Breakpoints => Addresses(_breakpoints);

    /// <summary>The watched write addresses, in address order.</summary>
    public IEnumerable<ushort> WatchedWrites => Addresses(_watchedWrites);

    public IReadOnlyList<PortWatch> PortWatches => _portWatches;

    /// <summary>Stops telling the machine's bus about this debugger.</summary>
    public void Detach() => _machine.Watch(null);

    /// <summary>
    /// Moves to another machine (after a reset or a reload), keeping the breakpoints and
    /// watchpoints; the new machine starts running.
    /// </summary>
    public void Attach(Spectrum machine)
    {
        _machine.Watch(null);
        _machine = machine;
        machine.Watch(this);
        IsPaused = false;
        _runUntil = null;
        _returnAbove = null;
        _hit = null;
        _leavingBreakpoint = false;
    }

    public bool IsBreakpoint(ushort address) => _breakpoints[address];

    public void SetBreakpoint(ushort address, bool enabled = true) => _breakpoints[address] = enabled;

    public void ToggleBreakpoint(ushort address) => _breakpoints[address] = !_breakpoints[address];

    /// <summary>Stops the machine after any instruction writes to <paramref name="address"/> (even in ROM).</summary>
    public void WatchWrites(ushort address, bool enabled = true) => _watchedWrites[address] = enabled;

    public bool IsWatchingWrites(ushort address) => _watchedWrites[address];

    public void WatchPort(PortWatch watch) => _portWatches.Add(watch);

    /// <summary>Removes every breakpoint and watchpoint.</summary>
    public void ClearAll()
    {
        Array.Clear(_breakpoints);
        Array.Clear(_watchedWrites);
        _portWatches.Clear();
    }

    public void Pause() => Stop(new DebugStop(DebugStopKind.Paused));

    public void Resume()
    {
        IsPaused = false;
        _leavingBreakpoint = _breakpoints[_machine.Cpu.PC];
    }

    /// <summary>
    /// Runs the rest of the frame, stopping early on a breakpoint, a watchpoint or the end of a
    /// step over or step out. Does nothing while paused.
    /// </summary>
    public void RunFrame()
    {
        while (!IsPaused)
        {
            var cpu = _machine.Cpu;
            if (_runUntil is { } target && cpu.PC == target.Address && cpu.SP >= target.StackPointer)
            {
                Stop(new DebugStop(DebugStopKind.Stepped));
                return;
            }

            if (_breakpoints[cpu.PC] && !_leavingBreakpoint)
            {
                Stop(new DebugStop(DebugStopKind.Breakpoint, cpu.PC, InstructionAddress: cpu.PC));
                return;
            }

            _leavingBreakpoint = false;
            var returning = _returnAbove is not null && IsReturn(cpu.PC);
            var frameEnded = RunInstruction();

            if (_hit is { } hit)
            {
                Stop(hit);
                return;
            }

            if (returning && cpu.SP > _returnAbove)
            {
                Stop(new DebugStop(DebugStopKind.Stepped));
                return;
            }

            if (frameEnded)
            {
                return;
            }
        }
    }

    /// <summary>Runs one instruction; the machine stays paused.</summary>
    public void StepInto()
    {
        if (!IsPaused)
        {
            return;
        }

        RunInstruction();
        Stop(_hit ?? new DebugStop(DebugStopKind.Stepped));
    }

    /// <summary>
    /// Runs a CALL, RST, DJNZ, repeating block instruction or HALT to its end, as one step: the
    /// machine runs until PC reaches the next instruction with the stack back at the same level.
    /// Any other instruction is a single step.
    /// </summary>
    public void StepOver()
    {
        if (!IsPaused)
        {
            return;
        }

        var cpu = _machine.Cpu;
        var instruction = Disassemble(cpu.PC);
        if (!RunsToTheNextInstruction(instruction.Text))
        {
            StepInto();
            return;
        }

        _runUntil = ((ushort)(cpu.PC + instruction.Length), cpu.SP);
        Resume();
    }

    /// <summary>Runs until the current routine returns to its caller.</summary>
    public void StepOut()
    {
        if (!IsPaused)
        {
            return;
        }

        _returnAbove = _machine.Cpu.SP;
        Resume();
    }

    public DisassembledInstruction Disassemble(ushort address, Func<ushort, string?>? symbol = null) =>
        Z80Disassembler.Disassemble(_machine.Memory.Read, address, symbol);

    void IBusWatch.OnMemoryWrite(ushort address, byte value)
    {
        if (_watchedWrites[address])
        {
            _hit ??= new DebugStop(DebugStopKind.MemoryWrite, address, value, _instructionAddress);
        }
    }

    void IBusWatch.OnPortRead(ushort port, byte value) => CheckPort(port, value, write: false);

    void IBusWatch.OnPortWrite(ushort port, byte value) => CheckPort(port, value, write: true);

    private bool RunInstruction()
    {
        _hit = null;
        _instructionAddress = _machine.Cpu.PC;
        return _machine.Step();
    }

    private void CheckPort(ushort port, byte value, bool write)
    {
        foreach (var watch in _portWatches)
        {
            if ((port & watch.Mask) == watch.Value && (write ? watch.Writes : watch.Reads))
            {
                var kind = write ? DebugStopKind.PortWrite : DebugStopKind.PortRead;
                _hit ??= new DebugStop(kind, port, value, _instructionAddress);
                return;
            }
        }
    }

    private void Stop(DebugStop stop)
    {
        IsPaused = true;
        LastStop = stop;
        _runUntil = null;
        _returnAbove = null;
        _hit = null;
    }

    /// <summary>RET, RET cc, RETI or RETN (DD/FD prefixes skipped).</summary>
    private bool IsReturn(ushort address)
    {
        var memory = _machine.Memory;
        var opcode = memory.Read(address);
        while (opcode is 0xDD or 0xFD)
        {
            opcode = memory.Read(++address);
        }

        if (opcode == 0xED)
        {
            // RETN and RETI: ED 45, 4D, 55, 5D, 65, 6D, 75, 7D.
            return (memory.Read((ushort)(address + 1)) & 0xC7) == 0x45;
        }

        return opcode == 0xC9 || (opcode & 0xC7) == 0xC0;
    }

    /// <summary>Instructions that step over runs to their end, by their mnemonic.</summary>
    private static readonly HashSet<string> RunToTheEnd =
        ["CALL", "RST", "DJNZ", "HALT", "LDIR", "LDDR", "CPIR", "CPDR", "INIR", "INDR", "OTIR", "OTDR"];

    private static bool RunsToTheNextInstruction(string text)
    {
        var end = text.IndexOf(' ');
        return RunToTheEnd.Contains(end < 0 ? text : text[..end]);
    }

    private static IEnumerable<ushort> Addresses(bool[] set)
    {
        for (var address = 0; address < set.Length; address++)
        {
            if (set[address])
            {
                yield return (ushort)address;
            }
        }
    }
}
