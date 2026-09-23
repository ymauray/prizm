// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The speaker, driven by bit 4 of port 0xFE. Level changes are stamped with the CPU's T-state
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

    /// <summary>High-pass coefficient: a time constant of about 200 samples (4.5 ms).</summary>
    private const double DcBlocking = 0.995;

    private readonly short[] _samples = new short[MaxSamplesPerFrame];
    private int _count;

    private bool _level;

    /// <summary>T-state (from the start of the frame) up to which the level has been integrated.</summary>
    private double _time;

    /// <summary>T-state at which the sample being built ends.</summary>
    private double _sampleEnd = TStatesPerSample;

    /// <summary>Time spent at the high level within the sample being built.</summary>
    private double _highTime;

    private double _previousInput;
    private double _previousOutput;

    /// <summary>The samples produced during the last frame, 16-bit signed, mono.</summary>
    public ReadOnlySpan<short> Samples => _samples.AsSpan(0, _count);

    /// <summary>Starts a new frame's sample buffer.</summary>
    public void StartFrame() => _count = 0;

    /// <summary>Records the speaker level set at <paramref name="tStates"/> in the current frame.</summary>
    public void SetLevel(long tStates, bool high)
    {
        Advance(tStates);
        _level = high;
    }

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

        while (time >= _sampleEnd)
        {
            if (_level)
            {
                _highTime += _sampleEnd - _time;
            }

            Emit(_highTime / TStatesPerSample);
            _highTime = 0;
            _time = _sampleEnd;
            _sampleEnd += TStatesPerSample;
        }

        if (_level)
        {
            _highTime += time - _time;
        }

        _time = time;
    }

    /// <summary>Filters and stores one sample; <paramref name="level"/> is the average level, 0 to 1.</summary>
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
