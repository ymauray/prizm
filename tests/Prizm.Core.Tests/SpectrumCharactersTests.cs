// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tests;

public class SpectrumCharactersTests
{
    [Theory]
    [InlineData('a', SpectrumKey.A)]
    [InlineData('z', SpectrumKey.Z)]
    [InlineData('0', SpectrumKey.D0)]
    [InlineData('9', SpectrumKey.D9)]
    [InlineData(' ', SpectrumKey.Space)]
    public void LettersDigitsAndSpace_NeedNoShift(char c, SpectrumKey expected)
    {
        Assert.True(SpectrumCharacters.TryGetKeys(c, out var key, out var shift));
        Assert.Equal(expected, key);
        Assert.Null(shift);
    }

    [Fact]
    public void Capitals_UseCapsShift()
    {
        Assert.True(SpectrumCharacters.TryGetKeys('Q', out var key, out var shift));
        Assert.Equal(SpectrumKey.Q, key);
        Assert.Equal(SpectrumKey.CapsShift, shift);
    }

    [Theory]
    [InlineData('"', SpectrumKey.P)]
    [InlineData('@', SpectrumKey.D2)]
    [InlineData('+', SpectrumKey.K)]
    [InlineData('=', SpectrumKey.L)]
    [InlineData(':', SpectrumKey.Z)]
    [InlineData('£', SpectrumKey.X)]
    [InlineData('.', SpectrumKey.M)]
    [InlineData('≠', SpectrumKey.W)]
    public void Symbols_UseSymbolShift(char c, SpectrumKey expected)
    {
        Assert.True(SpectrumCharacters.TryGetKeys(c, out var key, out var shift));
        Assert.Equal(expected, key);
        Assert.Equal(SpectrumKey.SymbolShift, shift);
    }

    [Theory]
    [InlineData('[')]
    [InlineData('~')]
    [InlineData('é')]
    [InlineData('\n')]
    public void ExtendedModeAndForeignCharacters_AreNotTypable(char c) =>
        Assert.False(SpectrumCharacters.TryGetKeys(c, out _, out _));

    [Theory]
    [InlineData('[', SpectrumKey.Y)]
    [InlineData(']', SpectrumKey.U)]
    [InlineData('{', SpectrumKey.F)]
    [InlineData('}', SpectrumKey.G)]
    [InlineData('~', SpectrumKey.A)]
    [InlineData('|', SpectrumKey.S)]
    [InlineData('\\', SpectrumKey.D)]
    [InlineData('©', SpectrumKey.P)]
    public void ExtendedModeCharacters_UseSymbolShiftAfterTheECursor(char c, SpectrumKey expected)
    {
        Assert.False(SpectrumCharacters.TryGetKeys(c, out _, out _));
        Assert.True(SpectrumCharacters.TryGetExtendedKey(c, out var key));
        Assert.Equal(expected, key);
        Assert.Equal([[SpectrumKey.CapsShift, SpectrumKey.SymbolShift], [SpectrumKey.SymbolShift, key]], SpectrumCharacters.ExtendedStrokes(c));
    }
}
