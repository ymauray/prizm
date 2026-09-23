// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Snapshots;

/// <summary>
/// The .Z80 snapshot, versions 1 to 3, for the 48K and the 128K. Version 1 has a 30-byte header
/// and the 48 KB of RAM, optionally compressed. Versions 2 and 3 (PC = 0 in the first header) add
/// a second header, then 16 KB pages, each compressed or not. On the 48K pages 8, 4 and 5 hold
/// 0x4000, 0x8000 and 0xC000; on the 128K pages 3 to 10 hold banks 0 to 7, and the second header
/// also keeps the last write to port 0x7FFD and the AY's registers.
/// </summary>
/// <remarks>Reference: the .Z80 format description on World of Spectrum.</remarks>
public static class Z80Format
{
    private const int Version1HeaderLength = 30;
    private const int PageLength = 0x4000;

    /// <summary>Whether a .Z80 file is a 128K snapshot (from the hardware mode of its second header).</summary>
    public static bool IsSpectrum128(ReadOnlySpan<byte> data) =>
        data.Length >= Version1HeaderLength + 2 + 23
        && Word(data, 6) == 0
        && Version(Word(data, 30)) is { } version
        && Is128Hardware(version, data[34]);

    /// <summary>Loads a snapshot into a machine of its model.</summary>
    public static void Load(Spectrum spectrum, ReadOnlySpan<byte> data)
    {
        if (data.Length < Version1HeaderLength)
        {
            throw new InvalidDataException("The file is too short for a .Z80 header.");
        }

        // Byte 12 = 255 is an old convention for 1.
        var flags = data[12] == 0xFF ? (byte)1 : data[12];
        var pc = Word(data, 6);

        if (pc != 0)
        {
            if (spectrum is not Spectrum48 spectrum48)
            {
                throw new NotSupportedException("Version 1 .Z80 snapshots are for the 48K.");
            }

            LoadVersion1Ram(spectrum48.Memory, data[Version1HeaderLength..], compressed: (flags & 0x20) != 0);
        }
        else
        {
            pc = LoadVersion2Or3(spectrum, data);
        }

        var cpu = spectrum.Cpu;
        (cpu.A, cpu.F) = (data[0], data[1]);
        (cpu.B, cpu.C) = (data[3], data[2]);
        (cpu.H, cpu.L) = (data[5], data[4]);
        cpu.PC = pc;
        cpu.SP = Word(data, 8);
        cpu.I = data[10];
        cpu.R = (byte)((data[11] & 0x7F) | ((flags & 0x01) << 7));
        spectrum.Ula.Border = (flags >> 1) & 0x07;
        (cpu.D, cpu.E) = (data[14], data[13]);
        (cpu.B_, cpu.C_) = (data[16], data[15]);
        (cpu.D_, cpu.E_) = (data[18], data[17]);
        (cpu.H_, cpu.L_) = (data[20], data[19]);
        (cpu.A_, cpu.F_) = (data[21], data[22]);
        cpu.IY = Word(data, 23);
        cpu.IX = Word(data, 25);
        cpu.IFF1 = data[27] != 0;
        cpu.IFF2 = data[28] != 0;
        cpu.IM = data[29] & 0x03;
        cpu.Halted = false;
        cpu.TStates = 0;
    }

    private static void LoadVersion1Ram(Memory48K memory, ReadOnlySpan<byte> body, bool compressed)
    {
        var ram = new byte[3 * PageLength];

        if (compressed)
        {
            // The data normally ends with the marker 00 ED ED 00, which decompression never reaches.
            Decompress(body, ram);
        }
        else if (body.Length >= ram.Length)
        {
            body[..ram.Length].CopyTo(ram);
        }
        else
        {
            throw new InvalidDataException("The uncompressed memory image is shorter than 48 KB.");
        }

        memory.LoadRam(Memory48K.RomSize, ram);
    }

