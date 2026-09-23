// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using System.Diagnostics.CodeAnalysis;

namespace Prizm.Core.Tape;

/// <summary>
/// Where the blocks the ROM saves end up: SA-BYTES is intercepted, and each block arrives whole
/// (flag, data, checksum, as a .TAP stores it). The front-end takes them to write them to a file.
/// </summary>
public sealed class TapeRecorder
{
    private readonly Queue<byte[]> _blocks = new();

    /// <summary>Blocks saved and not yet taken.</summary>
    public int Pending => _blocks.Count;

    /// <summary>Takes the oldest block saved, if any.</summary>
    public bool TryTake([NotNullWhen(true)] out byte[]? block) => _blocks.TryDequeue(out block);

    internal void Record(byte[] block) => _blocks.Enqueue(block);
}
