// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core.Snapshots;

/// <summary>
/// The .Z80 snapshot, versions 1 to 3, for 48K machines. Version 1 has a 30-byte header and the
/// 48 KB of RAM, optionally compressed. Versions 2 and 3 (PC = 0 in the first header) add a
/// second header, then 16 KB pages, each compressed or not: pages 8, 4 and 5 hold 0x4000,
/// 0x8000 and 0xC000. 128K snapshots are refused until milestone 8.
/// </summary>
/// <remarks>Reference: the .Z80 format description on World of Spectrum.</remarks>
public static class Z80Format
{
    private const int Version1HeaderLength = 30;
    private const int PageLength = 0x4000;

    public static void Load(Spectrum48 spectrum, ReadOnlySpan<byte> data)
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
            LoadVersion1Ram(spectrum.Memory, data[Version1HeaderLength..], compressed: (flags & 0x20) != 0);
        }
        else
        {
            pc = LoadVersion2Or3(spectrum.Memory, data);
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
    private static ushort LoadVersion2Or3(Memory48K memory, ReadOnlySpan<byte> data)
    {
        if (data.Length < Version1HeaderLength + 2)
        {
            throw new InvalidDataException("The file is too short for a version 2 or 3 header.");
        }

        var extraLength = Word(data, 30);
        var version = extraLength switch
        {
            23 => 2,
            54 or 55 => 3,
            _ => throw new InvalidDataException($"Unknown .Z80 header length {extraLength}."),
        };

        var pagesStart = Version1HeaderLength + 2 + extraLength;
        if (data.Length < pagesStart)
        {
            throw new InvalidDataException("The file is too short for its version 2 or 3 header.");
        }

        // Hardware 0 and 1 are 48K (with or without Interface 1) in both versions; in version 3,
        // 3 is 48K with an M.G.T. interface. Everything else needs 128K or another machine.
        var hardware = data[34];
        if (hardware is not (0 or 1) && !(version == 3 && hardware == 3))
        {
            throw new NotSupportedException($"Only 48K snapshots are supported (hardware mode {hardware}, version {version}).");
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

            // Other pages (ROM, interface ROMs) are not part of the 48K RAM.
            ushort? address = number switch
            {
                8 => 0x4000,
                4 => 0x8000,
                5 => 0xC000,
                _ => null,
            };

            if (address is null)
            {
                continue;
            }

            if (length == 0xFFFF)
            {
                block.CopyTo(page);
            }
            else
            {
                Decompress(block, page);
            }

            memory.LoadRam(address.Value, page);
            loaded |= 1 << number;
        }

        if (loaded != ((1 << 8) | (1 << 4) | (1 << 5)))
        {
            throw new InvalidDataException("A 48K snapshot must contain pages 4, 5 and 8.");
        }

        return Word(data, 32);
    }

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
