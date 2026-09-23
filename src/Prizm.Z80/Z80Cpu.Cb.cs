// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Z80;

// CB prefix: x = 0 rotations and shifts, 1 BIT, 2 RES, 3 SET, all on r[z].
public sealed partial class Z80Cpu
{
    private void ExecuteCb()
    {
        // The second opcode byte is fetched with its own M1 cycle, so R advances twice.
        var opcode = FetchOpcode();
        var x = opcode >> 6;
        var y = (opcode >> 3) & 7;
        var z = opcode & 7;

        if (z != 6)
        {
            var value = GetRegister(z);
            switch (x)
            {
                case 0: SetRegister(z, RotateOrShift(y, value)); break;
                case 1: Bit(y, value, value); break;
                case 2: SetRegister(z, (byte)(value & ~(1 << y))); break;
                default: SetRegister(z, (byte)(value | (1 << y))); break;
            }

            return;
        }

        var memory = ReadByte(HL);
        Internal(HL, 1);

        switch (x)
        {
            // BIT n,(HL) takes bits 3 and 5 from MEMPTR's high byte.
            case 1: Bit(y, memory, (byte)(WZ >> 8)); break;
            case 0: WriteByte(HL, RotateOrShift(y, memory)); break;
            case 2: WriteByte(HL, (byte)(memory & ~(1 << y))); break;
            default: WriteByte(HL, (byte)(memory | (1 << y))); break;
        }
    }

    /// <summary>
    /// DDCB d op / FDCB d op: the operand is always (IX+d) or (IY+d). The opcode byte comes after
    /// the displacement and is read as data, without an M1 cycle, so R does not advance for it.
    /// </summary>
    private void ExecuteIndexedCb()
    {
        var address = IndexedAddress(ReadOperand());
        var opcode = ReadOperand();
        Internal((ushort)(PC - 1), 2);
        var x = opcode >> 6;
        var y = (opcode >> 3) & 7;
        var z = opcode & 7;

        var value = ReadByte(address);
        Internal(address, 1);

        if (x == 1)
        {
            // BIT n,(IX+d): bits 3 and 5 from the high byte of the address (MEMPTR).
            Bit(y, value, (byte)(address >> 8));
            return;
        }

        var result = x switch
        {
            0 => RotateOrShift(y, value),
            2 => (byte)(value & ~(1 << y)),
            _ => (byte)(value | (1 << y)),
        };
        WriteByte(address, result);

        // Undocumented: for z != 6 the result is also copied into r[z] (plain H and L, not IXH/IXL).
        if (z != 6)
        {
            SetPlainRegister(z, result);
        }
    }

    /// <summary>RLC, RRC, RL, RR, SLA, SRA, SLL (undocumented: shifts in a 1), SRL.</summary>
    private byte RotateOrShift(int operation, byte value)
    {
        int result, carry;

        switch (operation)
        {
            case 0:
                carry = value >> 7;
                result = (value << 1) | carry;
                break;
            case 1:
                carry = value & 1;
                result = (value >> 1) | (carry << 7);
                break;
            case 2:
                carry = value >> 7;
                result = (value << 1) | (F & FlagC);
                break;
            case 3:
                carry = value & 1;
                result = (value >> 1) | ((F & FlagC) << 7);
                break;
            case 4:
                carry = value >> 7;
                result = value << 1;
                break;
            case 5:
                carry = value & 1;
                result = (value >> 1) | (value & 0x80);
                break;
            case 6:
                carry = value >> 7;
                result = (value << 1) | 1;
                break;
            default:
                carry = value & 1;
                result = value >> 1;
                break;
        }

        var r = (byte)result;
        F = (byte)(Sz53P[r] | carry);
        return r;
    }

    /// <summary>
    /// BIT n: Z and P/V set when the bit is 0, S only for BIT 7 when set, H set, C kept.
    /// <paramref name="undocumented"/> supplies bits 3 and 5 (the operand, or MEMPTR's high byte for memory).
    /// </summary>
    private void Bit(int bit, byte value, byte undocumented)
    {
        var tested = value & (1 << bit);
        F = (byte)((F & FlagC)
            | FlagH
            | (undocumented & (Flag5 | Flag3))
            | (tested == 0 ? FlagZ | FlagPV : 0)
            | (tested & FlagS));
    }
}
