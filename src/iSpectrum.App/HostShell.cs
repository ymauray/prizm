// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.ComponentModel;
using System.Diagnostics;

namespace iSpectrum.App;

/// <summary>Opens a folder in the host's file manager (Finder on macOS).</summary>
internal static class HostShell
{
    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        using var _ = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
}

/// <summary>
/// The host's file chooser, run as a separate process so that the emulator keeps running and
/// redrawing while it is open: AppleScript's "choose file" on macOS, zenity on Linux.
/// </summary>
internal sealed class FileChooser
{
    private Process? _process;

    public bool IsOpen => _process is not null;

    /// <summary>Opens the chooser in <paramref name="folder"/>; false if this host has none.</summary>
    public bool TryOpen(string folder)
    {
        if (_process is not null)
        {
            return true;
        }

        var start = new ProcessStartInfo { RedirectStandardOutput = true, UseShellExecute = false };

        if (OperatingSystem.IsMacOS())
        {
            var location = folder.Replace("\\", "\\\\").Replace("\"", "\\\"");
            start.FileName = "osascript";
            start.ArgumentList.Add("-e");
            start.ArgumentList.Add(
                $"POSIX path of (choose file with prompt \"Choose a snapshot or tape to load\" of type {{\"sna\", \"z80\", \"tap\", \"tzx\"}} " +
                $"default location (POSIX file \"{location}\"))");
        }
        else if (OperatingSystem.IsLinux())
        {
            start.FileName = "zenity";
            start.ArgumentList.Add("--file-selection");
            start.ArgumentList.Add("--file-filter=Snapshots and tapes | *.sna *.SNA *.z80 *.Z80 *.tap *.TAP *.tzx *.TZX");
            start.ArgumentList.Add($"--filename={folder}/");
        }
        else
        {
            return false;
        }

        try
        {
            _process = Process.Start(start);
            return _process is not null;
        }
        catch (Win32Exception)
        {
            // The helper program is not installed.
            return false;
        }
    }

    /// <summary>Once the chooser has closed, returns the chosen path, or null if it was cancelled.</summary>
    public string? PollResult()
    {
        if (_process is null || !_process.HasExited)
        {
            return null;
        }

        var path = _process.StandardOutput.ReadToEnd().Trim();
        var chosen = _process.ExitCode == 0 && path.Length > 0;
        _process.Dispose();
        _process = null;
        return chosen ? path : null;
    }
}
