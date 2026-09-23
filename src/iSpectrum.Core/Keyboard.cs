// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The 8 x 5 key matrix. The front-end sets which keys are down; the ULA reads the matrix
/// when the CPU reads port 0xFE.
/// </summary>
public sealed class Keyboard
{
    /// <summary>Per half-row, one bit per key held down (bit set = pressed).</summary>
    private readonly byte[] _rows = new byte[8];

    public void SetKey(SpectrumKey key, bool pressed)
    {
        var row = (int)key >> 3;
        var bit = 1 << ((int)key & 7);

        if (pressed)
        {
            _rows[row] |= (byte)bit;
        }
        else
        {
            _rows[row] &= (byte)~bit;
        }
    }

    public void ReleaseAll() => Array.Clear(_rows);

    /// <summary>
    /// Bits 0-4 of a port 0xFE read, active low. Each address line A8-A15 held low selects one
    /// half-row; when several are selected, a key pressed in any of them pulls its bit low.
    /// </summary>
    public byte Read(byte addressHigh)
    {
        var pressed = 0;
        for (var row = 0; row < 8; row++)
        {
            if ((addressHigh & (1 << row)) == 0)
            {
                pressed |= _rows[row];
            }
        }

        return (byte)(~pressed & 0x1F);
    }
}
