// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Z80.Tests;

public class InterruptTests
{
    private readonly FlatMemory _memory = new();
    private readonly Z80Cpu _cpu;

    public InterruptTests()
    {
        _cpu = new Z80Cpu(_memory, new NullIo())
        {
            PC = 0x1234,
            SP = 0x8000,
            IFF1 = true,
            IFF2 = true,
            IM = 1,
        };
    }

    [Fact]
    public void Interrupt_IsIgnored_WhenIff1IsReset()
    {
        _cpu.IFF1 = false;

        Assert.False(_cpu.Interrupt());
        Assert.Equal(0x1234, _cpu.PC);
        Assert.Equal(0x8000, _cpu.SP);
        Assert.Equal(0, _cpu.TStates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Im0AndIm1_CallRst38_In13TStates(int mode)
    {
        _cpu.IM = mode;

        Assert.True(_cpu.Interrupt());

        Assert.Equal(0x0038, _cpu.PC);
        Assert.Equal(0x0038, _cpu.WZ);
        Assert.Equal(0x7FFE, _cpu.SP);
        Assert.Equal(0x34, _memory.Read(0x7FFE));
        Assert.Equal(0x12, _memory.Read(0x7FFF));
        Assert.False(_cpu.IFF1);
        Assert.False(_cpu.IFF2);
        Assert.Equal(1, _cpu.R);
        Assert.Equal(13, _cpu.TStates);
    }

    [Fact]
    public void Im2_JumpsThroughTheVectorAtI00FF_In19TStates()
    {
        _cpu.IM = 2;
        _cpu.I = 0x80;
        _memory.Load(0x80FF, 0x00, 0xC0);

        Assert.True(_cpu.Interrupt());

        Assert.Equal(0xC000, _cpu.PC);
        Assert.Equal(0x7FFE, _cpu.SP);
        Assert.Equal(0x34, _memory.Read(0x7FFE));
        Assert.Equal(0x12, _memory.Read(0x7FFF));
        Assert.Equal(19, _cpu.TStates);
    }

    [Fact]
    public void Interrupt_LeavesHalt_AndReturnsAfterTheHaltOpcode()
    {
        _cpu.PC = 0x0000;
        _memory.Load(0x0000, 0x76); // HALT

        _cpu.Step();
        _cpu.Step();
        Assert.True(_cpu.Halted);
        Assert.Equal(0x0000, _cpu.PC);

        Assert.True(_cpu.Interrupt());

        Assert.False(_cpu.Halted);
        Assert.Equal(0x0038, _cpu.PC);
        Assert.Equal(0x01, _memory.Read(0x7FFE));
        Assert.Equal(0x00, _memory.Read(0x7FFF));
    }

    [Fact]
    public void Interrupt_IsDelayedByOneInstructionAfterEi()
    {
        _cpu.IFF1 = _cpu.IFF2 = false;
        _cpu.PC = 0x0000;
        _memory.Load(0x0000, 0xFB, 0xFB, 0x00); // EI; EI; NOP

        _cpu.Step();
        Assert.True(_cpu.IFF1);
        Assert.False(_cpu.Interrupt());

        // A second EI blocks again.
        _cpu.Step();
        Assert.False(_cpu.Interrupt());

        _cpu.Step();
        Assert.True(_cpu.Interrupt());
        Assert.Equal(0x03, _memory.Read(0x7FFE));
    }

    [Fact]
    public void Di_PreventsInterrupts()
    {
        _cpu.PC = 0x0000;
        _memory.Load(0x0000, 0xF3); // DI

        _cpu.Step();

        Assert.False(_cpu.Interrupt());
        Assert.Equal(0x0001, _cpu.PC);
    }
}
