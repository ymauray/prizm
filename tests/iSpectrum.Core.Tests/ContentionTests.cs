// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class ContentionTests
{
    private const int First = Contention48K.FirstContendedTState;

    [Theory]
    [InlineData(First - 1, 0)]
    [InlineData(First, 6)]
    [InlineData(First + 1, 5)]
    [InlineData(First + 5, 1)]
    [InlineData(First + 6, 0)]
    [InlineData(First + 7, 0)]
    [InlineData(First + 8, 6)]
    [InlineData(First + 127, 0)]
    [InlineData(First + 128, 0)] // right border and retrace: no contention
    [InlineData(First + 223, 0)]
    [InlineData(First + 224, 6)] // next line
    [InlineData(First + (191 * 224), 6)] // last screen line
    [InlineData(First + (192 * 224), 0)] // bottom border
    public void Delay_FollowsThe65432100Pattern_DuringScreenLines(int tStates, int expected) =>
        Assert.Equal(expected, Contention48K.Delay(tStates));

    [Theory]
    [InlineData(0x3FFF, false)]
    [InlineData(0x4000, true)]
    [InlineData(0x7FFF, true)]
    [InlineData(0x8000, false)]
    public void OnlyTheLowerRam_IsContended(int address, bool expected) =>
        Assert.Equal(expected, Contention48K.IsContended((ushort)address));

    [Theory]
    [InlineData(0x6000, 6 + 4)] // the fetch waits 6 T-states, then takes 4
    [InlineData(0x8000, 4)]
    public void Nop_TakesLonger_InContendedMemory_WhileTheScreenIsDrawn(int address, int expected)
    {
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]);
        spectrum.Memory.Write((ushort)address, 0x00); // NOP
        spectrum.Cpu.PC = (ushort)address;
        spectrum.Cpu.TStates = First;

        spectrum.Cpu.Step();

        Assert.Equal(First + expected, spectrum.Cpu.TStates);
    }

    [Fact]
    public void OutToTheUla_WaitsAtTheContentionPointOfItsIoCycle()
    {
        // OUT (0xFE),A from uncontended memory: fetch 4, operand 3, then the I/O cycle: 1, then a
        // contention point + 3. Starting 8 T-states early puts that point on the first contended
        // T-state: 6 more.
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]);
        spectrum.Memory.Write(0x8000, 0xD3);
        spectrum.Memory.Write(0x8001, 0xFE);
        spectrum.Cpu.PC = 0x8000;
        spectrum.Cpu.TStates = First - 8;

        spectrum.Cpu.Step();

        Assert.Equal(First - 8 + 11 + 6, spectrum.Cpu.TStates);
    }

    [Fact]
    public void FullFrame_OfNopsInContendedMemory_RunsFewerInstructions()
    {
        Assert.True(CountNopsInOneFrame(0x6000) < CountNopsInOneFrame(0x8000));
    }

    private static int CountNopsInOneFrame(ushort start)
    {
        // NOPs fill 16 KB from start; the count of fetched opcodes shows the time lost to the ULA.
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]);
        spectrum.Cpu.PC = start;
        var count = 0;
        while (spectrum.Cpu.TStates < Spectrum48.FrameTStates)
        {
            spectrum.Cpu.Step();
            count++;
        }

        return count;
    }
}
