// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Core.Tests.Tape;

namespace Prizm.Core.Tests;

public class HeadlessTests
{
    [Fact]
    public void HeadlessMachine_EndsInTheSameState_AsOneThatDrawsAndPlays()
    {
        // Boot, then load a program from the real tape signal: memory, contention, border
        // changes, speaker and tape all take part.
        var normal = Load(headless: false);
        var headless = Load(headless: true);

        Assert.Equal(normal.Memory.Contents.ToArray(), headless.Memory.Contents.ToArray());
        Assert.Equal(normal.Cpu.PC, headless.Cpu.PC);
        Assert.Equal(normal.Cpu.TStates, headless.Cpu.TStates);
        Assert.Equal(normal.Ula.Border, headless.Ula.Border);
    }

    [Fact]
    public void HeadlessMachine_MakesNeitherPictureNorSound()
    {
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]) { Headless = true };
        spectrum.Ula.Out(0x00FE, 0x12); // red border, speaker on

        spectrum.RunFrame();

        Assert.Equal(0u, spectrum.FrameBuffer[0]);
        Assert.Equal(0, spectrum.AudioSamples.Length);
    }

    [Fact]
    public void SoundResumes_WhenHeadlessIsCleared()
    {
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]) { Headless = true };
        spectrum.RunFrame();

        spectrum.Headless = false;
        spectrum.RunFrame();

        Assert.InRange(spectrum.AudioSamples.Length, 880, 882);
    }

    private static Spectrum48 Load(bool headless)
    {
        var spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")))
        {
            Headless = headless,
        };
        spectrum.Tape.Insert(TapeBuilder.HelloProgram());
        spectrum.AutoTyper.Start(AutoTyper.LoadCommand, Spectrum48.BootFrames);
        spectrum.RunFrames(Spectrum48.BootFrames + 450);
        return spectrum;
    }
}
