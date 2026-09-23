// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tape;

/// <summary>A tape: its blocks, in order.</summary>
public sealed class TapeImage(IReadOnlyList<TapeBlock> blocks)
{
    public IReadOnlyList<TapeBlock> Blocks { get; } = blocks;

    /// <summary>A .TAP file as a tape: standard blocks, each followed by a one-second pause.</summary>
    public static TapeImage FromTap(TapFile tap)
    {
        var blocks = new TapeBlock[tap.Blocks.Count];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = TapeBlock.Standard(tap.Blocks[i], TapePlayer.PauseTStates);
        }

        return new TapeImage(blocks);
    }

    public static bool IsSupported(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".tap" or ".tzx";

    /// <summary>Reads a .TAP or .TZX file, chosen by its extension.</summary>
    public static TapeImage Load(string path, ReadOnlySpan<byte> data) =>
        Path.GetExtension(path).Equals(".tzx", StringComparison.OrdinalIgnoreCase)
            ? TzxFile.Parse(data)
            : FromTap(TapFile.Parse(data));
}
