// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Snapshots;

namespace iSpectrum.Core.Tests.Snapshots;

public class Z80FormatTests
{
    private static Spectrum48 NewMachine() => new(new byte[Memory48K.RomSize]);

    [Theory]
    [InlineData(new byte[] { 0xED, 0xED, 0x05, 0xAA }, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA })]
    [InlineData(new byte[] { 0xED, 0xED, 0x02, 0xED }, new byte[] { 0xED, 0xED })]
    [InlineData(new byte[] { 0xED, 0x00, 0x12 }, new byte[] { 0xED, 0x00, 0x12 })]
    [InlineData(new byte[] { 0x01, 0xED, 0xED, 0x03, 0x00, 0x02 }, new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02 })]
    public void Decompress_ExpandsRunsAndCopiesEverythingElse(byte[] compressed, byte[] expected)
    {
        var output = new byte[expected.Length];

        Z80Format.Decompress(compressed, output);

        Assert.Equal(expected, output);
    }

    [Fact]
    public void Decompress_RejectsDataThatEndsTooEarly() =>
        Assert.Throws<InvalidDataException>(() => Z80Format.Decompress([0x01, 0x02], new byte[3]));

    [Fact]
    public void Decompress_RejectsARunPastTheEnd() =>
        Assert.Throws<InvalidDataException>(() => Z80Format.Decompress([0xED, 0xED, 0x09, 0x00], new byte[4]));

    [Fact]
    public void Version1Header_IsDecodedField_ByField()
    {
        // Written by hand from the format description, uncompressed.
        byte[] header =
        [
            0x24, 0x13,             // A, F
            0xDD, 0xEE,             // BC = EEDD
            0x99, 0xAA,             // HL = AA99
            0x34, 0x12,             // PC = 1234
            0x00, 0x80,             // SP = 8000
            0x3F,                   // I
            0x42,                   // R (low 7 bits)
            0x01 | (6 << 1),        // R bit 7, border yellow, not compressed
            0xBB, 0xCC,             // DE = CCBB
            0x55, 0x66,             // BC' = 6655
            0x33, 0x44,             // DE' = 4433
            0x11, 0x22,             // HL' = 2211
            0x88, 0x77,             // A', F'
            0x01, 0x5C,             // IY = 5C01
            0x02, 0x60,             // IX = 6002
            0x01, 0x00,             // IFF1 on, IFF2 off
            0x02,                   // IM 2
        ];
        var file = new byte[30 + 0xC000];
        header.CopyTo(file, 0);
        file[30] = 0xAB;
        var spectrum = NewMachine();

        Z80Format.Load(spectrum, file);

        var cpu = spectrum.Cpu;
        Assert.Equal((0x24, 0x13), (cpu.A, cpu.F));
        Assert.Equal((0xEE, 0xDD), (cpu.B, cpu.C));
        Assert.Equal((0xAA, 0x99), (cpu.H, cpu.L));
        Assert.Equal(0x1234, cpu.PC);
        Assert.Equal(0x8000, cpu.SP);
        Assert.Equal(0x3F, cpu.I);
        Assert.Equal(0xC2, cpu.R);
        Assert.Equal(6, spectrum.Ula.Border);
        Assert.Equal((0xCC, 0xBB), (cpu.D, cpu.E));
        Assert.Equal((0x66, 0x55), (cpu.B_, cpu.C_));
        Assert.Equal((0x44, 0x33), (cpu.D_, cpu.E_));
        Assert.Equal((0x22, 0x11), (cpu.H_, cpu.L_));
        Assert.Equal((0x88, 0x77), (cpu.A_, cpu.F_));
        Assert.Equal(0x5C01, cpu.IY);
        Assert.Equal(0x6002, cpu.IX);
        Assert.True(cpu.IFF1);
        Assert.False(cpu.IFF2);
        Assert.Equal(2, cpu.IM);
        Assert.Equal(0xAB, spectrum.Memory.Read(0x4000));
    }

    [Fact]
    public void Version1_TreatsFlagByte255AsOne()
    {
        var file = new byte[30 + 0xC000];
        file[6] = 0x01; // PC != 0: version 1
        file[12] = 0xFF;
        var spectrum = NewMachine();

        Z80Format.Load(spectrum, file);

        Assert.Equal(0x80, spectrum.Cpu.R);
        Assert.Equal(0, spectrum.Ula.Border);
    }

    [Theory]
    [InlineData(2, 3)] // 128K in version 2, on a 48K
    [InlineData(3, 4)] // 128K in version 3, on a 48K
    [InlineData(3, 7)] // +3, on no model
    public void Version2And3_RefuseHardwareThatIsNotTheMachines(int version, byte hardware)
    {
        var file = new byte[32 + 54];
        file[30] = version == 2 ? (byte)23 : (byte)54;
        file[34] = hardware;

        Assert.Throws<NotSupportedException>(() => Z80Format.Load(NewMachine(), file));
    }

    [Fact]
    public void Version3_RequiresTheThreeRamPages()
    {
        var file = new byte[32 + 54];
        file[30] = 54;

        Assert.Throws<InvalidDataException>(() => Z80Format.Load(NewMachine(), file));
    }

    [Fact]
    public void Compressor_AndDecompressor_AgreeOnTrickyData()
    {
        // Runs of ED, a lone ED followed by a run, short and long runs.
        byte[] data = [0xED, 0xED, 0x01, 0xED, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x07, 0x07, .. new byte[300]];
        var output = new byte[data.Length];

        Z80Format.Decompress(Z80FileBuilder.Compress(data), output);

        Assert.Equal(data, output);
    }
}
