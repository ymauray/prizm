namespace iSpectrum.Z80;

// Decoding follows "Decoding Z80 Opcodes" (Cristian Dinu): an opcode splits into
// x = bits 7-6, y = bits 5-3, z = bits 2-0, p = y >> 1, q = y & 1.
public sealed partial class Z80Cpu
{
    /// <summary>Fetches, decodes and executes one instruction, adding its exact T-states.</summary>
    public void Step()
    {
        var opcode = FetchOpcode();
        var x = opcode >> 6;
        var y = (opcode >> 3) & 7;
        var z = opcode & 7;

        switch (x)
        {
            case 0: ExecuteX0(y, z); break;
            case 1: ExecuteX1(y, z); break;
            case 2: Alu(y, ReadRegisterOrMemory(z)); break;
            default: ExecuteX3(opcode, y, z); break;
        }
    }

    private void ExecuteX0(int y, int z)
    {
        var p = y >> 1;
        var q = y & 1;

        switch (z)
        {
            case 0:
                ExecuteRelativeJumps(y);
                break;

            case 1:
                if (q == 0)
                {
                    SetRegisterPair(p, ReadOperandWord());
                }
                else
                {
                    Internal(7);
                    HL = Add16(HL, GetRegisterPair(p));
                }

                break;

            case 2:
                ExecuteIndirectLoads(p, q);
                break;

            case 3:
                // INC rr / DEC rr: flags untouched.
                Internal(2);
                SetRegisterPair(p, (ushort)(GetRegisterPair(p) + (q == 0 ? 1 : -1)));
                break;

            case 4:
                if (y == 6)
                {
                    var value = ReadByte(HL);
                    Internal(1);
                    WriteByte(HL, Inc(value));
                }
                else
                {
                    SetRegister(y, Inc(GetRegister(y)));
                }

                break;

            case 5:
                if (y == 6)
                {
                    var value = ReadByte(HL);
                    Internal(1);
                    WriteByte(HL, Dec(value));
                }
                else
                {
                    SetRegister(y, Dec(GetRegister(y)));
                }

                break;

            case 6:
                var operand = ReadOperand();
                if (y == 6)
                {
                    WriteByte(HL, operand);
                }
                else
                {
                    SetRegister(y, operand);
                }

                break;

            default:
                ExecuteAccumulatorOperations(y);
                break;
        }
    }

    /// <summary>NOP, EX AF,AF', DJNZ, JR and JR cc.</summary>
    private void ExecuteRelativeJumps(int y)
    {
        switch (y)
        {
            case 0:
                break;

            case 1:
                (A, A_) = (A_, A);
                (F, F_) = (F_, F);
                break;

            case 2:
                Internal(1);
                B--;
                JumpRelative(B != 0);
                break;

            case 3:
                JumpRelative(true);
                break;

            default:
                JumpRelative(Condition(y - 4));
                break;
        }
    }

    private void JumpRelative(bool taken)
    {
        var offset = (sbyte)ReadOperand();
        if (taken)
        {
            Internal(5);
            PC = (ushort)(PC + offset);
            WZ = PC;
        }
    }

    /// <summary>LD (BC)/(DE)/(nn) from A or HL, and the reverse loads.</summary>
    private void ExecuteIndirectLoads(int p, int q)
    {
        ushort address;

        switch (p)
        {
            case 0:
            case 1:
                address = p == 0 ? BC : DE;
                if (q == 0)
                {
                    WriteByte(address, A);
                    WZ = (ushort)((A << 8) | ((address + 1) & 0xFF));
                }
                else
                {
                    A = ReadByte(address);
                    WZ = (ushort)(address + 1);
                }

                break;

            case 2:
                address = ReadOperandWord();
                if (q == 0)
                {
                    WriteByte(address, L);
                    WriteByte((ushort)(address + 1), H);
                }
                else
                {
                    L = ReadByte(address);
                    H = ReadByte((ushort)(address + 1));
                }

                WZ = (ushort)(address + 1);
                break;

            default:
                address = ReadOperandWord();
                if (q == 0)
                {
                    WriteByte(address, A);
                    WZ = (ushort)((A << 8) | ((address + 1) & 0xFF));
                }
                else
                {
                    A = ReadByte(address);
                    WZ = (ushort)(address + 1);
                }

                break;
        }
    }

