// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

/// <summary>
/// The tape deck: plays a tape as the signal the Spectrum reads on its EAR input. The ROM saves
/// a pilot tone, two sync pulses, then two pulses per bit (most significant bit first); a .TAP
/// file adds a one-second pause after each block, a .TZX file gives its own timings.
/// </summary>
/// <remarks>Timings: The Complete Spectrum ROM Disassembly, and the TZX format description.</remarks>
public sealed class TapePlayer : ISoundSource
{
    public const int PilotPulse = 2168;
    public const int HeaderPilotPulses = 8063;
    public const int DataPilotPulses = 3223;
    public const int FirstSyncPulse = 667;
    public const int SecondSyncPulse = 735;
    public const int ZeroPulse = 855;
    public const int OnePulse = 1710;
    public const int PauseTStates = 3_500_000;

    /// <summary>The clock tape timings are given in (TZX, CSW): the 48K's.</summary>
    public const int ClockHz = 3_500_000;

    /// <summary>
    /// Edges without duration in a row beyond which the tape is stopped: a tape whose loops or
    /// jumps go round without ever making a sound would otherwise hang the emulator.
    /// </summary>
    private const int MaxSilentEdges = 100_000;

    /// <summary>How long the last level is held when the tape stops by itself (1 ms).</summary>
    private const int StopHold = 3500;

    // Loader detection, after FUSE (loader.c): reads of the ULA port this close together
    // (T-states), each with B one more or one less than the last, in a row.
    private const int LoaderReadInterval = 500;
    private const int LoaderReads = 10;

    private TapeImage _tape = new([]);
    private TapeCursor _cursor = new(new([]));
    private bool _playing;

    /// <summary>Set by an edge after which the deck stops (end of tape, stop block).</summary>
    private bool _stopAfterEdge;

    /// <summary>T-state (from the start of the current frame) of the next edge.</summary>
    private long _nextEdge;

    /// <summary>The last read of the ULA port (T-state of the current frame) and B at that time.</summary>
    private long _lastRead = -100_000;
    private byte _lastB;
    private int _loaderReads;

    /// <summary>The level on the EAR input.</summary>
    public bool Level { get; private set; }

    public bool IsPlaying => _playing;

    public int BlockCount => _tape.Blocks.Count;

    /// <summary>Index of the block being played, or about to be; equal to BlockCount at the end.</summary>
    public int CurrentBlock => _cursor.Block;

    public bool AtEnd => _cursor.AtEnd;

    /// <summary>
    /// The select block (TZX 0x28) the deck has stopped at, waiting for the user's choice
    /// (<see cref="Choose"/> or <see cref="SkipChoice"/>); null otherwise. The deck does not play
    /// until then.
    /// </summary>
    public TapeBlock? PendingSelect { get; private set; }

    /// <summary>Whether the machine is a 48K, on which the TZX block "stop the tape if in 48K mode" stops the tape.</summary>
    public bool Is48K { get; set; } = true;

    public void Insert(TapFile tape) => Insert(TapeImage.FromTap(tape));

    public void Insert(TapeImage tape)
    {
        _tape = tape;
        _cursor = new TapeCursor(tape);
        Rewind();
    }

    public void Eject() => Insert(new TapeImage([]));

    public void Rewind()
    {
        PendingSelect = null;
        _playing = false;
        _stopAfterEdge = false;
        _loaderReads = 0;
        _cursor.Rewind();
        Level = false;
    }

    /// <summary>Starts playing the current block at <paramref name="now"/> (a T-state of the current frame).</summary>
    public void Play(long now)
    {
        if (!_playing && !AtEnd && PendingSelect is null)
        {
            _playing = true;
            _stopAfterEdge = false;
            _nextEdge = now;
        }
    }

    public void Stop() => _playing = false;

    /// <summary>Goes on at the block the choice <paramref name="choice"/> of the pending select block leads to.</summary>
    public void Choose(int choice)
    {
        if (PendingSelect is not { } select)
        {
            return;
        }

        PendingSelect = null;
        _cursor.MoveTo(Math.Clamp(CurrentBlock + select.Offsets[choice], 0, BlockCount));
    }