    /// <summary>Loads the pages of a version 2 or 3 file and returns PC from its second header.</summary>
    private static ushort LoadVersion2Or3(Spectrum spectrum, ReadOnlySpan<byte> data)
    {
        if (data.Length < Version1HeaderLength + 2)
        {
            throw new InvalidDataException("The file is too short for a version 2 or 3 header.");
        }

        var extraLength = Word(data, 30);
        var version = Version(extraLength)
            ?? throw new InvalidDataException($"Unknown .Z80 header length {extraLength}.");

        var pagesStart = Version1HeaderLength + 2 + extraLength;
        if (data.Length < pagesStart)
        {
            throw new InvalidDataException("The file is too short for its version 2 or 3 header.");
        }

        var hardware = data[34];
        var is128 = Is128Hardware(version, hardware);
        if (!is128 && !Is48Hardware(version, hardware))
        {
            throw new NotSupportedException($"Hardware mode {hardware} (version {version}) is neither a 48K nor a 128K.");
        }

        if (is128 != spectrum is Spectrum128)
        {
            throw new NotSupportedException(is128 ? "This snapshot needs a 128K." : "This snapshot is for a 48K.");
        }

        var loaded = 0;
        var page = new byte[PageLength];
        var position = pagesStart;

        while (position < data.Length)
        {
            if (position + 3 > data.Length)
            {
                throw new InvalidDataException("Truncated page header.");
            }

            var length = Word(data, position);
            var number = data[position + 2];
            position += 3;

            var stored = length == 0xFFFF ? PageLength : length;
            if (position + stored > data.Length)
            {
                throw new InvalidDataException($"Page {number} is truncated.");
            }

            var block = data.Slice(position, stored);
            position += stored;

            if (length == 0xFFFF)
            {
                block.CopyTo(page);
            }
            else
            {
                Decompress(block, page);
            }

            if (StorePage(spectrum, number, page))
            {
                loaded |= 1 << number;
            }
        }

        if (spectrum is Spectrum128 spectrum128)
        {
            if (loaded != 0b111_1111_1000)
            {
                throw new InvalidDataException("A 128K snapshot must contain pages 3 to 10.");
            }

            RestoreChips(spectrum128, data);
        }
        else if (loaded != ((1 << 8) | (1 << 4) | (1 << 5)))
        {
            throw new InvalidDataException("A 48K snapshot must contain pages 4, 5 and 8.");
        }

        return Word(data, 32);
    }

    /// <summary>
    /// Puts a page where it belongs; false for pages that are not RAM (ROMs, interface ROMs).
    /// </summary>
    private static bool StorePage(Spectrum spectrum, int number, ReadOnlySpan<byte> page)
    {
        if (spectrum is Spectrum128 spectrum128)
        {
            if (number is < 3 or > 10)
            {
                return false;
            }

            page.CopyTo(spectrum128.Memory.Bank(number - 3));
            return true;
        }

        ushort? address = number switch
        {
            8 => 0x4000,
            4 => 0x8000,
            5 => 0xC000,
            _ => null,
        };

        if (address is null)
        {
            return false;
        }

        ((Spectrum48)spectrum).Memory.LoadRam(address.Value, page);
        return true;
    }

    /// <summary>The 128K's paging (byte 35) and the AY: registers at bytes 39-54, selected one at 38.</summary>
    private static void RestoreChips(Spectrum128 spectrum, ReadOnlySpan<byte> data)
    {
        spectrum.Memory.WritePaging(data[35]);

        for (var register = 0; register < 16; register++)
        {
            spectrum.Ay.SelectRegister((byte)register);
            spectrum.Ay.WriteSelected(data[39 + register]);
        }

        spectrum.Ay.SelectRegister(data[38]);
    }

    private static int? Version(int extraLength) => extraLength switch
    {
        23 => 2,
        54 or 55 => 3,
        _ => null,
    };

    /// <summary>48K, 48K + Interface 1, and in version 3 48K + M.G.T.</summary>
    private static bool Is48Hardware(int version, byte hardware) => hardware is 0 or 1 || (version == 3 && hardware == 3);

    /// <summary>128K and 128K + Interface 1; in version 3 also 128K + M.G.T. (the numbers shift by one).</summary>
    private static bool Is128Hardware(int version, byte hardware) =>
        version == 2 ? hardware is 3 or 4 : hardware is 4 or 5 or 6;

    /// <summary>
    /// Expands <paramref name="source"/> until <paramref name="destination"/> is full. ED ED n b
    /// stands for n copies of b; every other byte, including a lone ED, is copied as is.
    /// </summary>
    internal static void Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int input = 0, output = 0;

        while (output < destination.Length)
        {
            if (input >= source.Length)
            {
                throw new InvalidDataException("The compressed data ends before the memory is full.");
            }

            if (source[input] == 0xED && input + 3 < source.Length && source[input + 1] == 0xED)
            {
                var count = source[input + 2];
                if (output + count > destination.Length)
                {
                    throw new InvalidDataException("A compressed run goes past the end of the memory.");
                }

                destination.Slice(output, count).Fill(source[input + 3]);
                output += count;
                input += 4;
            }
            else
            {
                destination[output++] = source[input++];
            }
        }
    }

    private static ushort Word(ReadOnlySpan<byte> data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
}
