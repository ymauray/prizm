// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// 48K memory map: 16 KB of ROM at 0x0000-0x3FFF (writes ignored), 48 KB of RAM above, of which
/// 0x4000-0x7FFF is shared with the ULA and contended (see <see cref="SpectrumTimings"/>).
/// </summary>
public sealed class Memory48K : SpectrumMemory
{
    public const int RomSize = 0x4000;

    private const ushort ScreenEnd = 0x4000 + ScreenSize;

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

    /// <summary>Copies <paramref name="data"/> into RAM at <paramref name="address"/> (snapshot loading).</summary>
    public void LoadRam(ushort address, ReadOnlySpan<byte> data)
    {
        if (address < RomSize || address + data.Length > _memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "The data must fit in RAM (0x4000-0xFFFF).");
        }

        data.CopyTo(_memory.AsSpan(address));
    }

    /// <summary>The 48K always displays the RAM at 0x4000.</summary>
    public override ReadOnlySpan<byte> Screen => _memory.AsSpan(0x4000, BankSize);

    public override byte Read(ushort address) => _memory[address];

    public override void Write(ushort address, byte value)
    {
        Watch?.OnMemoryWrite(address, value);

        if (address < RomSize)
        {
            return;
        }

        if (address < ScreenEnd)
        {
            BeforeScreenChange();
        }

        _memory[address] = value;
    }

    public override int ContentionDelay(ushort address, long tStates) =>
        IsContended(address) ? SpectrumTimings.Spectrum48.ContentionDelay(tStates) : 0;

    /// <summary>The lower 16 KB of RAM, shared with the ULA.</summary>
    public override bool IsContended(ushort address) => (address & 0xC000) == 0x4000;
}