    /// <summary>Makes no choice: goes on at the block after the pending select block.</summary>
    public void SkipChoice()
    {
        if (PendingSelect is not null)
        {
            PendingSelect = null;
            _cursor.MoveTo(CurrentBlock + 1);
        }
    }

    /// <summary>
    /// Called on each read of the ULA port, with the CPU's B register: starts the tape when a
    /// program reads it as loaders do, in a tight loop that counts in B, as the ROM's own
    /// LD-EDGE. Turbo loaders start this way, as a user would press "play" for them. The tape
    /// is not stopped when the reads stop: loaders that count elsewhere would lose it.
    /// </summary>
    public void OnPortRead(long now, byte b)
    {
        var interval = now - _lastRead;
        var step = (byte)(b - _lastB);
        _lastRead = now;
        _lastB = b;

        if (_playing || AtEnd || interval > LoaderReadInterval || (step != 1 && step != 0xFF))
        {
            _loaderReads = 0;
        }
        else if (++_loaderReads >= LoaderReads)
        {
            Play(now);
        }
    }

    /// <summary>
    /// For fast loading, which copies blocks straight into memory: takes the next block whole,
    /// without playing it, if the ROM can read it (a data block at the ROM's speed), passing
    /// over blocks without sound. Returns null at the end of the tape, or before any other block
    /// (a turbo loader's), which is left to be played.
    /// </summary>
    public byte[]? TakeBlock()
    {
        while (!AtEnd && _tape.Blocks[CurrentBlock].Kind is TapeBlockKind.Info or TapeBlockKind.Pause or TapeBlockKind.Stop48 or TapeBlockKind.SetLevel)
        {
            _cursor.MoveTo(CurrentBlock + 1);
        }

        if (AtEnd || _tape.Blocks[CurrentBlock].RomData is not { } data)
        {
            return null;
        }

        _playing = false;
        _cursor.MoveTo(CurrentBlock + 1);
        return data;
    }

    /// <summary>Plays the tape up to <paramref name="time"/>, telling the beeper about each change of level.</summary>
    public void AdvanceTo(long time, Beeper beeper)
    {
        var silentEdges = 0;
        while (_playing && _nextEdge <= time)
        {
            if (_stopAfterEdge || AtEnd)
            {
                _playing = false;
                _stopAfterEdge = false;
                break;
            }

            if (_tape.Blocks[CurrentBlock] is { Kind: TapeBlockKind.Select } select)
            {
                // A menu with no choice would never go on.
                if (select.Offsets.Count == 0)
                {
                    _cursor.MoveTo(CurrentBlock + 1);
                    continue;
                }

                _playing = false;
                PendingSelect = select;
                break;
            }

            var edge = _cursor.Next();
            var level = edge.Transition switch
            {
                TapeTransition.Toggle => !Level,
                TapeTransition.Low => false,
                TapeTransition.High => true,
                _ => Level,
            };

            if (level != Level)
            {
                Level = level;
                beeper.SetTapeLevel(_nextEdge, level);
            }

            _stopAfterEdge = (edge.Flags & (TapeEdgeFlags.Stop | TapeEdgeFlags.EndOfTape)) != 0
                || (Is48K && (edge.Flags & TapeEdgeFlags.Stop48) != 0);

            // Once stopped, EAR no longer reads the tape: the last level is held a moment first,
            // so that a loader sees the edge that ends the last pulse.
            _nextEdge += _stopAfterEdge ? Math.Max(edge.Duration, StopHold) : edge.Duration;

            silentEdges = edge.Duration == 0 ? silentEdges + 1 : 0;
            if (silentEdges > MaxSilentEdges)
            {
                _playing = false;
            }
        }
    }

    /// <summary>Makes the next edge, and the last read, relative to the next frame.</summary>
    public void EndFrame(int frameTStates)
    {
        _lastRead -= frameTStates;
        if (_playing)
        {
            _nextEdge -= frameTStates;
        }
    }
}
