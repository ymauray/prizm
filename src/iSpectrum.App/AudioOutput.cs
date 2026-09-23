// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core;
using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// Plays the beeper through a Raylib audio stream. Samples wait in a ring buffer and go to the
/// stream in fixed chunks whenever it has played one. The main loop reads <see cref="Queued"/> to
/// decide how many frames to run, so the sound card, not a timer, sets the emulation speed.
/// </summary>
internal sealed class AudioOutput : IDisposable
{
    /// <summary>Samples per stream buffer: Raylib double-buffers, so about 46 ms are in flight.</summary>
    private const int ChunkLength = 1024;

    /// <summary>How much sound to keep waiting in the ring, beyond what the stream holds.</summary>
    public const int TargetQueued = 2 * ChunkLength;

    private const int RingLength = 8192;

    private readonly short[] _ring = new short[RingLength];
    private readonly short[] _chunk = new short[ChunkLength];
    private readonly AudioStream _stream;
    private int _head;
    private int _count;

    public AudioOutput()
    {
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady())
        {
            return;
        }

        Raylib.SetAudioStreamBufferSizeDefault(ChunkLength);
        _stream = Raylib.LoadAudioStream(Beeper.SampleRate, 16, 1);
        Raylib.PlayAudioStream(_stream);
        IsReady = true;
    }

    /// <summary>False when no audio device could be opened; the App then paces itself with a timer.</summary>
    public bool IsReady { get; }

    /// <summary>Samples waiting in the ring buffer.</summary>
    public int Queued => _count;

    public void Write(ReadOnlySpan<short> samples)
    {
        foreach (var sample in samples)
        {
            if (_count == RingLength)
            {
                // Should not happen while the main loop watches Queued: drop the oldest sample.
                _head = (_head + 1) % RingLength;
                _count--;
            }

            _ring[(_head + _count) % RingLength] = sample;
            _count++;
        }
    }

    /// <summary>Refills every stream buffer that has been played; silence covers a shortage.</summary>
    public void Pump()
    {
        if (!IsReady)
        {
            return;
        }

        while (Raylib.IsAudioStreamProcessed(_stream))
        {
            for (var i = 0; i < ChunkLength; i++)
            {
                if (_count > 0)
                {
                    _chunk[i] = _ring[_head];
                    _head = (_head + 1) % RingLength;
                    _count--;
                }
                else
                {
                    _chunk[i] = 0;
                }
            }

            Raylib.UpdateAudioStream<short>(_stream, _chunk, ChunkLength);
        }
    }

    public void Dispose()
    {
        if (IsReady)
        {
            Raylib.UnloadAudioStream(_stream);
        }

        Raylib.CloseAudioDevice();
    }
}
