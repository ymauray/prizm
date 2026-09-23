// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80;

// ED prefix: x = 1 holds the miscellaneous instructions, x = 2 with y >= 4 and z <= 3 the block
// instructions. Every other ED opcode is an 8 T-state NOP.
public sealed partial class Z80Cpu
{
    /// <summary>Interrupt mode set by IM, indexed by y (IM 0/1 at y = 1 and 5 acts as IM 0).</summary>
    private static readonly int[] InterruptModes = [0, 0, 1, 2, 0, 0, 1, 2];

    private void ExecuteEd()
    {
        // A DD or FD before ED has no effect: ED instructions always use HL.
        _index = IndexHL;
        var opcode = FetchOpcode();
        var x = opcode >> 6;
        var y = (opcode >> 3) & 7;
        var z = opcode & 7;

        if (x == 1)
        {
            ExecuteEdX1(y, z);
        }
        else if (x == 2 && y >= 4 && z <= 3)
        {
            ExecuteBlock(y, z);
        }
    }

    private void ExecuteEdX1(int y, int z)
    {
        var p = y >> 1;
        var q = y & 1;

        switch (z)
        {
            case 0:
            {
                // IN r,(C); y = 6 only sets the flags.
                var value = ReadPort(BC);
                WZ = (ushort)(BC + 1);
                F = (byte)((F & FlagC) | Sz53P[value]);
                if (y != 6)
                {
                    SetRegister(y, value);
                }

                break;
            }

            case 1:
                // OUT (C),r; y = 6 outputs 0 on NMOS Z80s (0xFF on CMOS).
                WritePort(BC, y == 6 ? (byte)0 : GetRegister(y));
                WZ = (ushort)(BC + 1);
                break;

            case 2:
                Internal(7);
                HL = q == 0 ? Sbc16(HL, GetRegisterPair(p)) : Adc16(HL, GetRegisterPair(p));
                break;

            case 3:
            {
                var address = ReadOperandWord();
                if (q == 0)
                {
                    var value = GetRegisterPair(p);
                    WriteByte(address, (byte)value);
                    WriteByte((ushort)(address + 1), (byte)(value >> 8));
                }
                else
                {
                    var low = ReadByte(address);
                    SetRegisterPair(p, (ushort)(low | (ReadByte((ushort)(address + 1)) << 8)));
                }

                WZ = (ushort)(address + 1);
                break;
            }

            case 4:
            {
                // NEG (every y): A = 0 - A.
                var value = A;
                A = 0;
                Sub(value, 0);
                break;
            }

            case 5:
                // RETN, and RETI at y = 1: both copy IFF2 into IFF1.
                IFF1 = IFF2;
                PC = Pop();
                WZ = PC;
                break;

            case 6:
                IM = InterruptModes[y];
                break;

            default:
                ExecuteEdX1Z7(y);
                break;
        }
    }

    /// <summary>LD I,A, LD R,A, LD A,I, LD A,R, RRD, RLD; y = 6 and 7 are NOPs.</summary>
    private void ExecuteEdX1Z7(int y)
    {
        switch (y)
        {
            case 0:
                Internal(1);
                I = A;
                break;

            case 1:
                Internal(1);
                R = A;
                break;

            case 2:
            case 3:
                // P/V reflects IFF2.
                Internal(1);
                A = y == 2 ? I : R;
                F = (byte)((F & FlagC) | Sz53[A] | (IFF2 ? FlagPV : 0));
                break;

            case 4:
            case 5:
            {
                var value = ReadByte(HL);
                Internal(4);
                if (y == 4)
                {
                    WriteByte(HL, (byte)((A << 4) | (value >> 4)));
                    A = (byte)((A & 0xF0) | (value & 0x0F));
                }
                else
                {
                    WriteByte(HL, (byte)((value << 4) | (A & 0x0F)));
                    A = (byte)((A & 0xF0) | (value >> 4));
                }

                F = (byte)((F & FlagC) | Sz53P[A]);
                WZ = (ushort)(HL + 1);
                break;
            }
        }
    }

    /// <summary>
    /// y = 4..7: I, D, IR, DR; z = 0..3: LD, CP, IN, OUT. The repeating forms rewind PC by 2
    /// and spend 5 more T-states while the loop continues.
    /// </summary>
    private void ExecuteBlock(int y, int z)
    {
        var step = (y & 1) == 0 ? 1 : -1;
        var repeat = y >= 6;

        switch (z)
        {
            case 0: BlockLoad(step, repeat); break;
            case 1: BlockCompare(step, repeat); break;
            case 2: BlockIn(step, repeat); break;
            default: BlockOut(step, repeat); break;
        }
    }

