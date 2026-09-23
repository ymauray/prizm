// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tests.Tape;

namespace iSpectrum.Core.Tests;

public class Spectrum128Tests
{
    /// <summary>Frames the 128K needs to test its RAM and show its menu.</summary>
    private const int BootFrames = 150;

    [Fact]
    public void Rom_BootsToTheMenu()
    {
        var spectrum = TestMachine.New128();

        spectrum.RunFrames(BootFrames);

        var screen = Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(spectrum, row)).ToList();
        Assert.Contains(screen, line => line.Contains("Tape Loader"));
        Assert.Contains(screen, line => line.Contains("128 BASIC"));
        Assert.Contains(screen, line => line.Contains("48 BASIC"));
        Assert.StartsWith("© 1986 Sinclair Research Ltd", screen[23]);
    }

    [Theory]
    [InlineData(0x7FFD, true)]
    [InlineData(0x1FFD, true)]  // only A15 and A1 are decoded
    [InlineData(0xFFFD, false)] // A15 set: the AY's register port
    [InlineData(0x7FFF, false)] // A1 set
    public void OutToThePagingPort_PagesTheMemory_WhenA15AndA1AreLow(int port, bool pages)
    {
        var spectrum = TestMachine.New128();
        spectrum.Memory.Write(0x8000, 0xED); // OUT (C),A
        spectrum.Memory.Write(0x8001, 0x79);
        spectrum.Cpu.PC = 0x8000;
        (spectrum.Cpu.B, spectrum.Cpu.C) = ((byte)(port >> 8), (byte)port);
        spectrum.Cpu.A = 0x10 | 3; // ROM 1, bank 3

        spectrum.Cpu.Step();

        Assert.Equal(pages ? 3 : 0, spectrum.Memory.RamPage);
        Assert.Equal(pages ? 1 : 0, spectrum.Memory.RomPage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TapeLoader_LoadsAndRunsAProgram(bool fastLoad)
    {
        var spectrum = TestMachine.New128();
        spectrum.FastLoad = fastLoad;
        spectrum.Headless = true;
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());

        // "Tape Loader" is the menu's first entry, already selected: ENTER runs LOAD "".
        spectrum.AutoTyper.Start([[SpectrumKey.Enter]], BootFrames);
        for (var frame = 0; frame < BootFrames + 600 && !OnScreen(spectrum, "HI "); frame++)
        {
            spectrum.RunFrame();
        }

        Assert.True(OnScreen(spectrum, "HI "), string.Join('\n', Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(spectrum, row))));
    }

    [Fact]
    public void Basic128Editor_TakesExtendedModeCharacters()
    {
        var spectrum = TestMachine.New128();

        // Menu: cursor down to "128 BASIC", ENTER; then PRINT "[|]", keywords spelt out.
        spectrum.RunFrames(BootFrames);
        spectrum.Type(SpectrumKey.CapsShift, SpectrumKey.D6);
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(50);
        spectrum.TypeText("print \"");
        foreach (var c in "[|]")
        {
            spectrum.AutoTyper.Append(SpectrumCharacters.ExtendedStrokes(c)!);
        }

        while (spectrum.AutoTyper.IsBusy)
        {
            spectrum.RunFrame();
        }

        spectrum.TypeText("\"");
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(20);

        Assert.True(OnScreen(spectrum, "[|] "), string.Join('\n', Enumerable.Range(0, 24).Select(row => ScreenText.ReadRow(spectrum, row))));
    }

    private static bool OnScreen(Spectrum128 spectrum, string start)
    {
        for (var row = 0; row < 24; row++)
        {
            if (ScreenText.ReadRow(spectrum, row).StartsWith(start, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
