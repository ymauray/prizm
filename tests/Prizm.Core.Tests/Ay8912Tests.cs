// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tests;

public class Ay8912Tests
{
    private static readonly SpectrumTimings Timings = SpectrumTimings.Spectrum128;

    private readonly Ay8912 _ay = new();
    private readonly Beeper _beeper = new(Timings.ClockRate);

    private void Set(int register, byte value)
    {
        _ay.SelectRegister((byte)register);
        _ay.WriteSelected(value);
    }

    private void RunFrame()
    {
        _beeper.StartFrame();
        _ay.AdvanceTo(Timings.FrameTStates, _beeper);
        _beeper.EndFrame(Timings.FrameTStates);
        _ay.EndFrame(Timings.FrameTStates);
    }

    [Theory]
    [InlineData(0, 0xFF)]
    [InlineData(1, 0x0F)]  // tone A, high 4 bits
    [InlineData(6, 0x1F)]  // noise period
    [InlineData(7, 0xFF)]  // mixer
    [InlineData(8, 0x1F)]  // volume A and envelope bit
    [InlineData(13, 0x0F)] // envelope shape
    public void Registers_KeepOnlyTheirExistingBits(int register, int expected)
    {
        Set(register, 0xFF);

        _ay.SelectRegister((byte)register);
        Assert.Equal(expected, _ay.ReadSelected());
    }

    [Fact]
    public void ToneChannel_SoundsAtClockOver16TimesThePeriod()
    {
        Set(0, 0x00);
        Set(1, 0x01);      // period 256
        Set(7, 0b111110);  // only tone A enabled
        Set(8, 15);        // full volume

        var signChanges = 0;
        var previous = 0;
        for (var frame = 0; frame < 10; frame++)
        {
            RunFrame();
            foreach (var sample in _beeper.Samples)
            {
                if (sample != 0 && previous != 0 && (sample > 0) != (previous > 0))
                {
                    signChanges++;
                }

                previous = sample != 0 ? sample : previous;
            }
        }

        // 1773450 / (16 x 256) = 433 Hz; two sign changes per period over 10 frames (0.19992 s): 173.
        Assert.InRange(signChanges, 165, 181);
    }

    [Theory]
    [InlineData(13, 1.0 / 3)] // rise, then hold at the top
    [InlineData(9, 0.0)]      // fall, then hold at 0
    [InlineData(4, 0.0)]      // rise, then drop to 0 and hold
    public void Envelope_EndsAtTheLevelItsShapeHolds(byte shape, double expected)
    {
        Set(7, 0b111111);  // no tone, no noise: the channel follows its volume
        Set(8, 0x10);      // channel A takes the envelope
        Set(11, 1);        // shortest envelope period
        Set(13, shape);

        RunFrame();

        Assert.Equal(expected, _ay.CurrentOutput, precision: 6);
    }

    [Fact]
    public void Envelope_Sawtooth_KeepsRepeating()
    {
        Set(7, 0b111111);
        Set(8, 0x10);
        Set(11, 1);
        Set(13, 8); // falling sawtooth, repeated

        var levels = new HashSet<double>();
        for (var frame = 0; frame < 3; frame++)
        {
            RunFrame();
            levels.Add(_ay.CurrentOutput);
        }

        Assert.True(levels.Count > 1);
    }

    [Fact]
    public void Rom_PlaysANoteWith128BasicsPlay()
    {
        var spectrum = TestMachine.New128();

        // Menu: cursor down to "128 BASIC", ENTER; then PLAY "c" typed letter by letter.
        spectrum.RunFrames(150);
        spectrum.Type(SpectrumKey.CapsShift, SpectrumKey.D6);
        spectrum.Type(SpectrumKey.Enter);
        spectrum.RunFrames(50);
        spectrum.TypeText("play \"c\"");
        spectrum.Type(SpectrumKey.Enter);

        var loud = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            spectrum.RunFrame();
            foreach (var sample in spectrum.AudioSamples)
            {
                loud = Math.Max(loud, Math.Abs((int)sample));
            }
        }

        Assert.True(loud > 500, $"No note heard (loudest sample {loud}).");
        Assert.NotEqual(0, spectrum.Ay.Register(0) | spectrum.Ay.Register(1));
    }
}
