// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

public class TapFileTests
{
    [Fact]
    public void Parse_SplitsTheBlocksOnTheirLengths()
    {
        byte[] data = [0x03, 0x00, 0x00, 0x41, 0x41, 0x02, 0x00, 0xFF, 0xFF];

        var tape = TapFile.Parse(data);

        Assert.Equal(2, tape.Blocks.Count);
        Assert.Equal(new byte[] { 0x00, 0x41, 0x41 }, tape.Blocks[0]);
        Assert.Equal(new byte[] { 0xFF, 0xFF }, tape.Blocks[1]);
    }

    [Fact]
    public void Parse_AcceptsAnEmptyFile() => Assert.Empty(TapFile.Parse([]).Blocks);

    [Theory]
    [InlineData(new byte[] { 0x05 })]
    [InlineData(new byte[] { 0x05, 0x00, 0x00, 0x01 })]
    public void Parse_RejectsTruncatedData(byte[] data) =>
        Assert.Throws<InvalidDataException>(() => TapFile.Parse(data));

    [Fact]
    public void MakeBlock_AddsFlagAndChecksum()
    {
        var block = TapFile.MakeBlock(0xFF, [0x12, 0x34]);

        Assert.Equal(new byte[] { 0xFF, 0x12, 0x34, 0xFF ^ 0x12 ^ 0x34 }, block);
    }
}
