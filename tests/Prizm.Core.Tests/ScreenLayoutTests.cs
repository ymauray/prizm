// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tests;

public class ScreenLayoutTests
{
    [Theory]
    [InlineData(0, 0, 0x4000)]
    [InlineData(8, 0, 0x4001)]
    [InlineData(255, 0, 0x401F)]
    [InlineData(0, 1, 0x4100)] // next pixel line: +256
    [InlineData(0, 7, 0x4700)]
    [InlineData(0, 8, 0x4020)] // next character row: +32
    [InlineData(0, 63, 0x47E0)]
    [InlineData(0, 64, 0x4800)] // second third
    [InlineData(0, 128, 0x5000)] // last third
    [InlineData(255, 191, 0x57FF)]
    public void PixelAddress_FollowsTheInterleavedLayout(int x, int y, int expected) =>
        Assert.Equal(expected, ScreenLayout.PixelAddress(x, y));

    [Theory]
    [InlineData(0, 0, 0x5800)]
    [InlineData(7, 7, 0x5800)]
    [InlineData(8, 0, 0x5801)]
    [InlineData(0, 8, 0x5820)]
    [InlineData(255, 191, 0x5AFF)]
    public void AttributeAddress_IsLinearPerCell(int x, int y, int expected) =>
        Assert.Equal(expected, ScreenLayout.AttributeAddress(x, y));
}
