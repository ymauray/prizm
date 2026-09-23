// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

/// <summary>SA-BYTES is intercepted: what the ROM saves lands whole in the recorder.</summary>
public class TapeSavingTests
{
    /// <summary>A program saved without LINE has no auto-start: its header holds 32768.</summary>
    private const int NoAutoStart = 0x8000;

    /// <summary>Saving is instant, but the ROM waits 50 frames between the header and the data.</summary>
    private const int SaveFrames = 80;

    [Fact]
    public void Save_RecordsTheHeaderAndTheProgram_AsTheRomBuildsThem()
    {
        var spectrum = TestMachine.Boot();

        // 10 PRINT "HI", then SAVE "hello": P and S give the keywords in K mode.
        spectrum.Type(SpectrumKey.D1);
        spectrum.Type(SpectrumKey.D0);
        spectrum.Type(SpectrumKey.P);
        spectrum.TypeText("\"HI\"");
        spectrum.Type(SpectrumKey.Enter);
        spectrum.Type(SpectrumKey.S);
        spectrum.TypeText("\"hello\"");
        spectrum.Type(SpectrumKey.Enter);

        // "Start tape, then press any key." The ROM waits 1 s between the header and the data.
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(SaveFrames);

        var expected = TapeBuilder.Program("hello", TapeBuilder.HelloProgram().Blocks[1][1..^1], NoAutoStart);
        Assert.Equal(expected.Blocks, TakeAll(spectrum.Recorder));
        Assert.StartsWith("0 OK, 0:1", ScreenText.ReadRow(spectrum.Memory.Contents, 23));
    }

    [Fact]
    public void SavedProgram_LoadsBackOnAnotherMachine()
    {
        var saving = TestMachine.Boot();
        saving.Type(SpectrumKey.D1);
        saving.Type(SpectrumKey.D0);
        saving.Type(SpectrumKey.P);
        saving.TypeText("\"HI\"");
        saving.Type(SpectrumKey.Enter);
        saving.Type(SpectrumKey.S);
        saving.TypeText("\"hello\"");
        saving.Type(SpectrumKey.Enter);
        saving.Type(SpectrumKey.Enter);
        saving.RunFrames(SaveFrames);

        using var file = new MemoryStream();
        foreach (var block in TakeAll(saving.Recorder))
        {
            TapFile.WriteBlock(file, block);
        }

        var loading = TestMachine.Boot();
        loading.FastLoad = true;
        loading.Tape.Insert(TapeImage.Load("hello.tap", file.ToArray()));
        loading.Type(SpectrumKey.J);
        loading.TypeText("\"\"");
        loading.Type(SpectrumKey.Enter);
        loading.RunFrames(10);

        // Without auto-start, the program is loaded but not run: LIST shows it.
        loading.Type(SpectrumKey.K);
        loading.Type(SpectrumKey.Enter);
        loading.RunFrames(10);

        var screen = Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(loading.Memory.Contents, row)).ToList();
        Assert.Contains(screen, line => line.Contains("10 PRINT \"HI\"", StringComparison.Ordinal));
    }

    [Fact]
    public void SaBytes_CalledFromMachineCode_RecordsTheBlockAndReturnsToTheCaller()
    {
        var spectrum = TestMachine.Boot();
        byte[] code =
        [
            0xDD, 0x21, 0x00, 0x40, // LD IX,0x4000
            0x11, 0x03, 0x00,       // LD DE,3
            0x3E, 0xFF,             // LD A,0xFF
            0xCD, 0xC2, 0x04,       // CALL SA-BYTES
            0x18, 0xFE,             // JR $
        ];
        const ushort Start = 0x8000;
        const ushort End = Start + 12;
        for (var i = 0; i < code.Length; i++)
        {
            spectrum.Memory.Write((ushort)(Start + i), code[i]);
        }

        spectrum.Memory.Write(0x4000, 1);
        spectrum.Memory.Write(0x4001, 2);
        spectrum.Memory.Write(0x4002, 3);
        var stack = spectrum.Cpu.SP;
        spectrum.Cpu.PC = Start;

        for (var step = 0; step < 100_000 && spectrum.Cpu.PC != End; step++)
        {
            spectrum.Step();
        }

        Assert.Equal(End, spectrum.Cpu.PC);
        Assert.Equal(stack, spectrum.Cpu.SP);
        Assert.Equal([TapFile.MakeBlock(0xFF, [1, 2, 3])], TakeAll(spectrum.Recorder));
    }

    [Fact]
    public void Save_On128KBasic_GoesThroughRom1()
    {
        var spectrum = TestMachine.New128();

        // Menu: cursor down to "128 BASIC", ENTER; keywords spelt out.
        spectrum.RunFrames(150);
        spectrum.Type(SpectrumKey.CapsShift, SpectrumKey.D6);
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(50);
        spectrum.TypeText("10 print \"HI\"");
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(20);
        spectrum.TypeText("save \"hello\"");
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(20);
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(SaveFrames);

        var blocks = TakeAll(spectrum.Recorder);
        Assert.True(blocks.Count == 2, string.Join('\n', Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(spectrum, row))));
        Assert.Equal(0x00, blocks[0][0]);
        Assert.Equal("hello     "u8.ToArray(), blocks[0][2..12]);
        Assert.Equal(0xFF, blocks[1][0]);
    }

    private static List<byte[]> TakeAll(TapeRecorder recorder)
    {
        var blocks = new List<byte[]>();
        while (recorder.TryTake(out var block))
        {
            blocks.Add(block);
        }

        return blocks;
    }
}
