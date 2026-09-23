// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Snapshots;

namespace iSpectrum.Core.Tests.Snapshots;

public class Snapshot128Tests
{
    /// <summary>A 128K whose eight banks each hold their own number, with ROM 1 and a given bank paged.</summary>
    private static Spectrum128 MarkedMachine(int pagedBank)
    {
        var spectrum = TestMachine.New128();
        for (var bank = 0; bank < 8; bank++)
        {
            spectrum.Memory.Bank(bank).Fill((byte)(0x10 + bank));
        }

        spectrum.Memory.WritePaging((byte)(0x10 | pagedBank));
        spectrum.Cpu.PC = 0x1234;
        spectrum.Cpu.SP = 0x8000;
        spectrum.Cpu.IX = 0x5C3A;
        spectrum.Ula.Border = 3;
        return spectrum;
    }

    private static void AssertSameMachine(Spectrum128 expected, Spectrum128 actual)
    {
        for (var bank = 0; bank < 8; bank++)
        {
            Assert.True(expected.Memory.Bank(bank).SequenceEqual(actual.Memory.Bank(bank)), $"Bank {bank} differs.");
        }

        Assert.Equal(expected.Memory.RamPage, actual.Memory.RamPage);
        Assert.Equal(expected.Memory.RomPage, actual.Memory.RomPage);
        Assert.Equal(expected.Cpu.PC, actual.Cpu.PC);
        Assert.Equal(expected.Cpu.SP, actual.Cpu.SP);
        Assert.Equal(expected.Cpu.IX, actual.Cpu.IX);
        Assert.Equal(expected.Ula.Border, actual.Ula.Border);
    }

    [Theory]
    [InlineData(3, SnaFormat.FileLength128)]
    [InlineData(5, SnaFormat.FileLength128WithRepeatedBank)] // the paged bank is saved twice
    public void Sna_SavesEveryBank_AndLoadsThemBack(int pagedBank, int expectedLength)
    {
        var original = MarkedMachine(pagedBank);

        var file = SnaFormat.Save(original);
        var copy = TestMachine.New128();
        SnaFormat.Load(copy, file);

        Assert.Equal(expectedLength, file.Length);
        Assert.True(SnaFormat.IsSpectrum128(file));
        AssertSameMachine(original, copy);
        Assert.Equal(file, SnaFormat.Save(copy));
    }

    [Fact]
    public void Z80Version3_RestoresBanksPagingAndTheAy()
    {
        var original = MarkedMachine(6);
        original.Ay.SelectRegister(0);
        original.Ay.WriteSelected(0x42);
        original.Ay.SelectRegister(8);
        original.Ay.WriteSelected(0x0F);

        var file = Z80FileBuilder.Version3For128(original);
        var copy = TestMachine.New128();
        Z80Format.Load(copy, file);

        Assert.True(Z80Format.IsSpectrum128(file));
        AssertSameMachine(original, copy);
        Assert.Equal(0x42, copy.Ay.Register(0));
        Assert.Equal(0x0F, copy.Ay.Register(8));
        Assert.Equal(8, copy.Ay.SelectedRegister);
    }

    [Fact]
    public void SnapshotOfTheWrongModel_IsRefused()
    {
        var file128 = SnaFormat.Save(MarkedMachine(0));
        var file48 = SnaFormat.Save(new Spectrum48(new byte[Memory48K.RomSize]) { Cpu = { SP = 0x8000 } });

        Assert.Throws<InvalidDataException>(() => SnaFormat.Load(new Spectrum48(new byte[Memory48K.RomSize]), file128));
        Assert.Throws<InvalidDataException>(() => SnaFormat.Load(TestMachine.New128(), file48));
        Assert.False(Snapshot.IsSpectrum128("game.sna", file48));
        Assert.True(Snapshot.IsSpectrum128("game.sna", file128));
    }

    public static TheoryData<string> Formats() => ["sna", "z80"];

    [Theory]
    [MemberData(nameof(Formats))]
    public void ProgramTypedIn128Basic_RunsAfterLoadingTheSnapshot(string format)
    {
        var original = TestMachine.New128();
        original.RunFrames(Spectrum128.MenuFrames);
        original.Type(SpectrumKey.CapsShift, SpectrumKey.D6); // menu: down to 128 BASIC
        original.Type(SpectrumKey.Enter);
        original.RunFrames(50);
        original.TypeText("10 print \"HELLO\"");
        original.Type(SpectrumKey.Enter);

        var data = format == "sna" ? SnaFormat.Save(original) : Z80FileBuilder.Version3For128(original);
        var copy = TestMachine.New128();
        Snapshot.Load(copy, $"game.{format}", data);

        // The snapshot was taken while the editor was still storing the line: let it finish, or
        // it ignores the first keys (as the original machine would).
        copy.RunFrames(50);
        copy.TypeText("run");
        copy.Type(SpectrumKey.Enter);
        copy.RunFrames(10);

        var screen = Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(copy, row)).ToList();
        Assert.True(screen.Any(line => line.StartsWith("HELLO ", StringComparison.Ordinal)), string.Join('\n', screen));
    }
}
