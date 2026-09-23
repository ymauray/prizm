// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Snapshots;

/// <summary>Picks the snapshot format from the file extension.</summary>
public static class Snapshot
{
    /// <summary>Whether the file name has a snapshot extension this emulator can load.</summary>
    public static bool IsSupported(string fileName) => Extension(fileName) is ".sna" or ".z80";

    /// <summary>Whether the snapshot is a 128K one: the machine to load it into must be a <see cref="Spectrum128"/>.</summary>
    public static bool IsSpectrum128(string fileName, ReadOnlySpan<byte> data) => Extension(fileName) switch
    {
        ".sna" => SnaFormat.IsSpectrum128(data),
        ".z80" => Z80Format.IsSpectrum128(data),
        _ => false,
    };

    /// <summary>
    /// Loads a snapshot into a machine of its model (see <see cref="IsSpectrum128"/>); throws if the
    /// data is invalid or unsupported.
    /// </summary>
    public static void Load(Spectrum spectrum, string fileName, ReadOnlySpan<byte> data)
    {
        switch (Extension(fileName))
        {
            case ".sna":
                SnaFormat.Load(spectrum, data);
                break;
            case ".z80":
                Z80Format.Load(spectrum, data);
                break;
            default:
                throw new NotSupportedException($"Unknown snapshot type: {Path.GetFileName(fileName)}");
        }
    }

    private static string Extension(string fileName) => Path.GetExtension(fileName).ToLowerInvariant();
}
