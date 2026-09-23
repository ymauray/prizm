// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests;

public class KeyboardTests
{
    private readonly Keyboard _keyboard = new();

    /// <summary>High byte of the port address that selects only <paramref name="row"/>.</summary>
    private static byte HalfRow(int row) => (byte)~(1 << row);

    [Fact]
    public void NoKeyPressed_ReadsAllBitsHigh() => Assert.Equal(0x1F, _keyboard.Read(0x00));

    [Theory]
    [InlineData(SpectrumKey.CapsShift, 0xFE, 0)]
    [InlineData(SpectrumKey.V, 0xFE, 4)]
    [InlineData(SpectrumKey.A, 0xFD, 0)]
    [InlineData(SpectrumKey.T, 0xFB, 4)]
    [InlineData(SpectrumKey.D1, 0xF7, 0)]
    [InlineData(SpectrumKey.D6, 0xEF, 4)]
    [InlineData(SpectrumKey.P, 0xDF, 0)]
    [InlineData(SpectrumKey.Enter, 0xBF, 0)]
    [InlineData(SpectrumKey.SymbolShift, 0x7F, 1)]
    [InlineData(SpectrumKey.B, 0x7F, 4)]
    public void PressedKey_PullsItsBitLow_InItsHalfRow(SpectrumKey key, byte halfRow, int bit)
    {
        _keyboard.SetKey(key, true);

        Assert.Equal(0x1F & ~(1 << bit), _keyboard.Read(halfRow));
    }

    [Fact]
    public void PressedKey_IsInvisible_FromOtherHalfRows()
    {
        _keyboard.SetKey(SpectrumKey.Q, true); // half-row 2

        for (var row = 0; row < 8; row++)
        {
            Assert.Equal(row == 2 ? 0x1E : 0x1F, _keyboard.Read(HalfRow(row)));
        }
    }

    [Fact]
    public void SeveralSelectedHalfRows_CombineTheirKeys()
    {
        _keyboard.SetKey(SpectrumKey.CapsShift, true); // row 0, bit 0
        _keyboard.SetKey(SpectrumKey.S, true);         // row 1, bit 1

        Assert.Equal(0x1C, _keyboard.Read(0xFC)); // rows 0 and 1
        Assert.Equal(0x1C, _keyboard.Read(0x00)); // all rows
    }

    [Fact]
    public void ReleasedKeys_ReadHighAgain()
    {
        _keyboard.SetKey(SpectrumKey.M, true);
        _keyboard.SetKey(SpectrumKey.N, true);

        _keyboard.SetKey(SpectrumKey.M, false);
        Assert.Equal(0x17, _keyboard.Read(0x7F)); // only N (bit 3)

        _keyboard.ReleaseAll();
        Assert.Equal(0x1F, _keyboard.Read(0x00));
    }

    [Fact]
    public void Ula_ReturnsTheKeyboardOnEvenPorts_WithBits5To7Set()
    {
        var ula = new Ula();
        ula.Keyboard.SetKey(SpectrumKey.Enter, true);

        Assert.Equal(0xFE, ula.In(0xBFFE));
        Assert.Equal(0xFF, ula.In(0x7FFE));
        Assert.Equal(0xFF, ula.In(0xBFFF)); // odd port: not the ULA
    }
}
