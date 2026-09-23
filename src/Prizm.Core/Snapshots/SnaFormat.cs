// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Snapshots;

/// <summary>
/// The .SNA snapshot. A 27-byte register header comes first on both models.
/// </summary>
/// <remarks>
/// 48K: the header, then the 48 KB of RAM. PC is not in the header: it sits on the stack, as if
/// an interrupt had just been taken, and loading ends with the equivalent of RETN.
/// 128K: the header, banks 5 and 2, the bank paged at 0xC000, then PC, the last write to port
/// 0x7FFD, a TR-DOS byte, and the other banks in ascending order. When the paged bank is 5 or 2 it
/// is saved twice, so the file has 6 more banks instead of 5 (147487 bytes instead of 131103).
/// Reference: the snapshot formats page of World of Spectrum.
/// </remarks>
public static class SnaFormat
{
    public const int HeaderLength = 27;
    public const int RamLength = 0xC000;
    public const int FileLength = HeaderLength + RamLength;

    public const int FileLength128 = FileLength + 4 + (5 * SpectrumMemory.BankSize);
    public const int FileLength128WithRepeatedBank = FileLength + 4 + (6 * SpectrumMemory.BankSize);

    /// <summary>Offset of PC in a 128K file, after the header and the first three banks.</summary>
    private const int Pc128Offset = FileLength;

    private const int Paging128Offset = FileLength + 2;
    private const int OtherBanksOffset = FileLength + 4;

    /// <summary>Whether a .SNA file is a 128K one (from its length).</summary>
    public static bool IsSpectrum128(ReadOnlySpan<byte> data) =>
        data.Length is FileLength128 or FileLength128WithRepeatedBank;

    /// <summary>Loads a snapshot into a machine of its model.</summary>
    public static void Load(Spectrum spectrum, ReadOnlySpan<byte> data)
    {
        switch (spectrum)
        {
            case Spectrum48 spectrum48:
                Load48(spectrum48, data);
                break;
            case Spectrum128 spectrum128:
                Load128(spectrum128, data);
                break;
            default:
                throw new NotSupportedException($"No .SNA format for {spectrum.GetType().Name}.");
        }
    }

    /// <summary>Writes the machine as a .SNA file; the running machine is not changed.</summary>
    public static byte[] Save(Spectrum spectrum) => spectrum switch
    {
        Spectrum48 spectrum48 => Save48(spectrum48),
        Spectrum128 spectrum128 => Save128(spectrum128),
        _ => throw new NotSupportedException($"No .SNA format for {spectrum.GetType().Name}."),
    };

    private static void Load48(Spectrum48 spectrum, ReadOnlySpan<byte> data)
    {
        if (data.Length != FileLength)
        {
            throw new InvalidDataException(IsSpectrum128(data)
                ? "This .SNA file is a 128K snapshot."
                : $"A 48K .SNA file is {FileLength} bytes long, not {data.Length}.");
        }

        spectrum.Memory.LoadRam(Memory48K.RomSize, data[HeaderLength..]);
        LoadHeader(spectrum, data);

        // RETN: PC comes off the stack.
        var cpu = spectrum.Cpu;
        var memory = spectrum.Memory;
        cpu.PC = (ushort)(memory.Read(cpu.SP) | (memory.Read((ushort)(cpu.SP + 1)) << 8));
        cpu.SP += 2;
    }

    private static void Load128(Spectrum128 spectrum, ReadOnlySpan<byte> data)
    {
        if (!IsSpectrum128(data))
        {
            throw new InvalidDataException(data.Length == FileLength
                ? "This .SNA file is a 48K snapshot."
                : $"A 128K .SNA file is {FileLength128} or {FileLength128WithRepeatedBank} bytes long, not {data.Length}.");
        }

        var paging = data[Paging128Offset];
        var paged = paging & 0x07;
        var repeated = paged is 2 or 5;
        if (repeated != (data.Length == FileLength128WithRepeatedBank))
        {
            throw new InvalidDataException("The file's length does not match its paged bank.");
        }

        var memory = spectrum.Memory;
        var banks = data[HeaderLength..];
        banks[..SpectrumMemory.BankSize].CopyTo(memory.Bank(5));
        banks.Slice(SpectrumMemory.BankSize, SpectrumMemory.BankSize).CopyTo(memory.Bank(2));
        banks.Slice(2 * SpectrumMemory.BankSize, SpectrumMemory.BankSize).CopyTo(memory.Bank(paged));

        var position = OtherBanksOffset;
        foreach (var bank in OtherBanks(paged))
        {
            data.Slice(position, SpectrumMemory.BankSize).CopyTo(memory.Bank(bank));
            position += SpectrumMemory.BankSize;
        }

        memory.WritePaging(paging);
        LoadHeader(spectrum, data);
        spectrum.Cpu.PC = Word(data, Pc128Offset);
    }

