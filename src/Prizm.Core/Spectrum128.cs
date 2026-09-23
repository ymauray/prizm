// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Z80;

namespace Prizm.Core;

/// <summary>
/// ZX Spectrum 128K: two ROMs, eight RAM banks paged through port 0x7FFD, the ULA, and the
/// AY-3-8912 sound chip on ports 0xFFFD and 0xBFFD.
/// </summary>
public sealed class Spectrum128 : Spectrum
{
    private readonly Memory128K _memory128;

    public Spectrum128(ReadOnlySpan<byte> rom0, ReadOnlySpan<byte> rom1)
        : this(new Memory128K(rom0, rom1), new Ula(SpectrumTimings.Spectrum128), new Ay8912())
    {
    }

    private Spectrum128(Memory128K memory, Ula ula, Ay8912 ay)
        : base(SpectrumTimings.Spectrum128, memory, ula, new Ports(ula, memory, ay))
    {
        _memory128 = memory;
        Ay = ay;
        ula.AddSoundSource(ay);

        // As in FUSE, the model decides, even when the 128K runs its 48K BASIC.
        ula.Tape.Is48K = false;
    }

    /// <summary>The sound chip; its output is mixed into <see cref="Spectrum.AudioSamples"/>.</summary>
    public Ay8912 Ay { get; }

    public override Memory128K Memory => _memory128;

    /// <summary>Frames the 128K needs to test its RAM and show its menu.</summary>
    public const int MenuFrames = 150;

    /// <summary>"Tape Loader" is the menu's first entry, selected at power-on: ENTER runs LOAD "".</summary>
    public override void LoadTapeAfterBoot() => AutoTyper.Start([[SpectrumKey.Enter]], MenuFrames);

    /// <summary>The tape routines are in ROM 1, the 48K BASIC; the 128K menu pages it in to load.</summary>
    protected override bool IsTapeRomPaged => _memory128.RomPage == 1;

    /// <summary>
    /// The 128K's ports: the ULA on even ports; the paging register on A15 = 0, A1 = 0 (0x7FFD);
    /// the AY's register select (and read) on A15 = 1, A14 = 1, A1 = 0 (0xFFFD) and its data on
    /// A15 = 1, A14 = 0, A1 = 0 (0xBFFD).
    /// </summary>
    private sealed class Ports(Ula ula, Memory128K memory, Ay8912 ay) : IIo
    {
        public byte In(ushort port) => (port & 0xC002) == 0xC000 ? ay.ReadSelected() : ula.In(port);

        public void Out(ushort port, byte value)
        {
            if ((port & 0x8002) == 0)
            {
                memory.WritePaging(value);
            }
            else if ((port & 0xC002) == 0xC000)
            {
                ay.SelectRegister(value);
            }
            else if ((port & 0xC002) == 0x8000)
            {
                // The sound so far must be made before the chip changes.
                ula.CatchUpSound();
                ay.WriteSelected(value);
            }

            ula.Out(port, value);
        }

        public int ContentionDelay(ushort port, long tStates) => ula.ContentionDelay(port, tStates);
    }
}
