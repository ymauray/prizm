// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Core.Tape;

namespace Prizm.Core.Tests.Tape;

/// <summary>The real ROM loads a tape from the signal, as it would from a cassette.</summary>
public class TapeLoadingTests
{
    /// <summary>
    /// The ROM first waits about 1 s for the tape to settle; then header pilot 5 s, pause 1 s,
    /// data pilot 2 s and the data: about 9 s in all (about 400 frames measured).
    /// </summary>
    private const int MaxLoadingFrames = 600;

    [Fact]
    public void Rom_LoadsAndRunsAProgramFromTheTapeSignal()
    {
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());

        TypeLoad(spectrum);
        Assert.True(spectrum.Tape.IsPlaying);

        // The ROM prints the header's name, then the program prints HI on the next line.
        for (var frame = 0; frame < MaxLoadingFrames && FindRow(spectrum, "HI ") < 0; frame++)
        {
            spectrum.RunFrame();
        }

        var nameRow = FindRow(spectrum, "Program: hello ");
        Assert.True(nameRow >= 0, "The header's name is not on the screen.");
        Assert.Equal(nameRow + 1, FindRow(spectrum, "HI "));

        // The report comes once the program has ended.
        spectrum.RunFrames(10);
        Assert.StartsWith("0 OK, 10:1", ScreenText.ReadRow(spectrum.Memory.Contents, 23));
    }

    [Fact]
    public void PilotTone_PaintsStripesInTheBorder_AndIsHeard()
    {
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());
        TypeLoad(spectrum);

        // The ROM waits about 1 s before it looks for the pilot tone.
        spectrum.RunFrames(60);

        // The ROM switches the border between red and cyan on each edge of the pilot tone.
        var colors = new HashSet<uint>();
        for (var row = 0; row < Ula.FrameHeight; row++)
        {
            colors.Add(spectrum.FrameBuffer[row * Ula.FrameWidth]);
        }

        Assert.Contains(Palette.Colors[2], colors);
        Assert.Contains(Palette.Colors[5], colors);

        // The pilot tone is a 807 Hz square wave: about 32 sign changes per frame.
        var signChanges = 0;
        for (var i = 1; i < spectrum.AudioSamples.Length; i++)
        {
            if ((spectrum.AudioSamples[i] > 0) != (spectrum.AudioSamples[i - 1] > 0))
            {
                signChanges++;
            }
        }

        Assert.InRange(signChanges, 26, 38);
    }

    [Fact]
    public void FastLoad_LoadsTheProgramInAFewFrames()
    {
        var spectrum = TestMachine.Boot();
        spectrum.FastLoad = true;
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());

        TypeLoad(spectrum);
        spectrum.RunFrames(10);

        Assert.Equal(FindRow(spectrum, "Program: hello ") + 1, FindRow(spectrum, "HI "));
        Assert.StartsWith("0 OK, 10:1", ScreenText.ReadRow(spectrum.Memory.Contents, 23));
        Assert.True(spectrum.Tape.AtEnd);
    }

    [Fact]
    public void FastLoad_SkipsABlockWithTheWrongFlag_AsTheRomDoes()
    {
        // A stray data block before the program: LOAD "" wants a header first, so it skips it.
        var hello = TapeBuilder.HelloProgram();
        var tape = TapeBuilder.Tap(TapFile.MakeBlock(0xFF, [1, 2, 3]), hello.Blocks[0], hello.Blocks[1]);
        var spectrum = TestMachine.Boot();
        spectrum.FastLoad = true;
        spectrum.Tape.Insert(tape);

        TypeLoad(spectrum);
        spectrum.RunFrames(10);

        Assert.True(FindRow(spectrum, "HI ") > 0);
    }

    [Fact]
    public void FastLoad_ReportsABadChecksum_AsATapeLoadingError()
    {
        var hello = TapeBuilder.HelloProgram();
        var corrupted = (byte[])hello.Blocks[1].Clone();
        corrupted[^1] ^= 0x01;
        var spectrum = TestMachine.Boot();
        spectrum.FastLoad = true;
        spectrum.Tape.Insert(TapeBuilder.Tap(hello.Blocks[0], corrupted));

        TypeLoad(spectrum);
        spectrum.RunFrames(10);

        Assert.StartsWith("R Tape loading error", ScreenText.ReadRow(spectrum.Memory.Contents, 23));
    }

    [Fact]
    public void Rom_LoadsATzxTape_ThroughItsInformationBlocks()
    {
        var hello = TapeBuilder.HelloProgram();
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TapeBuilder.Tzx(
            TapeBuilder.TextBlock("hello, a test tape"),
            TapeBuilder.StandardBlock(hello.Blocks[0]),
            TapeBuilder.StandardBlock(hello.Blocks[1])));

        TypeLoad(spectrum);
        RunUntilRow(spectrum, "HI ", MaxLoadingFrames);

        Assert.True(FindRow(spectrum, "HI ") > 0);
    }

    /// <summary>
    /// A data block at another speed than the ROM's, but one the ROM still reads, stands for a
    /// turbo loader's: fast loading takes the header at once, then plays the rest in real time.
    /// </summary>
    [Fact]
    public void FastLoad_PlaysInRealTime_WhatTheRomCannotTakeAtOnce()
    {
        var hello = TapeBuilder.HelloProgram();
        var spectrum = TestMachine.Boot();
        spectrum.FastLoad = true;
        spectrum.Tape.Insert(TapeBuilder.Tzx(
            TapeBuilder.StandardBlock(hello.Blocks[0]),
            TapeBuilder.TurboBlock(hello.Blocks[1], zeroPulse: 800, onePulse: 1600)));

        TypeLoad(spectrum);
        spectrum.RunFrames(10);
        Assert.True(FindRow(spectrum, "Program: hello ") >= 0, "The header is not fast-loaded.");
        Assert.True(spectrum.Tape.IsPlaying, "The turbo block does not play.");

        // Its pause, its pilot and its data take about 2 s.
        RunUntilRow(spectrum, "HI ", 200);
        Assert.True(FindRow(spectrum, "HI ") > 0);
    }

    [Fact]
    public void StopBlock_StopsTheTape_UntilTheRomLoadsAgain()
    {
        var hello = TapeBuilder.HelloProgram();
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TapeBuilder.Tzx(
            TapeBuilder.StandardBlock(hello.Blocks[0]),
            TapeBuilder.PauseBlock(0),
            TapeBuilder.StandardBlock(hello.Blocks[1])));

        TypeLoad(spectrum);
        RunUntilRow(spectrum, "Program: hello ", MaxLoadingFrames);

        // The ROM goes on to load the data, which starts the tape again.
        RunUntilRow(spectrum, "HI ", MaxLoadingFrames);
        Assert.True(FindRow(spectrum, "HI ") > 0);
    }

    [Fact]
    public void AutoTyper_TypesLoadAfterBoot_WhileTheUserTypesNothing()
    {
        var spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")))
        {
            FastLoad = true,
        };
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());

        spectrum.AutoTyper.Start(AutoTyper.LoadCommand, Spectrum48.BootFrames);
        spectrum.Keyboard.ReleaseAll(); // the front-end clearing its own keys must not stop the typing
        spectrum.RunFrames(Spectrum48.BootFrames + 50);

        Assert.False(spectrum.AutoTyper.IsBusy);
        Assert.True(FindRow(spectrum, "HI ") > 0);
    }

    private static void RunUntilRow(Spectrum48 spectrum, string start, int maxFrames)
    {
        for (var frame = 0; frame < maxFrames && FindRow(spectrum, start) < 0; frame++)
        {
            spectrum.RunFrame();
        }
    }

    private static int FindRow(Spectrum48 spectrum, string start)
    {
        for (var row = 0; row < 24; row++)
        {
            if (ScreenText.ReadRow(spectrum.Memory.Contents, row).StartsWith(start, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>LOAD "": J is LOAD in keyword mode, Symbol Shift + P is the quote.</summary>
    private static void TypeLoad(Spectrum48 spectrum)
    {
        spectrum.Type(SpectrumKey.J);
        spectrum.TypeText("\"\"");
        spectrum.Type(SpectrumKey.Enter);
    }
}
