// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>ZX Spectrum 48K: 16 KB of ROM, 48 KB of RAM, and the ULA on port 0xFE.</summary>
public sealed class Spectrum48 : Spectrum
{
    private readonly Memory48K _memory48;

    public Spectrum48(ReadOnlySpan<byte> rom)
        : this(new Memory48K(rom), new Ula(SpectrumTimings.Spectrum48))
    {
    }

    private Spectrum48(Memory48K memory, Ula ula)
        : base(SpectrumTimings.Spectrum48, memory, ula, io: ula)
    {
        _memory48 = memory;
    }

    public override Memory48K Memory => _memory48;
}
