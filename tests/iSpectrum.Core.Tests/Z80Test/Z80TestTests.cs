// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Text;
using iSpectrum.Core.Tape;
using Xunit.Abstractions;

namespace iSpectrum.Core.Tests.Z80Test;

// One class per program, so that xUnit runs them in parallel: each is 20 to 45 minutes of
// Spectrum time.

[Trait("Category", "Slow")]
public class Z80DocTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80Doc() => new Z80TestHarness(output).Run("z80doc.tap");
}

[Trait("Category", "Slow")]
public class Z80DocFlagsTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80DocFlags() => new Z80TestHarness(output).Run("z80docflags.tap");
}

[Trait("Category", "Slow")]
public class Z80FullTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80Full() => new Z80TestHarness(output).Run("z80full.tap");
}

[Trait("Category", "Slow")]
public class Z80FlagsTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80Flags() => new Z80TestHarness(output).Run("z80flags.tap");
}

[Trait("Category", "Slow")]
public class Z80CcfTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80Ccf() => new Z80TestHarness(output).Run("z80ccf.tap");
}

[Trait("Category", "Slow")]
public class Z80MemptrTests(ITestOutputHelper output)
{
    [Fact]
    public void Z80Memptr() => new Z80TestHarness(output).Run("z80memptr.tap");
}

/// <summary>
/// Loads a z80test tape and reads back what it prints through the ROM's RST 0x10 (see README.md).
/// </summary>
internal sealed class Z80TestHarness(ITestOutputHelper output)
{
    /// <summary>The ROM routine that prints the character in A.</summary>
    private const ushort PrintRoutine = 0x0010;

    /// <summary>SCR-CT: lines left to scroll before the ROM asks "scroll?".</summary>
    private const ushort ScrollCount = 23692;

    /// <summary>The control code for TAB-like positioning: it takes two more bytes.</summary>
    private const byte AtControl = 23;

    /// <summary>An hour of Spectrum time; the longest program takes about 46 minutes.</summary>
    private const int MaxFrames = 50 * 60 * 60;

    private const string Success = "Result: all tests passed.";

    public void Run(string tape)
    {
        var lines = RunProgram(Path.Combine(AppContext.BaseDirectory, "Z80Test", tape));

        foreach (var line in lines)
        {
            if (!line.EndsWith(" OK", StringComparison.Ordinal))
            {
                output.WriteLine(line);
            }
        }

        Assert.Equal(Success, lines.LastOrDefault(line => line.Length > 0));
    }

    private static List<string> RunProgram(string path)
    {
        var spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")))
        {
            FastLoad = true,
            Headless = true,
        };
        spectrum.Tape.Insert(TapFile.Parse(File.ReadAllBytes(path)));
        spectrum.AutoTyper.Start(AutoTyper.LoadCommand, Spectrum48.BootFrames);

        var lines = new List<string>();
        var line = new StringBuilder();
        var skip = 0;
        var frames = 0;

        while (!IsFinished(lines))
        {
            // Once the tape is loaded, everything printed comes from the test program.
            if (spectrum.Cpu.PC == PrintRoutine && spectrum.Tape.AtEnd)
            {
                spectrum.Memory.Write(ScrollCount, 255);
                Collect(spectrum.Cpu.A, line, lines, ref skip);
            }

            var before = spectrum.Cpu.TStates;
            spectrum.Step();
            if (spectrum.Cpu.TStates < before && ++frames > MaxFrames)
            {
                lines.Add($"(no result after {MaxFrames} frames)");
                break;
            }
        }

        return lines;
    }

    private static void Collect(byte character, StringBuilder line, List<string> lines, ref int skip)
    {
        if (skip > 0)
        {
            skip--;
        }
        else if (character == AtControl)
        {
            skip = 2;
        }
        else if (character == 13)
        {
            lines.Add(line.ToString());
            line.Clear();
        }
        else if (character is >= 32 and < 128)
        {
            line.Append((char)character);
        }
    }

    private static bool IsFinished(List<string> lines) =>
        lines.Count > 0 && lines[^1].StartsWith("Result: ", StringComparison.Ordinal);
}