    private void BlockLoad(int step, bool repeat)
    {
        var value = ReadByte(HL);
        WriteByte(DE, value);
        Internal(2);
        BC--;
        DE = (ushort)(DE + step);
        HL = (ushort)(HL + step);

        // Bits 3 and 5 come from bits 3 and 1 of (value + A).
        var n = value + A;
        F = (byte)((F & (FlagS | FlagZ | FlagC))
            | (BC != 0 ? FlagPV : 0)
            | (n & Flag3)
            | ((n << 4) & Flag5));

        if (repeat && BC != 0)
        {
            RepeatBlock();
        }
    }

    private void BlockCompare(int step, bool repeat)
    {
        var value = ReadByte(HL);
        Internal(5);
        var result = (byte)(A - value);
        var halfCarry = (A ^ value ^ result) & FlagH;
        BC--;
        HL = (ushort)(HL + step);
        WZ = (ushort)(WZ + step);

        // Bits 3 and 5 come from bits 3 and 1 of (A - value - H).
        var n = result - (halfCarry >> 4);
        F = (byte)((F & FlagC)
            | FlagN
            | halfCarry
            | (result & FlagS)
            | (result == 0 ? FlagZ : 0)
            | (BC != 0 ? FlagPV : 0)
            | (n & Flag3)
            | ((n << 4) & Flag5));

        if (repeat && BC != 0 && result != 0)
        {
            RepeatBlock();
        }
    }

    private void BlockIn(int step, bool repeat)
    {
        Internal(1);
        var value = ReadPort(BC);
        WriteByte(HL, value);
        WZ = (ushort)(BC + step);
        B--;
        HL = (ushort)(HL + step);

        var sum = value + ((C + step) & 0xFF);
        SetBlockIoFlags(value, sum);

        if (repeat && B != 0)
        {
            Internal(5);
            PC -= 2;
        }
    }

    private void BlockOut(int step, bool repeat)
    {
        Internal(1);
        var value = ReadByte(HL);
        B--;
        WZ = (ushort)(BC + step);
        WritePort(BC, value);
        HL = (ushort)(HL + step);

        // L is taken after HL has moved.
        var sum = value + L;
        SetBlockIoFlags(value, sum);

        if (repeat && B != 0)
        {
            Internal(5);
            PC -= 2;
        }
    }

    /// <summary>
    /// Flags of INI/IND/OUTI/OUTD and their repeating forms (see The Undocumented Z80 Documented):
    /// S, Z, 5, 3 from B; N from bit 7 of the transferred byte; H and C on overflow of the 8-bit sum;
    /// P/V is the parity of (sum &amp; 7) xor B.
    /// </summary>
    private void SetBlockIoFlags(byte value, int sum)
    {
        F = (byte)(Sz53[B]
            | ((value & 0x80) != 0 ? FlagN : 0)
            | (sum > 0xFF ? FlagH | FlagC : 0)
            | (Sz53P[(sum & 7) ^ B] & FlagPV));
    }

    private void RepeatBlock()
    {
        Internal(5);
        PC -= 2;
        WZ = (ushort)(PC + 1);
    }

    private ushort Adc16(ushort left, ushort right)
    {
        var result = left + right + (F & FlagC);
        WZ = (ushort)(left + 1);
        F = (byte)(((result >> 8) & (FlagS | Flag5 | Flag3))
            | ((result & 0xFFFF) == 0 ? FlagZ : 0)
            | (((left ^ right ^ result) >> 8) & FlagH)
            | ((~(left ^ right) & (left ^ result) & 0x8000) != 0 ? FlagPV : 0)
            | (result > 0xFFFF ? FlagC : 0));
        return (ushort)result;
    }

    private ushort Sbc16(ushort left, ushort right)
    {
        var result = left - right - (F & FlagC);
        WZ = (ushort)(left + 1);
        F = (byte)(FlagN
            | ((result >> 8) & (FlagS | Flag5 | Flag3))
            | ((result & 0xFFFF) == 0 ? FlagZ : 0)
            | (((left ^ right ^ result) >> 8) & FlagH)
            | (((left ^ right) & (left ^ result) & 0x8000) != 0 ? FlagPV : 0)
            | ((result & 0x10000) != 0 ? FlagC : 0));
        return (ushort)result;
    }
}
