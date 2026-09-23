// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80;

// Decoding follows "Decoding Z80 Opcodes" (Cristian Dinu): an opcode splits into
// x = bits 7-6, y = bits 5-3, z = bits 2-0, p = y >> 1, q = y & 1.
public sealed partial class Z80Cpu
{
    /// <summary>Fetches, decodes and executes one instruction, adding its exact T-states.</summary>
    public void Step()
    {
        _interruptsBlocked = false;
        var opcode = FetchOpcode();
        _index = IndexHL;

        // DD and FD only select IX or IY for the next opcode, at the cost of one opcode fetch.
        // In a chain of prefixes the last one wins; interrupts cannot occur in between.
        while (opcode is 0xDD or 0xFD)
        {
            _index = opcode == 0xDD ? IndexIX : IndexIY;
            opcode = FetchOpcode();
        }

        var x = opcode >> 6;
        var y = (opcode >> 3) & 7;
        var z = opcode & 7;

        switch (x)
        {
            case 0: ExecuteX0(y, z); break;
            case 1: ExecuteX1(y, z); break;
            case 2: Alu(y, ReadRegisterOrMemory(z)); break;
            default: ExecuteX3(y, z); break;
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
                    Internal(IR, 7);
                    IndexRegister = Add16(IndexRegister, GetRegisterPair(p));
                }

                break;

            case 2:
                ExecuteIndirectLoads(p, q);
                break;

            case 3:
                // INC rr / DEC rr: flags untouched.
                Internal(IR, 2);
                SetRegisterPair(p, (ushort)(GetRegisterPair(p) + (q == 0 ? 1 : -1)));
                break;

            case 4:
                if (y == 6)
                {
                    var address = MemoryOperandAddress();
                    var value = ReadByte(address);
                    Internal(address, 1);
                    WriteByte(address, Inc(value));
                }
                else
                {
                    SetRegister(y, Inc(GetRegister(y)));
                }

                break;

            case 5:
                if (y == 6)
                {
                    var address = MemoryOperandAddress();
                    var value = ReadByte(address);
                    Internal(address, 1);
                    WriteByte(address, Dec(value));
                }
                else
                {
                    SetRegister(y, Dec(GetRegister(y)));
                }

                break;

            case 6:
                if (y != 6)
                {
                    SetRegister(y, ReadOperand());
                }
                else if (_index == IndexHL)
                {
                    WriteByte(HL, ReadOperand());
                }
                else
                {
                    // LD (IX+d),n: the displacement and n are read first, then 2 internal T-states.
                    var address = IndexedAddress(ReadOperand());
                    var operand = ReadOperand();
                    Internal((ushort)(PC - 1), 2);
                    WriteByte(address, operand);
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
                Internal(IR, 1);
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
        if (!taken)
        {
            // The displacement's cycle still runs (and can be stalled), but FUSE does not read
            // the byte: its tests expect no memory read here.
            Contend(PC);
            TStates += 3;
            PC++;
            return;
        }

        var offset = (sbyte)ReadOperand();
        Internal((ushort)(PC - 1), 5);
        PC = (ushort)(PC + offset);
        WZ = PC;
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
                    var value = IndexRegister;
                    WriteByte(address, (byte)value);
                    WriteByte((ushort)(address + 1), (byte)(value >> 8));
                }
                else
                {
                    var low = ReadByte(address);
                    IndexRegister = (ushort)(low | (ReadByte((ushort)(address + 1)) << 8));
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

        // With a memory operand, H and L stay H and L: LD H,(IX+d) loads H, not IXH.
        if (y == 6)
        {
            WriteByte(MemoryOperandAddress(), GetPlainRegister(z));
        }
        else if (z == 6)
        {
            SetPlainRegister(y, ReadByte(MemoryOperandAddress()));
        }
        else
        {
            SetRegister(y, GetRegister(z));
        }
    }

    private void ExecuteX3(int y, int z)
    {
        var p = y >> 1;
        var q = y & 1;
        ushort address;

        switch (z)
        {
            case 0:
                // RET cc
                Internal(IR, 1);
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
                            PC = IndexRegister;
                            break;
                        default:
                            Internal(IR, 2);
                            SP = IndexRegister;
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
                ExecuteX3Z3(y);
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
                    Internal(IR, 1);
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
                    // p == 2; the DD and FD slots (p == 1 and 3) are consumed by Step.
                    ExecuteEd();
                }

                break;

            case 6:
                Alu(y, ReadOperand());
                break;

            default:
                // RST
                Internal(IR, 1);
                Push(PC);
                PC = (ushort)(y * 8);
                WZ = PC;
                break;
        }
    }

