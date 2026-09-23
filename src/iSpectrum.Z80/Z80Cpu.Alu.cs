namespace iSpectrum.Z80;

public sealed partial class Z80Cpu
{
    // Flag bits of F. Bits 3 and 5 are undocumented and usually copy the result.
    private const byte FlagC = 0x01;
    private const byte FlagN = 0x02;
    private const byte FlagPV = 0x04;
    private const byte Flag3 = 0x08;
    private const byte FlagH = 0x10;
    private const byte Flag5 = 0x20;
    private const byte FlagZ = 0x40;
    private const byte FlagS = 0x80;

    /// <summary>S, Z, 5 and 3 for each byte value.</summary>
    private static readonly byte[] Sz53 = new byte[256];

    /// <summary>S, Z, 5, 3 and parity (P/V set when even) for each byte value.</summary>
    private static readonly byte[] Sz53P = new byte[256];

    static Z80Cpu()
    {
        for (var i = 0; i < 256; i++)
        {
            var flags = (byte)(i & (FlagS | Flag5 | Flag3));
            if (i == 0)
            {
                flags |= FlagZ;
            }

            var parity = true;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((i & (1 << bit)) != 0)
                {
                    parity = !parity;
                }
            }

            Sz53[i] = flags;
            Sz53P[i] = (byte)(flags | (parity ? FlagPV : 0));
        }
    }

    /// <summary>The 8 ALU operations in opcode order: ADD, ADC, SUB, SBC, AND, XOR, OR, CP.</summary>
    private void Alu(int operation, byte value)
    {
        switch (operation)
        {
            case 0: Add(value, 0); break;
            case 1: Add(value, F & FlagC); break;
            case 2: Sub(value, 0); break;
            case 3: Sub(value, F & FlagC); break;
            case 4: A &= value; F = (byte)(Sz53P[A] | FlagH); break;
            case 5: A ^= value; F = Sz53P[A]; break;
            case 6: A |= value; F = Sz53P[A]; break;
            default: Compare(value); break;
        }
    }

    private void Add(byte value, int carry)
    {
        var result = A + value + carry;
        var r = (byte)result;
        F = (byte)(Sz53[r]
            | ((A ^ value ^ result) & FlagH)
            | ((~(A ^ value) & (A ^ result) & 0x80) != 0 ? FlagPV : 0)
            | (result > 0xFF ? FlagC : 0));
        A = r;
    }

    private byte SubtractFlags(byte value, int carry, out byte result)
    {
        var full = A - value - carry;
        result = (byte)full;
        return (byte)(FlagN
            | (result & FlagS)
            | (result == 0 ? FlagZ : 0)
            | ((A ^ value ^ full) & FlagH)
            | (((A ^ value) & (A ^ full) & 0x80) != 0 ? FlagPV : 0)
            | ((full & 0x100) != 0 ? FlagC : 0));
    }

    private void Sub(byte value, int carry)
    {
        var flags = SubtractFlags(value, carry, out var result);
        F = (byte)(flags | (result & (Flag5 | Flag3)));
        A = result;
    }

    /// <summary>CP: like SUB without storing A; bits 3 and 5 come from the operand, not the result.</summary>
    private void Compare(byte value)
    {
        var flags = SubtractFlags(value, 0, out _);
        F = (byte)(flags | (value & (Flag5 | Flag3)));
    }

    private byte Inc(byte value)
    {
        var result = (byte)(value + 1);
        F = (byte)((F & FlagC)
            | Sz53[result]
            | (result == 0x80 ? FlagPV : 0)
            | ((result & 0x0F) == 0 ? FlagH : 0));
        return result;
    }

    private byte Dec(byte value)
    {
        var result = (byte)(value - 1);
        F = (byte)((F & FlagC)
            | FlagN
            | Sz53[result]
            | (result == 0x7F ? FlagPV : 0)
            | ((value & 0x0F) == 0 ? FlagH : 0));
        return result;
    }

    /// <summary>ADD HL,rr (also ADD IX/IY,rr): S, Z and P/V are kept; 3 and 5 come from the high byte.</summary>
    private ushort Add16(ushort left, ushort right)
    {
        var result = left + right;
        WZ = (ushort)(left + 1);
        F = (byte)((F & (FlagS | FlagZ | FlagPV))
            | ((result >> 8) & (Flag5 | Flag3))
            | (((left ^ right ^ result) >> 8) & FlagH)
            | (result > 0xFFFF ? FlagC : 0));
        return (ushort)result;
    }

    /// <summary>Flags after RLCA, RRCA, RLA, RRA: S, Z and P/V kept, H and N reset.</summary>
    private void SetAccumulatorRotateFlags(int carry) =>
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | (A & (Flag5 | Flag3)) | carry);

    private void Daa()
    {
        int correction = 0, carry = F & FlagC;
        var low = A & 0x0F;

        if ((F & FlagH) != 0 || low > 9)
        {
            correction = 0x06;
        }

        if (carry != 0 || A > 0x99)
        {
            correction |= 0x60;
            carry = FlagC;
        }

        int halfCarry;
        if ((F & FlagN) != 0)
        {
            halfCarry = (F & FlagH) != 0 && low < 6 ? FlagH : 0;
            A = (byte)(A - correction);
        }
        else
        {
            halfCarry = low > 9 ? FlagH : 0;
            A = (byte)(A + correction);
        }

        F = (byte)(Sz53P[A] | (F & FlagN) | halfCarry | carry);
    }
}
