// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using iSpectrum.Core;
using Raylib_cs;

namespace iSpectrum.App;

/// <summary>
/// Plays the beeper through a Raylib audio stream. Samples wait in a ring buffer; the sound card
/// takes them from its own thread, through a callback, as it needs them, so the only delay is the
/// sound waiting in the ring. The main loop reads <see cref="Queued"/> to decide how many frames
/// to run, so the sound card, not a timer, sets the emulation speed.
/// </summary>
internal sealed class AudioOutput : IDisposable
{
    /// <summary>
    /// How much sound the main loop keeps waiting in the ring, about 23 ms. It must outlast the
    /// gap between two redraws (16.7 ms at 60 Hz) with some margin, or the sound breaks up.
    /// </summary>
    public const int TargetQueued = 1024;

    private const int RingLength = 8192;

    /// <summary>The one instance, for the callback, which Raylib calls without any context.</summary>
    private static AudioOutput? s_current;

    private readonly short[] _ring = new short[RingLength];
    private readonly AudioStream _stream;

    // Single producer (main thread) and single consumer (audio thread): each counter is written
    // by one thread only and only grows; the ring index is the counter modulo RingLength.
    private int _written;
    private int _read;

    // The last sample played, repeated when the ring runs dry so that a shortage does not click.
    private short _last;

    public unsafe AudioOutput()
    {
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady())
        {
            return;
        }

        s_current = this;
        _stream = Raylib.LoadAudioStream(Beeper.SampleRate, 16, 1);
        Raylib.SetAudioStreamCallback(_stream, &Fill);
        Raylib.PlayAudioStream(_stream);
        IsReady = true;
    }

    /// <summary>False when no audio device could be opened; the App then paces itself with a timer.</summary>
    public bool IsReady { get; }

    /// <summary>Samples waiting in the ring buffer.</summary>
    public int Queued => Volatile.Read(ref _written) - Volatile.Read(ref _read);

    public void Write(ReadOnlySpan<short> samples)
    {
        var written = _written;
        var free = RingLength - (written - Volatile.Read(ref _read));

        // Should not happen while the main loop watches Queued: drop what does not fit.
        var count = Math.Min(samples.Length, free);
        for (var i = 0; i < count; i++)
        {
            _ring[(written + i) & (RingLength - 1)] = samples[i];
        }

        Volatile.Write(ref _written, written + count);
    }

    public void Dispose()
    {
        if (IsReady)
        {
            Raylib.UnloadAudioStream(_stream);
            s_current = null;
        }

        Raylib.CloseAudioDevice();
    }

    /// <summary>Called on the audio thread: copies <paramref name="frames"/> samples out of the ring.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Fill(void* buffer, uint frames)
    {
        var output = new Span<short>(buffer, (int)frames);
        var self = s_current;
        if (self is null)
        {
            output.Clear();
            return;
        }

        var read = self._read;
        var available = Volatile.Read(ref self._written) - read;
        var count = Math.Min(output.Length, available);
        for (var i = 0; i < count; i++)
        {
            output[i] = self._ring[(read + i) & (RingLength - 1)];
        }

        if (count > 0)
        {
            self._last = output[count - 1];
        }

        output[count..].Fill(self._last);
        Volatile.Write(ref self._read, read + count);
    }
}
