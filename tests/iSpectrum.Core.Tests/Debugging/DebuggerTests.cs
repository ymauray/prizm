// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Debugging;

namespace iSpectrum.Core.Tests.Debugging;

public class DebuggerTests
{
    private const ushort Main = 0x8000;
    private const ushort AfterCall = 0x8003;
    private const ushort Store = 0x8003;
    private const ushort Output = 0x8006;
    private const ushort Loop = 0x8008;
    private const ushort Routine = 0x8010;
    private const ushort Result = 0x9000;

    private readonly Spectrum48 _spectrum = new(new byte[Memory48K.RomSize]);
    private readonly Debugger _debugger;

    /// <summary>
    /// 8000 CALL 8010 / 8003 LD (9000),A / 8006 OUT (FE),A / 8008 JR 8008,
    /// and the routine: 8010 LD A,2A / 8012 RET. Interrupts stay disabled.
    /// </summary>
    public DebuggerTests()
    {
        byte[] main = [0xCD, 0x10, 0x80, 0x32, 0x00, 0x90, 0xD3, 0xFE, 0x18, 0xFE];
        byte[] routine = [0x3E, 0x2A, 0xC9];
        _spectrum.Memory.LoadRam(Main, main);
        _spectrum.Memory.LoadRam(Routine, routine);
        _spectrum.Cpu.PC = Main;
        _spectrum.Cpu.SP = 0xF000;
        _debugger = new Debugger(_spectrum);
    }

    /// <summary>Runs frames until the debugger stops, as the front-end would.</summary>
    private void RunUntilStopped()
    {
        for (var frame = 0; frame < 10 && !_debugger.IsPaused; frame++)
        {
            _debugger.RunFrame();
        }
    }

    [Fact]
    public void StepInto_FollowsTheCall()
    {
        _debugger.Pause();

        _debugger.StepInto();
        Assert.Equal(Routine, _spectrum.Cpu.PC);
        _debugger.StepInto();
        _debugger.StepInto();

        Assert.Equal(AfterCall, _spectrum.Cpu.PC);
        Assert.Equal(0x2A, _spectrum.Cpu.A);
        Assert.True(_debugger.IsPaused);
    }

    [Fact]
    public void StepOver_RunsTheWholeCall()
    {
        _debugger.Pause();

        _debugger.StepOver();
        RunUntilStopped();

        Assert.Equal(AfterCall, _spectrum.Cpu.PC);
        Assert.Equal(0x2A, _spectrum.Cpu.A);
        Assert.Equal(DebugStopKind.Stepped, _debugger.LastStop.Kind);
    }

    [Fact]
    public void StepOver_IsASingleStep_ForOtherInstructions()
    {
        _spectrum.Cpu.PC = Routine;
        _debugger.Pause();

        _debugger.StepOver();

        Assert.True(_debugger.IsPaused);
        Assert.Equal(Routine + 2, _spectrum.Cpu.PC);
    }

    [Fact]
    public void StepOut_ReturnsToTheCaller()
    {
        _debugger.Pause();
        _debugger.StepInto(); // into the routine

        _debugger.StepOut();
        RunUntilStopped();

        Assert.Equal(AfterCall, _spectrum.Cpu.PC);
        Assert.Equal(0xF000, _spectrum.Cpu.SP);
    }

    [Fact]
    public void Breakpoint_StopsBeforeTheInstruction_AndResumingLeavesIt()
    {
        _debugger.SetBreakpoint(Routine);

        RunUntilStopped();
        Assert.Equal(Routine, _spectrum.Cpu.PC);
        Assert.Equal(DebugStopKind.Breakpoint, _debugger.LastStop.Kind);
        Assert.NotEqual(0x2A, _spectrum.Cpu.A);

        _debugger.Resume();
        _debugger.RunFrame();
        Assert.False(_debugger.IsPaused);
        Assert.Equal(Loop, _spectrum.Cpu.PC);
    }

    [Fact]
    public void WriteWatch_StopsAfterTheWrite_AndSaysWhoWrote()
    {
        _debugger.WatchWrites(Result);

        RunUntilStopped();

        Assert.Equal(new DebugStop(DebugStopKind.MemoryWrite, Result, 0x2A, Store), _debugger.LastStop);
        Assert.Equal(Output, _spectrum.Cpu.PC);
        Assert.Equal(0x2A, _spectrum.Memory.Read(Result));
    }

    [Fact]
    public void PortWatch_StopsOnAWriteToAMatchingPort()
    {
        // Every even port is the ULA: watch the low bit only.
        _debugger.WatchPort(new PortWatch(Mask: 0x0001, Value: 0x0000, Reads: false, Writes: true));

        RunUntilStopped();

        Assert.Equal(DebugStopKind.PortWrite, _debugger.LastStop.Kind);
        Assert.Equal(Output, _debugger.LastStop.InstructionAddress);
        Assert.Equal(Loop, _spectrum.Cpu.PC);
    }

    [Fact]
    public void WithoutBreakpoints_AFrameRunsToItsEnd()
    {
        _debugger.RunFrame();

        Assert.False(_debugger.IsPaused);
        Assert.Equal(Loop, _spectrum.Cpu.PC);
        Assert.InRange(_spectrum.Cpu.TStates, 0, 20);
    }

    [Fact]
    public void Detach_StopsWatching()
    {
        _debugger.WatchWrites(Result);
        _debugger.Detach();

        _spectrum.RunFrame();

        Assert.False(_debugger.IsPaused);
        Assert.Equal(0x2A, _spectrum.Memory.Read(Result));
    }
}
