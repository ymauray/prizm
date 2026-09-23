// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>
/// The timing of a Spectrum model: its clock, its frame and scan lines, where the screen starts
/// and when the ULA stalls the CPU (contention).
/// </summary>
/// <remarks>Values from FUSE and the Sinclair Wiki ("Contended memory", "ZX Spectrum 128").</remarks>
public sealed class SpectrumTimings
{
    /// <summary>
    /// The ULA's delay pattern, repeated every 8 T-states while it reads the screen. It must come
    /// before the models below: static fields are set in order, and the models use it.
    /// </summary>
    private static readonly byte[] Pattern = [6, 5, 4, 3, 2, 1, 0, 0];

    /// <summary>ZX Spectrum 48K: 3.5 MHz, 312 lines of 224 T-states.</summary>
    public static readonly SpectrumTimings Spectrum48 = new(
        clockRate: 3_500_000,
        frameTStates: 69888,
        lineTStates: 224,
        firstContendedTState: 14335,
        firstPixelTState: 14336,
        interruptLength: 32);

    /// <summary>
    /// ZX Spectrum 128K: 3.5469 MHz, 311 lines of 228 T-states. FUSE returns the first floating
    /// bus byte 26 T-states later than on the 48K (14364 against 14338), so the screen starts 26
    /// T-states later too; contention starts 1 T-state before the first pixel on both models.
    /// </summary>
    public static readonly SpectrumTimings Spectrum128 = new(
        clockRate: 3_546_900,
        frameTStates: 70908,
        lineTStates: 228,
        firstContendedTState: 14361,
        firstPixelTState: 14362,
        interruptLength: 36);

    private readonly byte[] _contention;

    private SpectrumTimings(
        int clockRate, int frameTStates, int lineTStates, int firstContendedTState, int firstPixelTState, int interruptLength)
    {
        ClockRate = clockRate;
        FrameTStates = frameTStates;
        LineTStates = lineTStates;
        FirstContendedTState = firstContendedTState;
        FirstPixelTState = firstPixelTState;
        InterruptLength = interruptLength;
        _contention = BuildContentionTable();
    }

    /// <summary>T-states per second.</summary>
    public int ClockRate { get; }

    /// <summary>T-states from one interrupt to the next.</summary>
    public int FrameTStates { get; }

    /// <summary>T-states per scan line; the beam draws 2 pixels per T-state.</summary>
    public int LineTStates { get; }

    /// <summary>The first T-state at which the ULA stalls the CPU (start of the first screen line).</summary>
    public int FirstContendedTState { get; }

    /// <summary>The T-state at which the beam draws the first pixel of the screen area.</summary>
    public int FirstPixelTState { get; }

    /// <summary>T-states during which the ULA holds INT at the start of each frame.</summary>
    public int InterruptLength { get; }

    /// <summary>
    /// Delay for a contended cycle starting at <paramref name="tStates"/> within a frame: while it
    /// draws the 192 screen lines, the ULA reads the screen memory during the first 128 T-states
    /// of each line, and a contended CPU cycle waits 6, 5, 4, 3, 2, 1, 0 or 0 T-states depending
    /// on its position in each group of 8.
    /// </summary>
    public int ContentionDelay(long tStates) =>
        tStates >= 0 && tStates < _contention.Length ? _contention[tStates] : 0;

    private byte[] BuildContentionTable()
    {
        var table = new byte[FrameTStates];
        for (var line = 0; line < ScreenLayout.Height; line++)
        {
            var start = FirstContendedTState + (line * LineTStates);
            for (var t = 0; t < 128; t++)
            {
                table[start + t] = Pattern[t % 8];
            }
        }

        return table;
    }
}