    /// <summary>RLCA, RRCA, RLA, RRA, DAA, CPL, SCF, CCF.</summary>
    private void ExecuteAccumulatorOperations(int y)
    {
        int carry;

        switch (y)
        {
            case 0:
                carry = A >> 7;
                A = (byte)((A << 1) | carry);
                SetAccumulatorRotateFlags(carry);
                break;

            case 1:
                carry = A & 1;
                A = (byte)((A >> 1) | (carry << 7));
                SetAccumulatorRotateFlags(carry);
                break;

            case 2:
                carry = A >> 7;
                A = (byte)((A << 1) | (F & FlagC));
                SetAccumulatorRotateFlags(carry);
                break;

            case 3:
                carry = A & 1;
                A = (byte)((A >> 1) | ((F & FlagC) << 7));
                SetAccumulatorRotateFlags(carry);
                break;

            case 4:
                Daa();
                break;

            case 5:
                A = (byte)~A;
                F = (byte)((F & (FlagS | FlagZ | FlagPV | FlagC)) | FlagH | FlagN | (A & (Flag5 | Flag3)));
                break;

            // SCF and CCF take bits 3 and 5 from A | F, as FUSE does. Real hardware depends on
            // whether the previous instruction changed F (the "Q" register), which FUSE ignores.
            case 6:
                F = (byte)((F & (FlagS | FlagZ | FlagPV)) | ((A | F) & (Flag5 | Flag3)) | FlagC);
                break;

            default:
                F = (byte)((F & (FlagS | FlagZ | FlagPV))
                    | ((A | F) & (Flag5 | Flag3))
                    | ((F & FlagC) != 0 ? FlagH : FlagC));
                break;
        }
    }

    /// <summary>LD r,r' and HALT (the LD (HL),(HL) slot).</summary>
    private void ExecuteX1(int y, int z)
    {
        if (y == 6 && z == 6)
        {
            // HALT re-executes itself (as NOPs) until an interrupt: PC stays on the opcode.
            Halted = true;
            PC--;
            return;
        }

        if (y == 6)
        {
            WriteByte(HL, GetRegister(z));
        }
        else
        {
            SetRegister(y, ReadRegisterOrMemory(z));
        }
    }

    private void ExecuteX3(byte opcode, int y, int z)
    {
        var p = y >> 1;
        var q = y & 1;
        ushort address;

        switch (z)
        {
            case 0:
                // RET cc
                Internal(1);
                if (Condition(y))
                {
                    PC = Pop();
                    WZ = PC;
                }

                break;

            case 1:
                if (q == 0)
                {
                    SetRegisterPair2(p, Pop());
                }
                else
                {
                    switch (p)
                    {
                        case 0:
                            PC = Pop();
                            WZ = PC;
                            break;
                        case 1:
                            (B, B_) = (B_, B);
                            (C, C_) = (C_, C);
                            (D, D_) = (D_, D);
                            (E, E_) = (E_, E);
                            (H, H_) = (H_, H);
                            (L, L_) = (L_, L);
                            break;
                        case 2:
                            PC = HL;
                            break;
                        default:
                            Internal(2);
                            SP = HL;
                            break;
                    }
                }

                break;

            case 2:
                // JP cc,nn: the address is read and put in WZ whether or not the jump is taken.
                address = ReadOperandWord();
                WZ = address;
                if (Condition(y))
                {
                    PC = address;
                }

                break;

            case 3:
                ExecuteX3Z3(opcode, y);
                break;

            case 4:
                address = ReadOperandWord();
                WZ = address;
                if (Condition(y))
                {
                    Call(address);
                }

                break;

            case 5:
                if (q == 0)
                {
                    Internal(1);
                    Push(GetRegisterPair2(p));
                }
                else if (p == 0)
                {
                    address = ReadOperandWord();
                    WZ = address;
                    Call(address);
                }
                else
                {
                    throw PrefixNotImplemented(opcode);
                }

                break;

            case 6:
                Alu(y, ReadOperand());
                break;

            default:
                // RST
                Internal(1);
                Push(PC);
                PC = (ushort)(y * 8);
                WZ = PC;
                break;
        }
    }

