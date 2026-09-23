// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

/// <summary>Boots the real 48K ROM and reads back what it prints.</summary>
public class BootTests
{
    [Fact]
    public void Rom_BootsToTheCopyrightMessage()
    {
        // The ROM tests the RAM first; the message appears after about 85 frames (1.7 s).
        var spectrum = TestMachine.Boot();

        Assert.Equal("© 1982 Sinclair Research Ltd", ScreenText.ReadRow(spectrum.Memory.Contents, 23).TrimEnd());
        Assert.Equal(7, spectrum.Ula.Border);
    }

    [Fact]
    public void Rom_RunsABasicCommandTypedOnTheKeyboard()
    {
        var spectrum = TestMachine.Boot();

        // In the 48K ROM, P at the start of a line is the PRINT keyword; Symbol Shift + K is "+".
        spectrum.Type(SpectrumKey.P);
        spectrum.Type(SpectrumKey.D2);
        spectrum.Type(SpectrumKey.SymbolShift, SpectrumKey.K);
        spectrum.Type(SpectrumKey.D2);
        spectrum.Type(SpectrumKey.Enter);

        Assert.StartsWith("4 ", ScreenText.ReadRow(spectrum.Memory.Contents, 0));
        Assert.StartsWith("0 OK, 0:1", ScreenText.ReadRow(spectrum.Memory.Contents, 23));
    }

    [Fact]
    public void Rom_PrintsSymbolsTypedAsCharacters()
    {
        var spectrum = TestMachine.Boot();

        // "p" at the start of a line is PRINT; the quotes, + and = go through Symbol Shift.
        spectrum.TypeText("p\"A+B=C\"");
        spectrum.Type(SpectrumKey.Enter);

        Assert.StartsWith("A+B=C ", ScreenText.ReadRow(spectrum.Memory.Contents, 0));
    }

    [Fact]
    public void Rom_PrintsExtendedModeCharacters_TypedInTwoStrokes()
    {
        var spectrum = TestMachine.Boot();
        spectrum.TypeText("p\"");

        foreach (var c in "[]{}~|\\©")
        {
            spectrum.AutoTyper.Append(SpectrumCharacters.ExtendedStrokes(c)!);
        }

        while (spectrum.AutoTyper.IsBusy)
        {
            spectrum.RunFrame();
        }

        spectrum.TypeText("\"");
        spectrum.Type(SpectrumKey.Enter);

        Assert.StartsWith("[]{}~|\\© ", ScreenText.ReadRow(spectrum.Memory.Contents, 0));
    }
}
