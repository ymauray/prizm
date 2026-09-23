// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Globalization;

namespace iSpectrum.Z80.Tests.Fuse;

/// <summary>CPU state line pair shared by tests.in and tests.expected.</summary>
public sealed record FuseCpuState(
    ushort AF, ushort BC, ushort DE, ushort HL,
    ushort AF_, ushort BC_, ushort DE_, ushort HL_,
    ushort IX, ushort IY, ushort SP, ushort PC, ushort MEMPTR,
    byte I, byte R, bool IFF1, bool IFF2, int IM, bool Halted, int TStates);

/// <summary>A run of bytes starting at <see cref="Address"/>.</summary>
public sealed record FuseMemoryBlock(ushort Address, byte[] Bytes);

/// <summary>Bus event from tests.expected: MR, MW, MC, PR, PW or PC. <see cref="Data"/> is null for contentions.</summary>
public sealed record FuseEvent(int Time, string Type, ushort Address, byte? Data);

public sealed record FuseInput(string Name, FuseCpuState State, IReadOnlyList<FuseMemoryBlock> Memory);

public sealed record FuseExpected(
    string Name, IReadOnlyList<FuseEvent> Events, FuseCpuState State, IReadOnlyList<FuseMemoryBlock> ChangedMemory);

/// <summary>Parser for the FUSE Z80 test files. The format is described in FORMAT.txt.</summary>
public static class FuseTestFile
{
    public static List<FuseInput> ParseInput(string path)
    {
        var lines = File.ReadAllLines(path);
        var tests = new List<FuseInput>();
        var i = 0;

        while (SkipBlankLines(lines, ref i))
        {
            var name = lines[i++].Trim();
            var state = ParseState(lines[i++], lines[i++]);
            var memory = new List<FuseMemoryBlock>();

            while (lines[i].Trim() != "-1")
            {
                memory.Add(ParseMemoryBlock(lines[i++]));
            }

            i++;
            tests.Add(new FuseInput(name, state, memory));
        }

        return tests;
    }

    public static List<FuseExpected> ParseExpected(string path)
    {
        var lines = File.ReadAllLines(path);
        var tests = new List<FuseExpected>();
        var i = 0;

        while (SkipBlankLines(lines, ref i))
        {
            var name = lines[i++].Trim();
            var events = new List<FuseEvent>();

            // Event lines are indented; the register line is not.
            while (lines[i].StartsWith(' '))
            {
                events.Add(ParseEvent(lines[i++]));
            }

            var state = ParseState(lines[i++], lines[i++]);
            var memory = new List<FuseMemoryBlock>();

            // Changed-memory lines run until a blank line or the end of the file.
            while (i < lines.Length && lines[i].Trim().Length > 0)
            {
                memory.Add(ParseMemoryBlock(lines[i++]));
            }

            tests.Add(new FuseExpected(name, events, state, memory));
        }

        return tests;
    }

    private static bool SkipBlankLines(string[] lines, ref int i)
    {
        while (i < lines.Length && lines[i].Trim().Length == 0)
        {
            i++;
        }

        return i < lines.Length;
    }

    private static FuseCpuState ParseState(string registerLine, string stateLine)
    {
        var r = Split(registerLine);
        var s = Split(stateLine);

        return new FuseCpuState(
            Word(r[0]), Word(r[1]), Word(r[2]), Word(r[3]),
            Word(r[4]), Word(r[5]), Word(r[6]), Word(r[7]),
            Word(r[8]), Word(r[9]), Word(r[10]), Word(r[11]), Word(r[12]),
            Byte(s[0]), Byte(s[1]), s[2] != "0", s[3] != "0",
            int.Parse(s[4], CultureInfo.InvariantCulture), s[5] != "0",
            int.Parse(s[6], CultureInfo.InvariantCulture));
    }

    private static FuseMemoryBlock ParseMemoryBlock(string line)
    {
        var parts = Split(line);
        var bytes = new List<byte>();

        for (var j = 1; parts[j] != "-1"; j++)
        {
            bytes.Add(Byte(parts[j]));
        }

        return new FuseMemoryBlock(Word(parts[0]), bytes.ToArray());
    }

    private static FuseEvent ParseEvent(string line)
    {
        var parts = Split(line);
        byte? data = parts.Length > 3 ? Byte(parts[3]) : null;

        return new FuseEvent(int.Parse(parts[0], CultureInfo.InvariantCulture), parts[1], Word(parts[2]), data);
    }

    private static string[] Split(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static ushort Word(string hex) => ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte Byte(string hex) => byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
