// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>
/// 48K memory map: 16 KB of ROM at 0x0000-0x3FFF (writes ignored), 48 KB of RAM above, of which
/// 0x4000-0x7FFF is shared with the ULA and contended (see <see cref="Contention48K"/>).
/// </summary>
public sealed class Memory48K : IMemory
{
    public const int RomSize = 0x4000;

    private const ushort ScreenEnd = 0x5B00;

    private readonly byte[] _memory = new byte[0x10000];

    /// <summary>Told before each write to the screen memory, so the picture can catch up first.</summary>
    internal IScreenWriteObserver? ScreenObserver { get; set; }

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

    public byte Read(ushort address) => _memory[address];

    public void Write(ushort address, byte value)
    {
        if (address < RomSize)
        {
            return;
        }

        if (address < ScreenEnd)
        {
            ScreenObserver?.BeforeScreenWrite(_memory);
        }

        _memory[address] = value;
    }

    public int ContentionDelay(ushort address, long tStates) =>
        Contention48K.IsContended(address) ? Contention48K.Delay(tStates) : 0;

    public bool IsContended(ushort address) => Contention48K.IsContended(address);
}
