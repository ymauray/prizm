// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Core.Snapshots;

namespace Prizm.Core.Tests.Snapshots;

/// <summary>
/// Saves a machine holding a BASIC program, loads the file into a fresh machine, and runs the
/// program there: the snapshot must carry the whole state the ROM needs.
/// </summary>
public class SnapshotRoundTripTests
{
    public static TheoryData<string> Formats() => ["sna", "z80 v1", "z80 v1 compressed", "z80 v3"];

    [Theory]
    [MemberData(nameof(Formats))]
    public void ProgramTypedBeforeSaving_RunsAfterLoading(string format)
    {
        var original = TestMachine.Boot();
        original.TypeText("10p\"HELLO\"");
        original.Type(SpectrumKey.Enter);
        original.Ula.Border = 4;

        var (fileName, data) = format switch
        {
            "sna" => ("game.sna", SnaFormat.Save(original)),
            "z80 v1" => ("game.z80", Z80FileBuilder.Version1(original, compressed: false)),
            "z80 v1 compressed" => ("game.z80", Z80FileBuilder.Version1(original, compressed: true)),
            _ => ("game.z80", Z80FileBuilder.Version3(original)),
        };

        var copy = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")));
        Snapshot.Load(copy, fileName, data);

        Assert.Equal(original.Cpu.PC, copy.Cpu.PC);
        Assert.Equal(original.Cpu.SP, copy.Cpu.SP);
        Assert.Equal(4, copy.Ula.Border);

        // .SNA leaves PC in the two bytes below SP; the rest of the RAM must be identical.
        var sp = original.Cpu.SP;
        for (var address = 0x4000; address < 0x10000; address++)
        {
            var holdsPushedPc = format == "sna" && (address == (ushort)(sp - 2) || address == (ushort)(sp - 1));
            if (!holdsPushedPc)
            {
                Assert.Equal(original.Memory.Read((ushort)address), copy.Memory.Read((ushort)address));
            }
        }

        copy.TypeText("r");
        copy.Type(SpectrumKey.Enter);

        Assert.StartsWith("HELLO ", ScreenText.ReadRow(copy.Memory.Contents, 0));
    }

    [Theory]
    [InlineData("game.SNA", true)]
    [InlineData("game.z80", true)]
    [InlineData("game.tap", false)]
    public void IsSupported_LooksAtTheExtension(string fileName, bool expected) =>
        Assert.Equal(expected, Snapshot.IsSupported(fileName));
}
