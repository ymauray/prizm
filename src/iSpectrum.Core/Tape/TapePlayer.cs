// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

/// <summary>
/// Plays a tape as the signal the Spectrum reads on its EAR input, with the pulse lengths the ROM
/// saves: a pilot tone, two sync pulses, then two pulses per bit (most significant bit first),
/// and a one-second pause after each block. Each pulse ends with an edge (the level flips).
/// </summary>
/// <remarks>Timings: The Complete Spectrum ROM Disassembly, and the TZX format description.</remarks>
public sealed class TapePlayer
{
    public const int PilotPulse = 2168;
    public const int HeaderPilotPulses = 8063;
    public const int DataPilotPulses = 3223;
    public const int FirstSyncPulse = 667;
    public const int SecondSyncPulse = 735;
    public const int ZeroPulse = 855;
    public const int OnePulse = 1710;
    public const int PauseTStates = 3_500_000;

    private enum Phase
    {
        Stopped,
        Pilot,
        FirstSync,
        SecondSync,
        Data,
        Pause,
    }

    private IReadOnlyList<byte[]> _blocks = [];
    private Phase _phase;
    private int _block;
    private int _pulsesLeft;
    private int _byte;
    private int _bitMask;
    private bool _secondHalf;

    /// <summary>T-state (from the start of the current frame) of the next edge.</summary>
    private long _nextEdge;

    /// <summary>The level on the EAR input.</summary>
    public bool Level { get; private set; }

    public bool IsPlaying => _phase != Phase.Stopped;

    public int BlockCount => _blocks.Count;

    /// <summary>Index of the block being played, or about to be; equal to BlockCount at the end.</summary>
    public int CurrentBlock => _block;

    public bool AtEnd => _block >= _blocks.Count;

    public void Insert(TapFile tape)
    {
        _blocks = tape.Blocks;
        Rewind();
    }

    public void Eject()
    {
        _blocks = [];
        Rewind();
    }

    public void Rewind()
    {
        _phase = Phase.Stopped;
        _block = 0;
        Level = false;
    }

    /// <summary>Starts playing the current block at <paramref name="now"/> (a T-state of the current frame).</summary>
    public void Play(long now)
    {
        if (!IsPlaying && !AtEnd)
        {
            StartBlock(now);
        }
    }

    public void Stop() => _phase = Phase.Stopped;

    /// <summary>
    /// Takes the current block whole, without playing it, and moves to the next one: for fast
    /// loading, which copies blocks straight into memory.
    /// </summary>
    public byte[]? TakeBlock()
    {
        if (AtEnd)
        {
            return null;
        }

        _phase = Phase.Stopped;
        return _blocks[_block++];
    }

    /// <summary>Plays the tape up to <paramref name="time"/>, telling the beeper about each edge.</summary>
    public void AdvanceTo(long time, Beeper beeper)
    {
        while (IsPlaying && _nextEdge <= time)
        {
            var edge = _nextEdge;

            if (_phase == Phase.Pause)
            {
                _block++;
                if (AtEnd)
                {
                    _phase = Phase.Stopped;
                }
                else
                {
                    StartBlock(edge);
                }

                continue;
            }

            Level = !Level;
            beeper.SetTapeLevel(edge, Level);
            ScheduleNextEdge(edge);
        }
    }

    /// <summary>Makes the next edge relative to the next frame.</summary>
    public void EndFrame(int frameTStates)
    {
        if (IsPlaying)
        {
            _nextEdge -= frameTStates;
        }
    }

    private void StartBlock(long now)
    {
        var block = _blocks[_block];
        if (block.Length == 0)
        {
            _phase = Phase.Pause;
            _nextEdge = now + PauseTStates;
            return;
        }

        // Headers have a flag byte below 0x80 and a longer pilot tone.
        _phase = Phase.Pilot;
        _pulsesLeft = block[0] < 0x80 ? HeaderPilotPulses : DataPilotPulses;
        _nextEdge = now + PilotPulse;
    }

    /// <summary>Called on each edge, which ends a pulse: schedules the end of the next one.</summary>
    private void ScheduleNextEdge(long edge)
    {
        switch (_phase)
        {
            case Phase.Pilot:
                if (--_pulsesLeft > 0)
                {
                    _nextEdge = edge + PilotPulse;
                }
                else
                {
                    _phase = Phase.FirstSync;
                    _nextEdge = edge + FirstSyncPulse;
                }

                break;

            case Phase.FirstSync:
                _phase = Phase.SecondSync;
                _nextEdge = edge + SecondSyncPulse;
                break;

            case Phase.SecondSync:
                _phase = Phase.Data;
                _byte = 0;
                _bitMask = 0x80;
                _secondHalf = false;
                _nextEdge = edge + BitPulse();
                break;

            default:
                if (!_secondHalf)
                {
                    _secondHalf = true;
                    _nextEdge = edge + BitPulse();
                    break;
                }

                _secondHalf = false;
                _bitMask >>= 1;
                if (_bitMask == 0)
                {
                    _bitMask = 0x80;
                    _byte++;
                }

                if (_byte == _blocks[_block].Length)
                {
                    _phase = Phase.Pause;
                    _nextEdge = edge + PauseTStates;
                }
                else
                {
                    _nextEdge = edge + BitPulse();
                }

                break;
        }
    }

    private int BitPulse() => (_blocks[_block][_byte] & _bitMask) != 0 ? OnePulse : ZeroPulse;
}
