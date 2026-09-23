// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Tests.Snapshots;

/// <summary>
/// Writes .Z80 files from a machine, for tests only (the emulator does not save .Z80 yet).
/// Follows the format description on World of Spectrum.
/// </summary>
internal static class Z80FileBuilder
{
    public static byte[] Version1(Spectrum48 spectrum, bool compressed)
    {
        var header = Header(spectrum, spectrum.Cpu.PC, compressed);
        var ram = spectrum.Memory.Contents[0x4000..];
        byte[] body = compressed ? [.. Compress(ram), 0x00, 0xED, 0xED, 0x00] : ram.ToArray();
        return [.. header, .. body];
    }

    /// <summary>
    /// A version 3 file (54-byte extra header). Page 5 is stored uncompressed, the others
    /// compressed, to cover both kinds of blocks.
    /// </summary>
    public static byte[] Version3(Spectrum48 spectrum, byte hardware = 0)
    {
        var extra = new byte[2 + 54];
        extra[0] = 54;
        extra[2] = (byte)spectrum.Cpu.PC;
        extra[3] = (byte)(spectrum.Cpu.PC >> 8);
        extra[4] = hardware;

        var file = new List<byte>(Header(spectrum, pc: 0, compressed: false));
        file.AddRange(extra);
        AddPage(file, spectrum, 4, 0x8000, compressed: true);
        AddPage(file, spectrum, 5, 0xC000, compressed: false);
        AddPage(file, spectrum, 8, 0x4000, compressed: true);
        return [.. file];
    }

    /// <summary>
    /// A version 3 file for the 128K (hardware mode 4): pages 3 to 10 hold banks 0 to 7, byte 35
    /// the last write to port 0x7FFD, bytes 38-54 the AY's selected register and its registers.
    /// </summary>
    public static byte[] Version3For128(Spectrum128 spectrum)
    {
        var extra = new byte[2 + 54];
        extra[0] = 54;
        extra[2] = (byte)spectrum.Cpu.PC;
        extra[3] = (byte)(spectrum.Cpu.PC >> 8);
        extra[4] = 4;
        extra[5] = spectrum.Memory.LastPagingValue;
        extra[8] = (byte)spectrum.Ay.SelectedRegister;
        for (var register = 0; register < 16; register++)
        {
            extra[9 + register] = spectrum.Ay.Register(register);
        }

        var file = new List<byte>(Header(spectrum, pc: 0, compressed: false));
        file.AddRange(extra);
        for (var bank = 0; bank < 8; bank++)
        {
            var block = Compress(spectrum.Memory.Bank(bank));
            file.AddRange([(byte)block.Length, (byte)(block.Length >> 8), (byte)(bank + 3)]);
            file.AddRange(block);
        }

        return [.. file];
    }

    /// <summary>
    /// The .Z80 compression: runs of 5 or more equal bytes, and runs of 2 or more ED, become
    /// ED ED count byte; the byte right after a lone ED is never the start of a run.
    /// </summary>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var output = new List<byte>();
        var i = 0;

        while (i < data.Length)
        {
            var value = data[i];
            var run = 1;
            while (i + run < data.Length && data[i + run] == value && run < 255)
            {
                run++;
            }

            if (run >= 5 || (value == 0xED && run >= 2))
            {
                output.AddRange([0xED, 0xED, (byte)run, value]);
                i += run;
            }
            else if (value == 0xED)
            {
                output.Add(0xED);
                i++;
                if (i < data.Length)
                {
                    output.Add(data[i++]);
                }
            }
            else
            {
                output.Add(value);
                i++;
            }
        }

        return [.. output];
    }

    private static byte[] Header(Spectrum spectrum, ushort pc, bool compressed)
    {
        var cpu = spectrum.Cpu;
        var h = new byte[30];
        (h[0], h[1]) = (cpu.A, cpu.F);
        (h[2], h[3]) = (cpu.C, cpu.B);
        (h[4], h[5]) = (cpu.L, cpu.H);
        (h[6], h[7]) = ((byte)pc, (byte)(pc >> 8));
        (h[8], h[9]) = ((byte)cpu.SP, (byte)(cpu.SP >> 8));
        h[10] = cpu.I;
        h[11] = (byte)(cpu.R & 0x7F);
        h[12] = (byte)((cpu.R >> 7) | (spectrum.Ula.Border << 1) | (compressed ? 0x20 : 0));
        (h[13], h[14]) = (cpu.E, cpu.D);
        (h[15], h[16]) = (cpu.C_, cpu.B_);
        (h[17], h[18]) = (cpu.E_, cpu.D_);
        (h[19], h[20]) = (cpu.L_, cpu.H_);
        (h[21], h[22]) = (cpu.A_, cpu.F_);
        (h[23], h[24]) = ((byte)cpu.IY, (byte)(cpu.IY >> 8));
        (h[25], h[26]) = ((byte)cpu.IX, (byte)(cpu.IX >> 8));
        h[27] = cpu.IFF1 ? (byte)1 : (byte)0;
        h[28] = cpu.IFF2 ? (byte)1 : (byte)0;
        h[29] = (byte)cpu.IM;
        return h;
    }

    private static void AddPage(List<byte> file, Spectrum48 spectrum, byte number, int address, bool compressed)
    {
        var data = spectrum.Memory.Contents.Slice(address, 0x4000);
        var block = compressed ? Compress(data) : data.ToArray();
        var length = compressed ? block.Length : 0xFFFF;
        file.AddRange([(byte)length, (byte)(length >> 8), number]);
        file.AddRange(block);
    }
}
