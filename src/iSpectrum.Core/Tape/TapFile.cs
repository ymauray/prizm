// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

/// <summary>
/// A .TAP tape image: the blocks the ROM saves, one after the other, each preceded by its length
/// (2 bytes, little-endian). A block is a flag byte (0x00 for a header, 0xFF for data), the data,
/// and a checksum byte (the XOR of all the bytes before it).
/// </summary>
public sealed class TapFile
{
    private TapFile(IReadOnlyList<byte[]> blocks) => Blocks = blocks;

    /// <summary>The blocks, each with its flag and checksum bytes.</summary>
    public IReadOnlyList<byte[]> Blocks { get; }

    public static TapFile Parse(ReadOnlySpan<byte> data)
    {
        var blocks = new List<byte[]>();
        var position = 0;

        while (position < data.Length)
        {
            if (position + 2 > data.Length)
            {
                throw new InvalidDataException("The file ends in the middle of a block length.");
            }

            var length = data[position] | (data[position + 1] << 8);
            position += 2;

            if (position + length > data.Length)
            {
                throw new InvalidDataException($"Block {blocks.Count + 1} is truncated.");
            }

            blocks.Add(data.Slice(position, length).ToArray());
            position += length;
        }

        return new TapFile(blocks);
    }

    /// <summary>Builds a block from its flag and data, adding the checksum (used by tests and tools).</summary>
    public static byte[] MakeBlock(byte flag, ReadOnlySpan<byte> data)
    {
        var block = new byte[data.Length + 2];
        block[0] = flag;
        data.CopyTo(block.AsSpan(1));

        byte checksum = 0;
        for (var i = 0; i < block.Length - 1; i++)
        {
            checksum ^= block[i];
        }

        block[^1] = checksum;
        return block;
    }
}
