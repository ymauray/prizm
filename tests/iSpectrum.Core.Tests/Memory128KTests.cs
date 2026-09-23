// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class Memory128KTests
{
    private readonly Memory128K _memory;

    public Memory128KTests()
    {
        var rom0 = new byte[SpectrumMemory.BankSize];
        var rom1 = new byte[SpectrumMemory.BankSize];
        rom0[0] = 0xA0;
        rom1[0] = 0xA1;
        _memory = new Memory128K(rom0, rom1);
    }

    [Fact]
    public void PowerOn_PagesRom0AndBank0()
    {
        Assert.Equal(0xA0, _memory.Read(0x0000));
        Assert.Equal(0, _memory.RamPage);
        Assert.False(_memory.ShadowScreen);
    }

    [Fact]
    public void Bit4_SelectsTheRom_AndRomIgnoresWrites()
    {
        _memory.WritePaging(0x10);
        _memory.Write(0x0000, 0x55);

        Assert.Equal(0xA1, _memory.Read(0x0000));
    }

    [Fact]
    public void Bits0To2_PageABankAt0xC000()
    {
        _memory.WritePaging(3);
        _memory.Write(0xC000, 0x33);

        Assert.Equal(0x33, _memory.Bank(3)[0]);
        _memory.WritePaging(4);
        Assert.Equal(0x00, _memory.Read(0xC000));
    }

    [Fact]
    public void Banks5And2_AreAlwaysAt0x4000And0x8000_AndAlsoPageableAt0xC000()
    {
        _memory.Write(0x4000, 0x55);
        _memory.Write(0x8000, 0x22);

        _memory.WritePaging(5);
        Assert.Equal(0x55, _memory.Read(0xC000));
        _memory.WritePaging(2);
        Assert.Equal(0x22, _memory.Read(0xC000));
    }

    [Fact]
    public void Bit3_ShowsBank7InsteadOfBank5()
    {
        _memory.Write(0x4000, 0x55);
        _memory.WritePaging(7);
        _memory.Write(0xC000, 0x77);

        Assert.Equal(0x55, _memory.Screen[0]);
        _memory.WritePaging(7 | 0x08);
        Assert.Equal(0x77, _memory.Screen[0]);
    }

    [Fact]
    public void Bit5_LocksPagingUntilReset()
    {
        _memory.WritePaging(0x20 | 1);
        _memory.WritePaging(0x10 | 6);

        Assert.True(_memory.PagingLocked);
        Assert.Equal(1, _memory.RamPage);
        Assert.Equal(0xA0, _memory.Read(0x0000));
    }

    [Theory]
    [InlineData(0, 0x0000, false)] // ROM
    [InlineData(0, 0x4000, true)]  // bank 5
    [InlineData(0, 0x8000, false)] // bank 2
    [InlineData(0, 0xC000, false)] // bank 0
    [InlineData(1, 0xC000, true)]  // bank 1
    [InlineData(4, 0xC000, false)]
    [InlineData(7, 0xC000, true)]
    public void OddBanks_AreContended_WhereverTheyArePaged(byte paging, int address, bool expected)
    {
        _memory.WritePaging(paging);

        Assert.Equal(expected, _memory.IsContended((ushort)address));
    }
}
