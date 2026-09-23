// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

/// <summary>Builds small tapes for tests, as the ROM's SAVE would write them.</summary>
internal static class TapeBuilder
{
    /// <summary>
    /// A tape holding the BASIC program <c>10 PRINT "HI"</c>, which starts on its own at line 10.
    /// </summary>
    public static TapFile HelloProgram()
    {
        // Line 10: number (big-endian), length (little-endian), PRINT token, "HI", ENTER.
        byte[] program = [0x00, 0x0A, 0x06, 0x00, 0xF5, (byte)'"', (byte)'H', (byte)'I', (byte)'"', 0x0D];
        return Program("hello", program, autoStartLine: 10);
    }

    public static TapFile Program(string name, byte[] program, int autoStartLine)
    {
        // Header: type 0 (program), 10-character name, data length, auto-start line, and the
        // offset of the variables (no variables: the end of the program).
        var header = new byte[17];
        header[0] = 0x00;
        for (var i = 0; i < 10; i++)
        {
            header[1 + i] = i < name.Length ? (byte)name[i] : (byte)' ';
        }

        (header[11], header[12]) = ((byte)program.Length, (byte)(program.Length >> 8));
        (header[13], header[14]) = ((byte)autoStartLine, (byte)(autoStartLine >> 8));
        (header[15], header[16]) = ((byte)program.Length, (byte)(program.Length >> 8));

        return Tap(TapFile.MakeBlock(0x00, header), TapFile.MakeBlock(0xFF, program));
    }

    public static TapFile Tap(params byte[][] blocks)
    {
        var file = new List<byte>();
        foreach (var block in blocks)
        {
            file.Add((byte)block.Length);
            file.Add((byte)(block.Length >> 8));
            file.AddRange(block);
        }

        return TapFile.Parse(file.ToArray());
    }
}
