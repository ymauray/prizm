// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tape;

/// <summary>What happens to the tape signal at the start of an edge.</summary>
public enum TapeTransition : byte
{
    /// <summary>The level flips: an ordinary edge.</summary>
    Toggle,

    /// <summary>The level stays as it is (a block that makes no sound, or a pulse that continues the last one).</summary>
    None,

    /// <summary>The level goes (or stays) low.</summary>
    Low,

    /// <summary>The level goes (or stays) high.</summary>
    High,
}

/// <summary>What else an edge tells the tape deck.</summary>
[Flags]
public enum TapeEdgeFlags : byte
{
    None = 0,

    /// <summary>The last edge of a block.</summary>
    EndOfBlock = 1,

    /// <summary>The deck stops once this edge is over (TZX pause of 0 ms).</summary>
    Stop = 2,

    /// <summary>The deck stops once this edge is over, on a 48K machine only (TZX block 0x2A).</summary>
    Stop48 = 4,

    /// <summary>The last edge of the tape.</summary>
    EndOfTape = 8,
}

/// <summary>
/// One event of the tape signal: a transition, then a level held for <see cref="Duration"/>
/// T-states. The model of libspectrum (FUSE), whose tests describe tapes this way.
/// </summary>
public readonly record struct TapeEdge(int Duration, TapeTransition Transition, TapeEdgeFlags Flags = TapeEdgeFlags.None);