    /// <summary>
    /// Pushes PC on the stack of the saved RAM. Fails if the stack pointer leaves no room for PC
    /// in RAM.
    /// </summary>
    private static byte[] Save48(Spectrum48 spectrum)
    {
        var cpu = spectrum.Cpu;
        var sp = (ushort)(cpu.SP - 2);
        if (sp < Memory48K.RomSize || sp == 0xFFFF)
        {
            throw new InvalidOperationException($"Cannot push PC: SP = 0x{cpu.SP:X4} leaves no room in RAM.");
        }

        var data = new byte[FileLength];
        var ram = data.AsSpan(HeaderLength);
        spectrum.Memory.Contents[Memory48K.RomSize..].CopyTo(ram);
        ram[sp - Memory48K.RomSize] = (byte)cpu.PC;
        ram[sp + 1 - Memory48K.RomSize] = (byte)(cpu.PC >> 8);

        SaveHeader(spectrum, data, sp);
        return data;
    }

    private static byte[] Save128(Spectrum128 spectrum)
    {
        var memory = spectrum.Memory;
        var paged = memory.RamPage;
        var data = new byte[paged is 2 or 5 ? FileLength128WithRepeatedBank : FileLength128];

        SaveHeader(spectrum, data, spectrum.Cpu.SP);
        memory.Bank(5).CopyTo(data.AsSpan(HeaderLength));
        memory.Bank(2).CopyTo(data.AsSpan(HeaderLength + SpectrumMemory.BankSize));
        memory.Bank(paged).CopyTo(data.AsSpan(HeaderLength + (2 * SpectrumMemory.BankSize)));

        SetWord(data, Pc128Offset, spectrum.Cpu.PC);
        data[Paging128Offset] = memory.LastPagingValue;

        var position = OtherBanksOffset;
        foreach (var bank in OtherBanks(paged))
        {
            memory.Bank(bank).CopyTo(data.AsSpan(position));
            position += SpectrumMemory.BankSize;
        }

        return data;
    }

    /// <summary>The banks saved after PC: all but 5, 2 and the paged one, in ascending order.</summary>
    private static IEnumerable<int> OtherBanks(int paged)
    {
        for (var bank = 0; bank < 8; bank++)
        {
            if (bank is not (2 or 5) && bank != paged)
            {
                yield return bank;
            }
        }
    }

    private static void LoadHeader(Spectrum spectrum, ReadOnlySpan<byte> data)
    {
        var cpu = spectrum.Cpu;
        cpu.I = data[0];
        (cpu.H_, cpu.L_) = (data[2], data[1]);
        (cpu.D_, cpu.E_) = (data[4], data[3]);
        (cpu.B_, cpu.C_) = (data[6], data[5]);
        (cpu.A_, cpu.F_) = (data[8], data[7]);
        (cpu.H, cpu.L) = (data[10], data[9]);
        (cpu.D, cpu.E) = (data[12], data[11]);
        (cpu.B, cpu.C) = (data[14], data[13]);
        cpu.IY = Word(data, 15);
        cpu.IX = Word(data, 17);
        cpu.IFF2 = (data[19] & 0x04) != 0;
        cpu.IFF1 = cpu.IFF2;
        cpu.R = data[20];
        (cpu.A, cpu.F) = (data[22], data[21]);
        cpu.SP = Word(data, 23);
        cpu.IM = data[25] & 0x03;
        spectrum.Ula.Border = data[26];
        cpu.Halted = false;
        cpu.TStates = 0;
    }

    private static void SaveHeader(Spectrum spectrum, byte[] data, ushort sp)
    {
        var cpu = spectrum.Cpu;
        data[0] = cpu.I;
        (data[2], data[1]) = (cpu.H_, cpu.L_);
        (data[4], data[3]) = (cpu.D_, cpu.E_);
        (data[6], data[5]) = (cpu.B_, cpu.C_);
        (data[8], data[7]) = (cpu.A_, cpu.F_);
        (data[10], data[9]) = (cpu.H, cpu.L);
        (data[12], data[11]) = (cpu.D, cpu.E);
        (data[14], data[13]) = (cpu.B, cpu.C);
        SetWord(data, 15, cpu.IY);
        SetWord(data, 17, cpu.IX);
        data[19] = cpu.IFF2 ? (byte)0x04 : (byte)0;
        data[20] = cpu.R;
        (data[22], data[21]) = (cpu.A, cpu.F);
        SetWord(data, 23, sp);
        data[25] = (byte)cpu.IM;
        data[26] = (byte)spectrum.Ula.Border;
    }

    private static ushort Word(ReadOnlySpan<byte> data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static void SetWord(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
    }
}
