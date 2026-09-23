// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class BeeperTests
{
    private const double TStatesPerSample = 3_500_000.0 / Beeper.SampleRate;

    private readonly Beeper _beeper = new();

    [Fact]
    public void QuietSpeaker_GivesSilence()
    {
        _beeper.StartFrame();
        _beeper.EndFrame(Spectrum48.FrameTStates);

        Assert.All(_beeper.Samples.ToArray(), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void FiftyFrames_Give44029Samples()
    {
        // 50 frames of 69888 T-states at 3.5 MHz last 0.998400 s: 44029.4 samples at 44.1 kHz.
        var total = 0;
        for (var frame = 0; frame < 50; frame++)
        {
            _beeper.StartFrame();
            _beeper.EndFrame(Spectrum48.FrameTStates);
            total += _beeper.Samples.Length;
        }

        Assert.Equal(44029, total);
    }

    [Fact]
    public void Sample_AveragesTheLevelOverItsDuration()
    {
        _beeper.StartFrame();
        _beeper.SetLevel((long)(TStatesPerSample / 2), high: true); // high for the second half of sample 0
        _beeper.EndFrame(Spectrum48.FrameTStates);

        // The filter passes the first step unchanged: half of the full amplitude (8000).
        Assert.InRange(_beeper.Samples[0], 3900, 4100);
        Assert.InRange(_beeper.Samples[1], 7900, 8000);
    }

    [Fact]
    public void Ula_DrivesTheSpeakerWithBit4()
    {
        var ula = new Ula();
        ula.Beeper.StartFrame();

        ula.Out(0x00FE, 0x10);
        ula.Beeper.EndFrame(Spectrum48.FrameTStates);

        Assert.True(ula.Beeper.Samples[0] > 0);
    }

    [Fact]
    public void Rom_BeepPlaysMiddleC()
    {
        var spectrum = TestMachine.Boot();

        // BEEP is Symbol Shift + Z in extended mode (Caps Shift + Symbol Shift): BEEP 1,0.
        spectrum.Type(SpectrumKey.CapsShift, SpectrumKey.SymbolShift);
        spectrum.Type(SpectrumKey.SymbolShift, SpectrumKey.Z);
        spectrum.TypeText("1,0");
        spectrum.Type(SpectrumKey.Enter);

        // The beep lasts 50 frames; Type has already run 10 of them. Listen to the next 10.
        var signChanges = 0;
        var previous = 0;
        for (var frame = 0; frame < 10; frame++)
        {
            spectrum.RunFrame();
            foreach (var sample in spectrum.AudioSamples)
            {
                if (sample != 0 && previous != 0 && (sample > 0) != (previous > 0))
                {
                    signChanges++;
                }

                previous = sample != 0 ? sample : previous;
            }
        }

        // Middle C is 261.6 Hz: two sign changes per period, over 10 frames (0.1997 s): about 104.5.
        Assert.InRange(signChanges, 94, 115);
    }
}
