// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Debugging;

namespace iSpectrum.Core.Tests.Debugging;

public class DebuggerCommandsTests
{
    private readonly Spectrum48 _spectrum = new(new byte[Memory48K.RomSize]);
    private readonly Debugger _debugger;
    private readonly DebuggerCommands _commands;

    public DebuggerCommandsTests()
    {
        _debugger = new Debugger(_spectrum);
        _commands = new DebuggerCommands(_debugger)
        {
            Symbols = SymbolTable.Parse("start: EQU 0x00008000\nscore: EQU 0x00009000\n"),
        };
    }

    [Fact]
    public void Breakpoint_TakesASymbol_AndTogglesOnTheSecondTime()
    {
        Assert.Equal("Breakpoint set at start", _commands.Execute("b start"));
        Assert.True(_debugger.IsBreakpoint(0x8000));

        Assert.Equal("Breakpoint removed at start", _commands.Execute("b $8000"));
        Assert.False(_debugger.IsBreakpoint(0x8000));
    }

    [Fact]
    public void Watch_WatchesWritesToAnAddressPlusAnOffset()
    {
        Assert.Equal("Writes to $9001 watched", _commands.Execute("w score+1"));
        Assert.True(_debugger.IsWatchingWrites(0x9001));
    }

    [Fact]
    public void PortWatch_OnAByte_MatchesTheLowByteOnly()
    {
        _commands.Execute("pw $FE");

        Assert.Equal(new PortWatch(0x00FF, 0x00FE, Reads: false, Writes: true), _debugger.PortWatches.Single());
    }

    [Fact]
    public void Views_MoveToTheGivenAddress_AndDisassemblyCanFollowPcAgain()
    {
        _commands.Execute("m score");
        _commands.Execute("d start");
        Assert.Equal(0x9000, _commands.MemoryAddress);
        Assert.Equal((ushort)0x8000, _commands.DisassemblyAddress);

        _commands.Execute("d");
        Assert.Null(_commands.DisassemblyAddress);
    }

    [Fact]
    public void StepCommands_DriveTheDebugger()
    {
        _commands.Execute("p");
        Assert.True(_debugger.IsPaused);

        _commands.Execute("s");
        Assert.Equal(1, _spectrum.Cpu.PC); // a NOP of the empty ROM

        _commands.Execute("c");
        Assert.False(_debugger.IsPaused);
    }

    [Theory]
    [InlineData("b nowhere", "Unknown address 'nowhere'")]
    [InlineData("b", "An address is needed")]
    [InlineData("jump", "Unknown command 'jump' (? for help)")]
    public void Mistakes_AreExplained(string line, string expected) =>
        Assert.Equal(expected, _commands.Execute(line));

    [Fact]
    public void Stops_AreDescribedWithSymbols()
    {
        var stop = new DebugStop(DebugStopKind.MemoryWrite, Address: 0x9000, Value: 0x2A, InstructionAddress: 0x8000);

        Assert.Equal("WRITE $2A -> score BY start", _commands.Describe(stop));
    }
}
