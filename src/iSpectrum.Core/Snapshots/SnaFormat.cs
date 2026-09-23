// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core.Snapshots;

/// <summary>
/// The 48K .SNA snapshot: a 27-byte register header followed by the 48 KB of RAM. PC is not in
/// the header: it sits on the stack, as if an interrupt had just been taken, and loading ends
/// with the equivalent of RETN (PC popped, IFF1 = IFF2).
/// </summary>
public static class SnaFormat
{
    public const int HeaderLength = 27;
    public const int RamLength = 0xC000;
    public const int FileLength = HeaderLength + RamLength;

    public static void Load(Spectrum48 spectrum, ReadOnlySpan<byte> data)
    {
        if (data.Length != FileLength)
        {
            throw new InvalidDataException($"A 48K .SNA file is {FileLength} bytes long, not {data.Length}.");
        }

        spectrum.Memory.LoadRam(Memory48K.RomSize, data[HeaderLength..]);

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

        // RETN: PC comes off the stack.
        var memory = spectrum.Memory;
        cpu.PC = (ushort)(memory.Read(cpu.SP) | (memory.Read((ushort)(cpu.SP + 1)) << 8));
        cpu.SP += 2;

        cpu.Halted = false;
        cpu.TStates = 0;
    }

    /// <summary>
    /// Writes the machine as a .SNA file, pushing PC on the stack of the saved RAM (the running
    /// machine is not changed). Fails if the stack pointer leaves no room for PC in RAM.
    /// </summary>
    public static byte[] Save(Spectrum48 spectrum)
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

        return data;
    }

    private static ushort Word(ReadOnlySpan<byte> data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static void SetWord(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
    }
}
