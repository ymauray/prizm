// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>ZX Spectrum 128K: two ROMs, eight RAM banks paged through port 0x7FFD, and the ULA.</summary>
public sealed class Spectrum128 : Spectrum
{
    private readonly Memory128K _memory128;

    public Spectrum128(ReadOnlySpan<byte> rom0, ReadOnlySpan<byte> rom1)
        : this(new Memory128K(rom0, rom1), new Ula(SpectrumTimings.Spectrum128))
    {
    }

    private Spectrum128(Memory128K memory, Ula ula)
        : base(SpectrumTimings.Spectrum128, memory, ula, new Ports(ula, memory))
    {
        _memory128 = memory;
    }

    public override Memory128K Memory => _memory128;

    /// <summary>The tape routines are in ROM 1, the 48K BASIC; the 128K menu pages it in to load.</summary>
    protected override bool IsTapeRomPaged => _memory128.RomPage == 1;

    /// <summary>
    /// The 128K's ports: the ULA on even ports, and the paging register, decoded on A15 = 0 and
    /// A1 = 0 (0x7FFD is the usual address).
    /// </summary>
    private sealed class Ports(Ula ula, Memory128K memory) : IIo
    {
        public byte In(ushort port) => ula.In(port);

        public void Out(ushort port, byte value)
        {
            if ((port & 0x8002) == 0)
            {
                memory.WritePaging(value);
            }

            ula.Out(port, value);
        }

        public int ContentionDelay(ushort port, long tStates) => ula.ContentionDelay(port, tStates);
    }
}
