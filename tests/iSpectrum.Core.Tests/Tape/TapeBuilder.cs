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

    /// <summary>A .TZX file made of the given blocks (each with its ID).</summary>
    public static TapeImage Tzx(params byte[][] blocks)
    {
        var file = new List<byte>("ZXTape!\u001A"u8.ToArray()) { 1, 20 };
        foreach (var block in blocks)
        {
            file.AddRange(block);
        }

        return TzxFile.Parse(file.ToArray());
    }

    /// <summary>TZX block 0x10: a block saved by the ROM, then a pause.</summary>
    public static byte[] StandardBlock(byte[] block, int pauseMs = 1000) =>
        [0x10, .. Word(pauseMs), .. Word(block.Length), .. block];

    /// <summary>TZX block 0x11: a data block with its own bit timings.</summary>
    public static byte[] TurboBlock(byte[] block, int zeroPulse, int onePulse, int pilotPulses = TapePlayer.DataPilotPulses, int pauseMs = 1000) =>
    [
        0x11, .. Word(TapePlayer.PilotPulse), .. Word(TapePlayer.FirstSyncPulse), .. Word(TapePlayer.SecondSyncPulse),
        .. Word(zeroPulse), .. Word(onePulse), .. Word(pilotPulses), 8, .. Word(pauseMs),
        (byte)block.Length, (byte)(block.Length >> 8), (byte)(block.Length >> 16), .. block,
    ];

    /// <summary>TZX block 0x20: a pause; 0 stops the tape.</summary>
    public static byte[] PauseBlock(int pauseMs) => [0x20, .. Word(pauseMs)];

    /// <summary>TZX block 0x30: a text description.</summary>
    public static byte[] TextBlock(string text) => [0x30, (byte)text.Length, .. text.Select(c => (byte)c)];

    /// <summary>TZX block 0x28: a menu, each text with the offset (in blocks) it leads to.</summary>
    public static byte[] SelectBlock(params (int Offset, string Text)[] choices)
    {
        var body = new List<byte> { (byte)choices.Length };
        foreach (var (offset, text) in choices)
        {
            body.AddRange(Word(offset));
            body.Add((byte)text.Length);
            body.AddRange(text.Select(c => (byte)c));
        }

        return [0x28, .. Word(body.Count), .. body];
    }

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];
}
