// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tape;

/// <summary>
/// Walks a tape edge by edge, following its loops, jumps and calls. Every block yields at least
/// one edge, blocks without sound an empty one (no transition, no duration), and the last edge
/// of a block is marked. Follows libspectrum (FUSE) so that its tests apply: a block that
/// follows a pause starts with the level low.
/// </summary>
public sealed class TapeCursor(TapeImage tape)
{
    private readonly IReadOnlyList<TapeBlock> _blocks = tape.Blocks;

    private int _block;
    private int _segment;
    private int _index;

    /// <summary>The block after a loop start, or -1 outside a loop.</summary>
    private int _loopStart = -1;
    private int _loopCount;

    /// <summary>The call sequence block being played, or -1.</summary>
    private int _callBlock = -1;
    private int _call;

    /// <summary>Whether the next block starts low: at the start of the tape, and after a pause.</summary>
    private bool _forceLow = true;

    /// <summary>Index of the block being played, or about to be; the block count at the end.</summary>
    public int Block => _block;

    public bool AtEnd => _block >= _blocks.Count;

    public void Rewind()
    {
        _loopStart = -1;
        _callBlock = -1;
        MoveTo(0);
    }

    /// <summary>Goes to the start of block <paramref name="block"/> (the block count for the end).</summary>
    public void MoveTo(int block)
    {
        _block = block;
        _segment = 0;
        _index = 0;
        _forceLow = true;
    }

    /// <summary>The next edge. Not to be called at the end of the tape.</summary>
    public TapeEdge Next()
    {
        var block = _blocks[_block];
        var next = _block + 1;
        var nextStartsLow = false;
        TapeEdge edge;

        switch (block.Kind)
        {
            case TapeBlockKind.Sound:
                if (NextPulse(block, out edge))
                {
                    if (block.HasPause || HasPulseLeft(block))
                    {
                        return FirstEdgeOfBlock(edge, endsBlock: false, ref nextStartsLow);
                    }
                }
                else if (block.HasPause)
                {
                    // The pause starts with an edge that ends the last pulse.
                    edge = block.PauseTStates > 0
                        ? new TapeEdge(block.PauseTStates, TapeTransition.Toggle)
                        : new TapeEdge(0, TapeTransition.None);
                    nextStartsLow = block.PauseTStates > 0;
                }
                else
                {
                    edge = new TapeEdge(0, TapeTransition.None);
                }

                break;

            case TapeBlockKind.Pause:
                edge = block.PauseTStates > 0
                    ? new TapeEdge(block.PauseTStates, TapeTransition.Low)
                    : new TapeEdge(0, TapeTransition.None, TapeEdgeFlags.Stop);
                nextStartsLow = block.PauseTStates > 0;
                break;

            case TapeBlockKind.Stop48:
                edge = new TapeEdge(0, TapeTransition.None, TapeEdgeFlags.Stop48);
                break;

            case TapeBlockKind.SetLevel:
                edge = new TapeEdge(0, block.Value != 0 ? TapeTransition.High : TapeTransition.Low);
                break;

            case TapeBlockKind.LoopStart:
                if (block.Value > 0 && next < _blocks.Count)
                {
                    _loopStart = next;
                    _loopCount = block.Value;
                }

                edge = new TapeEdge(0, TapeTransition.None);
                break;

            case TapeBlockKind.LoopEnd:
                if (_loopStart >= 0)
                {
                    if (--_loopCount > 0)
                    {
                        next = _loopStart;
                    }
                    else
                    {
                        _loopStart = -1;
                    }
                }

                edge = new TapeEdge(0, TapeTransition.None);
                break;

            case TapeBlockKind.Jump:
                // A jump to itself would loop for ever: it goes on to the next block instead.
                next = _block + (block.Value != 0 ? block.Value : 1);
                edge = new TapeEdge(0, TapeTransition.None);
                break;

            case TapeBlockKind.CallSequence:
                if (block.Offsets.Count > 0)
                {
                    _callBlock = _block;
                    _call = 0;
                    next = _block + block.Offsets[0];
                }

                edge = new TapeEdge(0, TapeTransition.None);
                break;

            case TapeBlockKind.Return:
                if (_callBlock >= 0)
                {
                    var calls = _blocks[_callBlock].Offsets;
                    if (++_call < calls.Count)
                    {
                        next = _callBlock + calls[_call];
                    }
                    else
                    {
                        next = _callBlock + 1;
                        _callBlock = -1;
                    }
                }

                edge = new TapeEdge(0, TapeTransition.None);
                break;

            default:
                edge = new TapeEdge(0, TapeTransition.None);
                break;
        }

        edge = FirstEdgeOfBlock(edge, endsBlock: true, ref nextStartsLow);
        var flags = edge.Flags | TapeEdgeFlags.EndOfBlock;

        if (next < 0 || next >= _blocks.Count)
        {
            next = _blocks.Count;
            flags |= TapeEdgeFlags.EndOfTape;

            // The tape ends with an edge, which ends the last pulse: a loader that reads it
            // would otherwise miss the last bit of a block that has no pause after it.
            if (edge.Transition == TapeTransition.None)
            {
                edge = edge with { Transition = TapeTransition.Toggle };
            }
        }

        MoveTo(next);
        _forceLow = nextStartsLow;
        return edge with { Flags = flags };
    }

    /// <summary>
    /// Forces the first edge of a block low when a pause came before. A block without sound
    /// passes that on to the next one.
    /// </summary>
    private TapeEdge FirstEdgeOfBlock(TapeEdge edge, bool endsBlock, ref bool nextStartsLow)
    {
        if (_forceLow)
        {
            if (edge.Transition == TapeTransition.Toggle)
            {
                edge = edge with { Transition = TapeTransition.Low };
            }
            else if (endsBlock && edge.Transition == TapeTransition.None && edge.Duration == 0)
            {
                nextStartsLow = true;
            }
        }

        _forceLow = nextStartsLow;
        return edge;
    }

    /// <summary>Takes the next pulse of a sound block, if any is left.</summary>
    private bool NextPulse(TapeBlock block, out TapeEdge edge)
    {
        if (SkipEmptySegments(block))
        {
            edge = block.Segments[_segment][_index++];
            return true;
        }

        edge = default;
        return false;
    }

    private bool HasPulseLeft(TapeBlock block) => SkipEmptySegments(block);

    /// <summary>Moves to the next pulse to play; false when the pulses are all played.</summary>
    private bool SkipEmptySegments(TapeBlock block)
    {
        while (_segment < block.Segments.Length && _index >= block.Segments[_segment].Count)
        {
            _segment++;
            _index = 0;
        }

        return _segment < block.Segments.Length;
    }
}
