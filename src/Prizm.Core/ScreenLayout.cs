// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>Where the ULA finds each pixel and attribute of the 256x192 display.</summary>
public static class ScreenLayout
{
    public const int Width = 256;
    public const int Height = 192;
    public const ushort BitmapAddress = 0x4000;
    public const ushort AttributesAddress = 0x5800;

    /// <summary>
    /// Address of the byte holding pixel (x, y); bit 7 of that byte is the leftmost pixel.
    /// The screen is split in three thirds of 64 lines, and within a third the address
    /// advances through pixel lines (bits 8-10) before character rows (bits 5-7).
    /// </summary>
    public static ushort PixelAddress(int x, int y) =>
        (ushort)(BitmapAddress | ((y & 0xC0) << 5) | ((y & 0x07) << 8) | ((y & 0x38) << 2) | (x >> 3));

    /// <summary>Address of the attribute of the 8x8 cell holding pixel (x, y).</summary>
    public static ushort AttributeAddress(int x, int y) =>
        (ushort)(AttributesAddress + ((y >> 3) * 32) + (x >> 3));
}
