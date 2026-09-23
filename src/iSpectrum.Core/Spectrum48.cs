// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;
using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>ZX Spectrum 48K: Z80, memory and ULA, run one 50 Hz frame at a time.</summary>
public sealed class Spectrum48
{
    public const int FrameTStates = 69888;

    /// <summary>The ULA holds INT low for 32 T-states at the start of each frame.</summary>
    private const int InterruptLength = 32;

    /// <summary>LD-BYTES, the ROM routine that loads a block from tape.</summary>
    private const ushort LdBytes = 0x0556;

    public Spectrum48(ReadOnlySpan<byte> rom)
    {
        Memory = new Memory48K(rom);
        Ula = new Ula();
        Cpu = new Z80Cpu(Memory, Ula);
        Ula.Connect(Cpu);
    }

    public Z80Cpu Cpu { get; }

    public Memory48K Memory { get; }

    public Ula Ula { get; }

    /// <summary>Keys held down; the front-end updates it between frames.</summary>
    public Keyboard Keyboard => Ula.Keyboard;

    /// <summary>The tape deck. It starts playing when the ROM starts loading.</summary>
    public TapePlayer Tape => Ula.Tape;

    /// <summary>The picture of the last completed frame (see <see cref="Ula.FrameBuffer"/>).</summary>
    public ReadOnlySpan<uint> FrameBuffer => Ula.FrameBuffer;

    /// <summary>The sound of the last completed frame (see <see cref="Beeper"/>).</summary>
    public ReadOnlySpan<short> AudioSamples => Ula.Beeper.Samples;

    /// <summary>
    /// Runs one frame. The interrupt is requested while INT is held, so a CPU that has just run
    /// EI still takes it once the next instruction is done; the last instruction may overrun
    /// the frame, and the overrun is carried into the next one.
    /// </summary>
    public void RunFrame()
    {
        var interruptTaken = false;
        Ula.Beeper.StartFrame();

        while (Cpu.TStates < FrameTStates)
        {
            if (!interruptTaken && Cpu.TStates < InterruptLength)
            {
                interruptTaken = Cpu.Interrupt();
            }

            // Press "play" when the ROM starts listening to the tape.
            if (Cpu.PC == LdBytes && !Tape.IsPlaying)
            {
                Tape.Play(Cpu.TStates);
            }

            Cpu.Step();
        }

        Ula.EndFrameSound(Cpu.TStates, FrameTStates);
        Cpu.TStates -= FrameTStates;
        Ula.EndFrame(Memory.Contents);
    }
}
