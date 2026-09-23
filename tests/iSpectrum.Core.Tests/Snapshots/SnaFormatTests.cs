// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Snapshots;

namespace iSpectrum.Core.Tests.Snapshots;

public class SnaFormatTests
{
    private static Spectrum48 NewMachine() => new(new byte[Memory48K.RomSize]);

    /// <summary>A .SNA file with a distinct value in every register, written by hand from the format description.</summary>
    private static byte[] SampleFile()
    {
        var data = new byte[SnaFormat.FileLength];
        byte[] header =
        [
            0x3F,                   // I
            0x11, 0x22,             // HL' = 2211
            0x33, 0x44,             // DE' = 4433
            0x55, 0x66,             // BC' = 6655
            0x77, 0x88,             // AF' = 8877
            0x99, 0xAA,             // HL = AA99
            0xBB, 0xCC,             // DE = CCBB
            0xDD, 0xEE,             // BC = EEDD
            0x01, 0x5C,             // IY = 5C01
            0x02, 0x60,             // IX = 6002
            0x04,                   // IFF2 set
            0x42,                   // R
            0x13, 0x24,             // AF = 2413
            0x00, 0x80,             // SP = 8000
            0x01,                   // IM 1
            0x05,                   // border cyan
        ];
        header.CopyTo(data, 0);

        // PC = 0x1234 on the stack, at 0x8000 (RAM offset 0x4000).
        data[SnaFormat.HeaderLength + 0x4000] = 0x34;
        data[SnaFormat.HeaderLength + 0x4001] = 0x12;
        data[SnaFormat.HeaderLength] = 0xAB; // first RAM byte, at 0x4000
        return data;
    }

    [Fact]
    public void Load_RestoresRegistersBorderAndRam_AndPopsPc()
    {
        var spectrum = NewMachine();

        SnaFormat.Load(spectrum, SampleFile());

        var cpu = spectrum.Cpu;
        Assert.Equal(0x3F, cpu.I);
        Assert.Equal((0x22, 0x11), (cpu.H_, cpu.L_));
        Assert.Equal((0x44, 0x33), (cpu.D_, cpu.E_));
        Assert.Equal((0x66, 0x55), (cpu.B_, cpu.C_));
        Assert.Equal((0x88, 0x77), (cpu.A_, cpu.F_));
        Assert.Equal((0xAA, 0x99), (cpu.H, cpu.L));
        Assert.Equal((0xCC, 0xBB), (cpu.D, cpu.E));
        Assert.Equal((0xEE, 0xDD), (cpu.B, cpu.C));
        Assert.Equal(0x5C01, cpu.IY);
        Assert.Equal(0x6002, cpu.IX);
        Assert.True(cpu.IFF1);
        Assert.True(cpu.IFF2);
        Assert.Equal(0x42, cpu.R);
        Assert.Equal((0x24, 0x13), (cpu.A, cpu.F));
        Assert.Equal(0x1234, cpu.PC);
        Assert.Equal(0x8002, cpu.SP);
        Assert.Equal(1, cpu.IM);
        Assert.Equal(5, spectrum.Ula.Border);
        Assert.Equal(0xAB, spectrum.Memory.Read(0x4000));
    }

    [Fact]
    public void Save_AfterLoad_GivesBackTheSameFile()
    {
        var file = SampleFile();
        var spectrum = NewMachine();
        SnaFormat.Load(spectrum, file);

        Assert.Equal(file, SnaFormat.Save(spectrum));
    }

    [Fact]
    public void Save_DoesNotChangeTheRunningMachine()
    {
        var spectrum = NewMachine();
        SnaFormat.Load(spectrum, SampleFile());
        var before = spectrum.Memory.Contents.ToArray();

        SnaFormat.Save(spectrum);

        Assert.Equal(0x8002, spectrum.Cpu.SP);
        Assert.Equal(before, spectrum.Memory.Contents.ToArray());
    }

    [Theory]
    [InlineData(0x4001)]
    [InlineData(0x0001)]
    public void Save_Fails_WhenPcCannotBePushedInRam(int sp)
    {
        var spectrum = NewMachine();
        spectrum.Cpu.SP = (ushort)sp;

        Assert.Throws<InvalidOperationException>(() => SnaFormat.Save(spectrum));
    }

    [Fact]
    public void Load_RejectsAFileOfTheWrongLength() =>
        Assert.Throws<InvalidDataException>(() => SnaFormat.Load(NewMachine(), new byte[SnaFormat.FileLength + 1]));
}
