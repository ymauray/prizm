// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// 48K memory map: 16 KB of ROM at 0x0000-0x3FFF (writes ignored), 48 KB of RAM above.
/// Contention is not modelled yet (milestone 7).
/// </summary>
public sealed class Memory48K : IMemory
{
    public const int RomSize = 0x4000;

    private readonly byte[] _memory = new byte[0x10000];

    public Memory48K(ReadOnlySpan<byte> rom)
    {
        if (rom.Length != RomSize)
        {
            throw new ArgumentException($"The 48K ROM must be {RomSize} bytes, got {rom.Length}.", nameof(rom));
        }

        rom.CopyTo(_memory);
    }

    /// <summary>The whole 64 KB address space, for the ULA and for tests.</summary>
    public ReadOnlySpan<byte> Contents => _memory;

    public byte Read(ushort address) => _memory[address];

    public void Write(ushort address, byte value)
    {
        if (address >= RomSize)
        {
            _memory[address] = value;
        }
    }
}
