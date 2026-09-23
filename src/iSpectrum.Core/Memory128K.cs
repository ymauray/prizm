// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The 128K's memory: two 16 KB ROMs and eight 16 KB RAM banks, paged through port 0x7FFD.
/// 0x0000 holds ROM 0 (editor, menu, 128 BASIC) or ROM 1 (48K BASIC); 0x4000 always holds bank
/// 5, 0x8000 bank 2, 0xC000 any bank. The ULA displays bank 5, or bank 7 (the shadow screen).
/// The odd banks are shared with the ULA and contended, wherever they are paged.
/// </summary>
/// <remarks>Port 0x7FFD: bits 0-2 bank at 0xC000, bit 3 shadow screen, bit 4 ROM, bit 5 locks paging until reset.</remarks>
public sealed class Memory128K : SpectrumMemory
{
    private const int BankCount = 8;
    private const int NormalScreenBank = 5;
    private const int ShadowScreenBank = 7;

    private readonly byte[][] _roms = new byte[2][];
    private readonly byte[][] _banks = new byte[BankCount][];

    public Memory128K(ReadOnlySpan<byte> rom0, ReadOnlySpan<byte> rom1)
    {
        _roms[0] = CheckedRom(rom0, nameof(rom0));
        _roms[1] = CheckedRom(rom1, nameof(rom1));
        for (var bank = 0; bank < BankCount; bank++)
        {
            _banks[bank] = new byte[BankSize];
        }
    }

    /// <summary>The ROM at 0x0000: 0 or 1.</summary>
    public int RomPage { get; private set; }

    /// <summary>The RAM bank at 0xC000: 0 to 7.</summary>
    public int RamPage { get; private set; }

    /// <summary>Whether the ULA displays bank 7 instead of bank 5.</summary>
    public bool ShadowScreen { get; private set; }

    /// <summary>Set by bit 5 of port 0x7FFD: paging ignores further writes until reset.</summary>
    public bool PagingLocked { get; private set; }

    /// <summary>The last value written to port 0x7FFD while paging was unlocked (for snapshots).</summary>
    public byte LastPagingValue { get; private set; }

    public override ReadOnlySpan<byte> Screen => _banks[DisplayedBank];

    private int DisplayedBank => ShadowScreen ? ShadowScreenBank : NormalScreenBank;

    /// <summary>RAM bank <paramref name="number"/> (0 to 7), for snapshots and tests.</summary>
    public Span<byte> Bank(int number) => _banks[number];

    public override byte Read(ushort address) => address < BankSize
        ? _roms[RomPage][address]
        : _banks[BankAt(address)][address & (BankSize - 1)];

    public override void Write(ushort address, byte value)
    {
        Watch?.OnMemoryWrite(address, value);

        if (address < BankSize)
        {
            return;
        }

        var bank = BankAt(address);
        var offset = address & (BankSize - 1);
        if (bank == DisplayedBank && offset < ScreenSize)
        {
            BeforeScreenChange();
        }

        _banks[bank][offset] = value;
    }

    public override bool IsContended(ushort address) => address >= BankSize && (BankAt(address) & 1) != 0;

    public override int ContentionDelay(ushort address, long tStates) =>
        IsContended(address) ? SpectrumTimings.Spectrum128.ContentionDelay(tStates) : 0;

    /// <summary>A write to port 0x7FFD; ignored once paging is locked.</summary>
    public void WritePaging(byte value)
    {
        if (PagingLocked)
        {
            return;
        }

        var shadow = (value & 0x08) != 0;
        if (shadow != ShadowScreen)
        {
            // The picture catches up with the screen shown so far before the switch.
            BeforeScreenChange();
        }

        RamPage = value & 0x07;
        ShadowScreen = shadow;
        RomPage = (value >> 4) & 1;
        PagingLocked = (value & 0x20) != 0;
        LastPagingValue = value;
    }

    private int BankAt(ushort address) => (address >> 14) switch
    {
        1 => NormalScreenBank,
        2 => 2,
        _ => RamPage,
    };

    private static byte[] CheckedRom(ReadOnlySpan<byte> rom, string name) => rom.Length == BankSize
        ? rom.ToArray()
        : throw new ArgumentException($"A 128K ROM must be {BankSize} bytes, got {rom.Length}.", name);
}
