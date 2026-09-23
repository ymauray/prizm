// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// When the 48K's ULA stalls the CPU. While it draws the 192 screen lines, the ULA reads the
/// screen memory during the first 128 T-states of each 224 T-state line, and a CPU cycle on
/// 0x4000-0x7FFF (or a contended I/O point) must wait: 6, 5, 4, 3, 2, 1, 0, 0 T-states depending
/// on its position in each group of 8. The first such cycle is at T-state 14335.
/// </summary>
/// <remarks>Timings: FUSE, and the "Contended memory" page of the Sinclair Wiki.</remarks>
public static class Contention48K
{
    public const int FirstContendedTState = 14335;

    private static readonly byte[] Pattern = [6, 5, 4, 3, 2, 1, 0, 0];

    private static readonly byte[] Delays = BuildTable();

    /// <summary>Delay for a contended cycle starting at <paramref name="tStates"/> within a frame.</summary>
    public static int Delay(long tStates) =>
        tStates >= 0 && tStates < Delays.Length ? Delays[tStates] : 0;

    public static bool IsContended(ushort address) => (address & 0xC000) == 0x4000;

    private static byte[] BuildTable()
    {
        var table = new byte[Spectrum48.FrameTStates];
        for (var line = 0; line < ScreenLayout.Height; line++)
        {
            var start = FirstContendedTState + (line * Ula.LineTStates);
            for (var t = 0; t < 128; t++)
            {
                table[start + t] = Pattern[t % 8];
            }
        }

        return table;
    }
}
