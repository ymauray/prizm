// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class FloatingBusTests
{
    private const int Start = Ula.FirstPixelTState;

    private readonly Spectrum48 _spectrum = new(new byte[Memory48K.RomSize]);

    public FloatingBusTests()
    {
        _spectrum.Memory.Write(0x4000, 0xA1); // pixels, line 0, column 0
        _spectrum.Memory.Write(0x4001, 0xA2); // pixels, line 0, column 1
        _spectrum.Memory.Write(0x4100, 0xB1); // pixels, line 1, column 0
        _spectrum.Memory.Write(0x5800, 0xC1); // attribute, row 0, column 0
        _spectrum.Memory.Write(0x5801, 0xC2); // attribute, row 0, column 1
    }

    [Theory]
    [InlineData(Start - 1, 0xFF)]              // top border
    [InlineData(Start, 0xFF)]
    [InlineData(Start + 2, 0xFF)]
    [InlineData(Start + 3, 0xA1)]              // pixels of column 0
    [InlineData(Start + 4, 0xC1)]              // their attribute
    [InlineData(Start + 5, 0xA2)]              // pixels of column 1
    [InlineData(Start + 6, 0xC2)]              // their attribute
    [InlineData(Start + 7, 0xFF)]
    [InlineData(Start + 128 + 3, 0xFF)]        // right border
    [InlineData(Start + 224 + 3, 0xB1)]        // next line
    [InlineData(Start + (192 * 224) + 3, 0xFF)] // bottom border
    public void FloatingBus_ReturnsTheByteTheUlaIsReading(int tStates, int expected) =>
        Assert.Equal(expected, _spectrum.Ula.FloatingBus(tStates));

    [Fact]
    public void OddPort_ReadsTheFloatingBus_AtTheEndOfTheIoCycle()
    {
        // In is called 1 T-state into the I/O cycle; the data is latched 3 T-states later.
        _spectrum.Cpu.TStates = Start;

        Assert.Equal(0xA1, _spectrum.Ula.In(0x00FF));
    }
}
