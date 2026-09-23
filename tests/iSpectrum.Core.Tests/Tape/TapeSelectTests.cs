// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

/// <summary>TZX block 0x28: the deck stops at the menu, and goes on where the user chooses.</summary>
public class TapeSelectTests
{
    private const int MaxFrames = 600;

    [Fact]
    public void Parse_ReadsTheChoicesAndTheirOffsets()
    {
        var tape = TapeBuilder.Tzx(TapeBuilder.SelectBlock((1, "Side A"), (-1, "Rewind")));

        var select = Assert.Single(tape.Blocks);
        Assert.Equal(TapeBlockKind.Select, select.Kind);
        Assert.Equal(["Side A", "Rewind"], select.Texts);
        Assert.Equal([1, -1], select.Offsets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Choice_LoadsTheProgramItLeadsTo(bool fastLoad)
    {
        var spectrum = TestMachine.Boot();
        spectrum.FastLoad = fastLoad;
        spectrum.Tape.Insert(TwoPrograms());
        TypeLoad(spectrum);

        var select = WaitForSelect(spectrum);
        Assert.Equal(["hello", "other"], select.Texts);
        spectrum.Tape.Choose(1);
        spectrum.Tape.Play(spectrum.Cpu.TStates);

        Assert.True(RunUntilOnScreen(spectrum, "HO "));
        Assert.True(OnScreen(spectrum, "Program: other "));
    }

    [Fact]
    public void NoChoice_GoesOnAtTheNextBlock()
    {
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TwoPrograms());
        TypeLoad(spectrum);

        WaitForSelect(spectrum);
        spectrum.Tape.SkipChoice();
        spectrum.Tape.Play(spectrum.Cpu.TStates);

        Assert.True(RunUntilOnScreen(spectrum, "HI "));
    }

    [Fact]
    public void Deck_DoesNotPlay_UntilTheUserChooses()
    {
        var spectrum = TestMachine.Boot();
        spectrum.Tape.Insert(TwoPrograms());
        TypeLoad(spectrum);
        WaitForSelect(spectrum);

        // The ROM keeps listening, and would press "play" again.
        spectrum.RunFrames(50);

        Assert.False(spectrum.Tape.IsPlaying);
        Assert.NotNull(spectrum.Tape.PendingSelect);
        Assert.Equal(0, spectrum.Tape.CurrentBlock);
    }

    /// <summary>A menu, then "hello" (prints HI) and "other" (prints HO), each a header and its data.</summary>
    private static TapeImage TwoPrograms()
    {
        var hello = TapeBuilder.HelloProgram();
        byte[] print = [0x00, 0x0A, 0x06, 0x00, 0xF5, (byte)'"', (byte)'H', (byte)'O', (byte)'"', 0x0D];
        var other = TapeBuilder.Program("other", print, autoStartLine: 10);
        return TapeBuilder.Tzx(
            TapeBuilder.SelectBlock((1, "hello"), (3, "other")),
            TapeBuilder.StandardBlock(hello.Blocks[0]),
            TapeBuilder.StandardBlock(hello.Blocks[1]),
            TapeBuilder.StandardBlock(other.Blocks[0]),
            TapeBuilder.StandardBlock(other.Blocks[1]));
    }

    private static TapeBlock WaitForSelect(Spectrum48 spectrum)
    {
        for (var frame = 0; frame < MaxFrames && spectrum.Tape.PendingSelect is null; frame++)
        {
            spectrum.RunFrame();
        }

        return Assert.IsType<TapeBlock>(spectrum.Tape.PendingSelect);
    }

    private static void TypeLoad(Spectrum48 spectrum)
    {
        spectrum.Type(SpectrumKey.J);
        spectrum.TypeText("\"\"");
        spectrum.Type(SpectrumKey.Enter);
    }

    private static bool RunUntilOnScreen(Spectrum48 spectrum, string start)
    {
        for (var frame = 0; frame < MaxFrames && !OnScreen(spectrum, start); frame++)
        {
            spectrum.RunFrame();
        }

        return OnScreen(spectrum, start);
    }

    private static bool OnScreen(Spectrum48 spectrum, string start)
    {
        for (var row = 0; row < 24; row++)
        {
            if (ScreenText.ReadRow(spectrum.Memory.Contents, row).StartsWith(start, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
