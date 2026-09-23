// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

/// <summary>
/// A run of edges inside a block (a pilot tone, sync pulses, data bits...), read by index so
/// that playing a tape never allocates.
/// </summary>
internal abstract class PulseSegment
{
    public abstract int Count { get; }

    public abstract TapeEdge this[int index] { get; }
}

/// <summary><paramref name="count"/> pulses of the same length: a pilot tone.</summary>
internal sealed class ToneSegment(int length, int count) : PulseSegment
{
    public override int Count => count;

    public override TapeEdge this[int index] => new(length, TapeTransition.Toggle);
}

/// <summary>Edges given one by one: sync pulses, and blocks decoded when the tape is read.</summary>
internal sealed class EdgeListSegment(TapeEdge[] edges) : PulseSegment
{
    public override int Count => edges.Length;

    public override TapeEdge this[int index] => edges[index];
}

/// <summary>
/// Data bits, most significant bit first, each sent as two pulses of the same length: the
/// encoding of the ROM, and of TZX turbo and pure data blocks.
/// </summary>
internal sealed class DataSegment(byte[] data, int bitCount, int zeroPulse, int onePulse) : PulseSegment
{
    public override int Count => bitCount * 2;

    public override TapeEdge this[int index]
    {
        get
        {
            var bit = index >> 1;
            var set = (data[bit >> 3] & (0x80 >> (bit & 7))) != 0;
            return new TapeEdge(set ? onePulse : zeroPulse, TapeTransition.Toggle);
        }
    }
}