    /// <summary>JP nn, CB prefix, OUT (n),A, IN A,(n), EX (SP),HL, EX DE,HL, DI, EI.</summary>
    private void ExecuteX3Z3(byte opcode, int y)
    {
        switch (y)
        {
            case 0:
                PC = ReadOperandWord();
                WZ = PC;
                break;

            case 1:
                throw PrefixNotImplemented(opcode);

            case 2:
            {
                var port = ReadOperand();
                WritePort((ushort)((A << 8) | port), A);
                WZ = (ushort)((A << 8) | ((port + 1) & 0xFF));
                break;
            }

            case 3:
            {
                var port = (ushort)((A << 8) | ReadOperand());
                A = ReadPort(port);
                WZ = (ushort)(port + 1);
                break;
            }

            case 4:
            {
                var low = ReadByte(SP);
                var high = ReadByte((ushort)(SP + 1));
                Internal(1);
                WriteByte((ushort)(SP + 1), H);
                WriteByte(SP, L);
                Internal(2);
                H = high;
                L = low;
                WZ = HL;
                break;
            }

            case 5:
                (D, H) = (H, D);
                (E, L) = (L, E);
                break;

            case 6:
                IFF1 = IFF2 = false;
                break;

            default:
                IFF1 = IFF2 = true;
                break;
        }
    }

    /// <summary>Pushes the return address and jumps. The extra T-state comes before the push.</summary>
    private void Call(ushort address)
    {
        Internal(1);
        Push(PC);
        PC = address;
    }

    private static NotImplementedException PrefixNotImplemented(byte prefix) =>
        new($"Prefix 0x{prefix:X2} is not implemented yet.");

    /// <summary>Conditions in opcode order: NZ, Z, NC, C, PO, PE, P, M.</summary>
    private bool Condition(int index) => index switch
    {
        0 => (F & FlagZ) == 0,
        1 => (F & FlagZ) != 0,
        2 => (F & FlagC) == 0,
        3 => (F & FlagC) != 0,
        4 => (F & FlagPV) == 0,
        5 => (F & FlagPV) != 0,
        6 => (F & FlagS) == 0,
        _ => (F & FlagS) != 0,
    };

    /// <summary>r[index] in opcode order B, C, D, E, H, L, (HL), A; index 6 reads memory.</summary>
    private byte ReadRegisterOrMemory(int index) => index == 6 ? ReadByte(HL) : GetRegister(index);

    /// <summary>r[index] for every index except 6, which is (HL) and goes through the bus.</summary>
    private byte GetRegister(int index) => index switch
    {
        0 => B,
        1 => C,
        2 => D,
        3 => E,
        4 => H,
        5 => L,
        _ => A,
    };

    private void SetRegister(int index, byte value)
    {
        switch (index)
        {
            case 0: B = value; break;
            case 1: C = value; break;
            case 2: D = value; break;
            case 3: E = value; break;
            case 4: H = value; break;
            case 5: L = value; break;
            default: A = value; break;
        }
    }

    /// <summary>rp[index]: BC, DE, HL, SP.</summary>
    private ushort GetRegisterPair(int index) => index switch
    {
        0 => BC,
        1 => DE,
        2 => HL,
        _ => SP,
    };

    private void SetRegisterPair(int index, ushort value)
    {
        switch (index)
        {
            case 0: BC = value; break;
            case 1: DE = value; break;
            case 2: HL = value; break;
            default: SP = value; break;
        }
    }

    /// <summary>rp2[index]: BC, DE, HL, AF (PUSH and POP).</summary>
    private ushort GetRegisterPair2(int index) => index == 3 ? AF : GetRegisterPair(index);

    private void SetRegisterPair2(int index, ushort value)
    {
        if (index == 3)
        {
            AF = value;
        }
        else
        {
            SetRegisterPair(index, value);
        }
    }
}
