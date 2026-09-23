// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The speaker, driven by bit 4 of port 0xFE, and the tape signal, which reaches the speaker on
/// the 48K too (that is the loading noise). Level changes are stamped with the CPU's T-state
/// count; each output sample is the average level over its 1/44100 s (about 79 T-states), so
/// fast toggling gives intermediate values instead of aliasing badly. A one-pole high-pass
/// filter removes the constant part, so that the idle level and its changes do not click.
/// </summary>
public sealed class Beeper
{
    public const int SampleRate = 44100;

    /// <summary>The Z80 clock of the 48K, in T-states per second.</summary>
    private const double ClockRate = 3_500_000;

    private const double TStatesPerSample = ClockRate / SampleRate;

    /// <summary>A little over one frame's worth (about 881 samples at 50.08 frames per second).</summary>
    private const int MaxSamplesPerFrame = 1024;

    private const double Amplitude = 8000;

    /// <summary>Loudness of the tape signal relative to the speaker.</summary>
    private const double TapeVolume = 0.5;

    /// <summary>High-pass coefficient: a time constant of about 200 samples (4.5 ms).</summary>
    private const double DcBlocking = 0.995;

    private readonly short[] _samples = new short[MaxSamplesPerFrame];
    private int _count;

    private bool _speaker;
    private bool _tape;

    /// <summary>T-state (from the start of the frame) up to which the level has been integrated.</summary>
    private double _time;

    /// <summary>T-state at which the sample being built ends.</summary>
    private double _sampleEnd = TStatesPerSample;

    /// <summary>Level integrated over time within the sample being built.</summary>
    private double _area;

    private double _previousInput;
    private double _previousOutput;

    /// <summary>
    /// When set, no samples are made: the levels are only followed, so that sound resumes
    /// correctly when it is cleared.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>The samples produced during the last frame, 16-bit signed, mono.</summary>
    public ReadOnlySpan<short> Samples => _samples.AsSpan(0, _count);

    /// <summary>Starts a new frame's sample buffer.</summary>
    public void StartFrame() => _count = 0;

    /// <summary>Records the speaker level set at <paramref name="tStates"/> in the current frame.</summary>
    public void SetLevel(long tStates, bool high)
    {
        Advance(tStates);
        _speaker = high;
    }

    /// <summary>Records the tape signal level at <paramref name="tStates"/> in the current frame.</summary>
    public void SetTapeLevel(long tStates, bool high)
    {
        Advance(tStates);
        _tape = high;
    }

    private double Level => (_speaker ? 1 : 0) + (_tape ? TapeVolume : 0);

    /// <summary>
    /// Produces the samples up to the end of the frame, then makes the times relative to the next
    /// frame. The last instruction may have run past the end; that part carries over.
    /// </summary>
    public void EndFrame(int frameTStates)
    {
        Advance(frameTStates);
        _time -= frameTStates;
        _sampleEnd -= frameTStates;
    }

    private void Advance(double time)
    {
        if (time <= _time)
        {
            return;
        }

        if (Muted)
        {
            // Skip whole samples: only keep the sample boundaries where they would be.
            if (time >= _sampleEnd)
            {
                _sampleEnd += (Math.Floor((time - _sampleEnd) / TStatesPerSample) + 1) * TStatesPerSample;
                _area = 0;
            }

            _time = time;
            return;
        }

        while (time >= _sampleEnd)
        {
            _area += Level * (_sampleEnd - _time);
            Emit(_area / TStatesPerSample);
            _area = 0;
            _time = _sampleEnd;
            _sampleEnd += TStatesPerSample;
        }

        _area += Level * (time - _time);
        _time = time;
    }

    /// <summary>Filters and stores one sample; <paramref name="level"/> is the average level.</summary>
    private void Emit(double level)
    {
        var input = level * Amplitude;
        var output = input - _previousInput + (DcBlocking * _previousOutput);
        _previousInput = input;
        _previousOutput = output;

        if (_count < _samples.Length)
        {
            _samples[_count++] = (short)Math.Clamp(Math.Round(output), short.MinValue, short.MaxValue);
        }
    }
}
