// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class Memory48KTests
{
    [Fact]
    public void Rom_IsWriteProtected_AndRamIsWritable()
    {
        var rom = new byte[Memory48K.RomSize];
        rom[0x3FFF] = 0xAA;
        var memory = new Memory48K(rom);

        memory.Write(0x3FFF, 0x55);
        memory.Write(0x4000, 0x55);

        Assert.Equal(0xAA, memory.Read(0x3FFF));
        Assert.Equal(0x55, memory.Read(0x4000));
    }

    [Fact]
    public void Constructor_RejectsAWrongRomSize() =>
        Assert.Throws<ArgumentException>(() => new Memory48K(new byte[100]));
}
