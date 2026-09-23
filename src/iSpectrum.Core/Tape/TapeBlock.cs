// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

public enum TapeBlockKind
{
    /// <summary>A block that makes a sound: data, tone, pulses, samples.</summary>
    Sound,

    /// <summary>Silence, the signal held low; a pause of 0 stops the tape.</summary>
    Pause,

    /// <summary>Stops the tape on a 48K machine.</summary>
    Stop48,

    /// <summary>Sets the level of the signal (TZX block 0x2B).</summary>
    SetLevel,

    /// <summary>The blocks up to the next <see cref="LoopEnd"/> are played <see cref="TapeBlock.Value"/> times.</summary>
    LoopStart,

    LoopEnd,

    /// <summary>Playing goes on <see cref="TapeBlock.Value"/> blocks away (relative, may be negative).</summary>
    Jump,

    /// <summary>Plays the sequences found at each of <see cref="TapeBlock.Offsets"/>, each up to a <see cref="Return"/>.</summary>
    CallSequence,

    Return,

    /// <summary>
    /// A menu for the user (TZX block 0x28): each of <see cref="TapeBlock.Texts"/> goes on
    /// at the matching one of <see cref="TapeBlock.Offsets"/>. The deck stops there and waits.
    /// </summary>
    Select,

    /// <summary>Text, groups and other information: nothing to play.</summary>
    Info,
}

/// <summary>One block of a tape, from a .TAP or a .TZX file.</summary>
public sealed class TapeBlock
{
    public TapeBlockKind Kind { get; init; }

    /// <summary>The TZX block ID (0x10 for the blocks of a .TAP file).</summary>
    public byte Id { get; init; }

    /// <summary>
    /// For sound blocks, the pause after the sound (T-states, 0 for none), which starts with an
    /// edge that ends the last pulse; for pauses, their length.
    /// </summary>
    public int PauseTStates { get; init; }

    /// <summary>Whether this sound block ends with a pause (possibly of 0), as data blocks do; tones and pulses do not.</summary>
    public bool HasPause { get; init; }

    /// <summary>Loop count, jump offset, or level (1 for high) of a set-level block.</summary>
    public int Value { get; init; }

    /// <summary>The offsets of a call sequence or a select block, relative to the block.</summary>
    public IReadOnlyList<int> Offsets { get; init; } = [];

    /// <summary>The choices of a select block, one for each offset.</summary>
    public IReadOnlyList<string> Texts { get; init; } = [];

    /// <summary>
    /// The bytes of a block the ROM's LD-BYTES can read (flag, data, checksum), when it is sent
    /// at the ROM's speed: what fast loading copies into memory. Null for any other block.
    /// </summary>
    public byte[]? RomData { get; init; }

    internal PulseSegment[] Segments { get; init; } = [];

    /// <summary>A standard-speed data block, as the ROM saves it, followed by a pause.</summary>
    public static TapeBlock Standard(byte[] data, int pauseTStates) => new()
    {
        Kind = TapeBlockKind.Sound,
        Id = 0x10,
        HasPause = true,
        PauseTStates = pauseTStates,
        RomData = data,
        Segments =
        [
            // Headers have a flag byte below 0x80 and a longer pilot tone.
            new ToneSegment(TapePlayer.PilotPulse, data.Length > 0 && data[0] < 0x80 ? TapePlayer.HeaderPilotPulses : TapePlayer.DataPilotPulses),
            new EdgeListSegment([new(TapePlayer.FirstSyncPulse, TapeTransition.Toggle), new(TapePlayer.SecondSyncPulse, TapeTransition.Toggle)]),
            new DataSegment(data, data.Length * 8, TapePlayer.ZeroPulse, TapePlayer.OnePulse),
        ],
    };
}
