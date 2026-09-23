// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>
/// Types key strokes on the machine, one per 10 frames: each is held for 5 frames, long enough
/// for the ROM, which scans the keyboard once per frame, then released for 5, short of its
/// auto-repeat delay. Used to type LOAD "" when a tape is inserted, and characters that take
/// more than one stroke.
/// </summary>
public sealed class AutoTyper
{
    private const int HoldFrames = 5;
    private const int ReleaseFrames = 5;

    private SpectrumKey[][] _strokes = [];
    private int _stroke;
    private int _frame;
    private int _delay;

    /// <summary>LOAD "": J is LOAD in keyword mode, Symbol Shift + P is the quote.</summary>
    public static SpectrumKey[][] LoadCommand { get; } =
    [
        [SpectrumKey.J],
        [SpectrumKey.SymbolShift, SpectrumKey.P],
        [SpectrumKey.SymbolShift, SpectrumKey.P],
        [SpectrumKey.Enter],
    ];

    public bool IsBusy => _stroke < _strokes.Length;

    /// <summary>Starts typing <paramref name="strokes"/> after <paramref name="delayFrames"/> frames.</summary>
    public void Start(SpectrumKey[][] strokes, int delayFrames)
    {
        _strokes = strokes;
        _stroke = 0;
        _frame = 0;
        _delay = delayFrames;
    }

    /// <summary>Types <paramref name="strokes"/> after those still to type, or at once if there are none.</summary>
    public void Append(SpectrumKey[][] strokes)
    {
        if (!IsBusy)
        {
            Start(strokes, 0);
            return;
        }

        _strokes = [.. _strokes.AsSpan(_stroke), .. strokes];
        _stroke = 0;
    }

    /// <summary>Sets the typed keys for the frame about to run.</summary>
    public void NextFrame(Keyboard keyboard)
    {
        keyboard.ReleaseTyped();

        if (_delay > 0)
        {
            _delay--;
            return;
        }

        if (!IsBusy)
        {
            return;
        }

        if (_frame < HoldFrames)
        {
            foreach (var key in _strokes[_stroke])
            {
                keyboard.PressTyped(key);
            }
        }

        if (++_frame == HoldFrames + ReleaseFrames)
        {
            _frame = 0;
            _stroke++;
        }
    }
}
