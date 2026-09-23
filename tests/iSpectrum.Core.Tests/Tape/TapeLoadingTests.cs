// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests.Tape;

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