    /// <summary>JP nn, CB prefix, OUT (n),A, IN A,(n), EX (SP),HL, EX DE,HL, DI, EI.</summary>
    private void ExecuteX3Z3(int y)
    {
        switch (y)
        {
            case 0:
                PC = ReadOperandWord();
                WZ = PC;
                break;

            case 1:
                if (_index == IndexHL)
                {
                    ExecuteCb();
                }
                else
                {
                    ExecuteIndexedCb();
                }

                break;

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
                var value = IndexRegister;
                Internal((ushort)(SP + 1), 1);
                WriteByte((ushort)(SP + 1), (byte)(value >> 8));
                WriteByte(SP, (byte)value);
                Internal(SP, 2);
                IndexRegister = (ushort)(low | (high << 8));
                WZ = IndexRegister;
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
                _interruptsBlocked = true;
                break;
        }
    }

    /// <summary>
    /// Pushes the return address and jumps. The extra T-state comes before the push, with the
    /// address of the operand's high byte still on the bus.
    /// </summary>
    private void Call(ushort address)
    {
        Internal((ushort)(PC - 1), 1);
        Push(PC);
        PC = address;
    }

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
    private byte ReadRegisterOrMemory(int index) =>
        index == 6 ? ReadByte(MemoryOperandAddress()) : GetRegister(index);

    /// <summary>
    /// Address of the (HL) operand. After DD or FD it is (IX+d) or (IY+d): the displacement is
    /// read, then 5 internal T-states, and MEMPTR takes the address.
    /// </summary>
    private ushort MemoryOperandAddress()
    {
        if (_index == IndexHL)
        {
            return HL;
        }

        var address = IndexedAddress(ReadOperand());
        Internal((ushort)(PC - 1), 5);
        return address;
    }

    private ushort IndexedAddress(byte displacement)
    {
        var address = (ushort)(IndexRegister + (sbyte)displacement);
        WZ = address;
        return address;
    }

    /// <summary>r[index] except 6; after DD or FD, H and L become the halves of IX or IY.</summary>
    private byte GetRegister(int index)
    {
        if (_index == IndexHL || index is not (4 or 5))
        {
            return GetPlainRegister(index);
        }

        var value = IndexRegister;
        return index == 4 ? (byte)(value >> 8) : (byte)value;
    }

    private void SetRegister(int index, byte value)
    {
        if (_index == IndexHL || index is not (4 or 5))
        {
            SetPlainRegister(index, value);
            return;
        }

        var pair = IndexRegister;
        IndexRegister = index == 4 ? (ushort)((value << 8) | (pair & 0xFF)) : (ushort)((pair & 0xFF00) | value);
    }

    /// <summary>r[index] without the IX/IY substitution.</summary>
    private byte GetPlainRegister(int index) => index switch
    {
        0 => B,
        1 => C,
        2 => D,
        3 => E,
        4 => H,
        5 => L,
        _ => A,
    };

    private void SetPlainRegister(int index, byte value)
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

    /// <summary>rp[index]: BC, DE, HL (IX or IY after a prefix), SP.</summary>
    private ushort GetRegisterPair(int index) => index switch
    {
        0 => BC,
        1 => DE,
        2 => IndexRegister,
        _ => SP,
    };

    private void SetRegisterPair(int index, ushort value)
    {
        switch (index)
        {
            case 0: BC = value; break;
            case 1: DE = value; break;
            case 2: IndexRegister = value; break;
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
