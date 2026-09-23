// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>ZX Spectrum 48K: Z80, memory and ULA, run one 50 Hz frame at a time.</summary>
public sealed class Spectrum48
{
    public const int FrameTStates = 69888;

    /// <summary>The ULA holds INT low for 32 T-states at the start of each frame.</summary>
    private const int InterruptLength = 32;

    public Spectrum48(ReadOnlySpan<byte> rom)
    {
        Memory = new Memory48K(rom);
        Ula = new Ula();
        Cpu = new Z80Cpu(Memory, Ula);
    }

    public Z80Cpu Cpu { get; }

    public Memory48K Memory { get; }

    public Ula Ula { get; }

    /// <summary>Keys held down; the front-end updates it between frames.</summary>
    public Keyboard Keyboard => Ula.Keyboard;

    /// <summary>The picture of the last completed frame (see <see cref="Ula.FrameBuffer"/>).</summary>
    public ReadOnlySpan<uint> FrameBuffer => Ula.FrameBuffer;

    /// <summary>
    /// Runs one frame. The interrupt is requested while INT is held, so a CPU that has just run
    /// EI still takes it once the next instruction is done; the last instruction may overrun
    /// the frame, and the overrun is carried into the next one.
    /// </summary>
    public void RunFrame()
    {
        var interruptTaken = false;

        while (Cpu.TStates < FrameTStates)
        {
            if (!interruptTaken && Cpu.TStates < InterruptLength)
            {
                interruptTaken = Cpu.Interrupt();
            }

            Cpu.Step();
        }

        Cpu.TStates -= FrameTStates;
        Ula.EndFrame(Memory.Contents);
    }
}
