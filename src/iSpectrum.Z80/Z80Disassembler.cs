// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80;

/// <summary>One disassembled instruction: its address, its length in bytes, and its text.</summary>
public readonly record struct DisassembledInstruction(ushort Address, int Length, string Text);

/// <summary>
/// Turns Z80 machine code into assembly text, for every opcode including the undocumented ones,
/// the DD/FD (IX/IY) prefixes and DDCB/FDCB. Decoding follows the x/y/z split of "Decoding Z80
/// Opcodes" (Cristian Dinu), like the CPU. Numbers are written in hexadecimal as $12 and $1234;
/// jump and call targets, and 16-bit addresses, are replaced by a symbol when one is known.
/// </summary>
/// <remarks>
/// An instruction is what <see cref="Z80Cpu.Step"/> executes at once: DD or FD prefixes that
/// change nothing (before an opcode that does not use HL, or before another prefix) belong to the
/// instruction that follows, and are shown as a comment.
/// </remarks>
public static class Z80Disassembler
{
    private static readonly string[] Registers = ["B", "C", "D", "E", "H", "L", "(HL)", "A"];
    private static readonly string[] Pairs = ["BC", "DE", "HL", "SP"];
    private static readonly string[] Pairs2 = ["BC", "DE", "HL", "AF"];
    private static readonly string[] Conditions = ["NZ", "Z", "NC", "C", "PO", "PE", "P", "M"];
    private static readonly string[] AluOperations = ["ADD A,", "ADC A,", "SUB ", "SBC A,", "AND ", "XOR ", "OR ", "CP "];
    private static readonly string[] Rotations = ["RLC", "RRC", "RL", "RR", "SLA", "SRA", "SLL", "SRL"];
    private static readonly string[] AccumulatorOperations = ["RLCA", "RRCA", "RLA", "RRA", "DAA", "CPL", "SCF", "CCF"];
    private static readonly string[] InterruptModes = ["0", "0/1", "1", "2", "0", "0/1", "1", "2"];

    private static readonly string[][] BlockInstructions =
    [
        ["LDI", "CPI", "INI", "OUTI"],
        ["LDD", "CPD", "IND", "OUTD"],
        ["LDIR", "CPIR", "INIR", "OTIR"],
        ["LDDR", "CPDR", "INDR", "OTDR"],
    ];

    /// <summary>
    /// Disassembles the instruction at <paramref name="address"/>, reading memory through
    /// <paramref name="read"/>. <paramref name="symbol"/>, if given, names addresses.
    /// </summary>
    public static DisassembledInstruction Disassemble(
        Func<ushort, byte> read, ushort address, Func<ushort, string?>? symbol = null)
    {
        var decoder = new Decoder(read, address, symbol);
        var text = decoder.Decode();
        return new DisassembledInstruction(address, decoder.Length, text);
    }

    /// <summary>Reads the bytes of one instruction in order, and writes its text.</summary>
    private sealed class Decoder(Func<ushort, byte> read, ushort start, Func<ushort, string?>? symbol)
    {
        private int _offset;

        /// <summary>"HL", "IX" or "IY", as set by the prefix.</summary>
        private string _index = "HL";

        /// <summary>The displacement of an (IX+d) operand, read once it is needed.</summary>
        private sbyte? _displacement;

        public int Length => _offset;

        private bool Indexed => _index != "HL";

        public string Decode()
        {
            var opcode = Next();
            var ignoredPrefixes = 0;

            // As in the CPU, the last of a chain of DD/FD prefixes wins.
            while (opcode is 0xDD or 0xFD)
            {
                if (Indexed)
                {
                    ignoredPrefixes++;
                }

                _index = opcode == 0xDD ? "IX" : "IY";
                opcode = Next();
            }

            if (opcode == 0xED)
            {
                // ED ignores a DD/FD prefix.
                if (Indexed)
                {
                    ignoredPrefixes++;
                }

                _index = "HL";
                return Comment(DecodeEd(Next()), ignoredPrefixes);
            }

            if (opcode == 0xCB)
            {
                return Comment(Indexed ? DecodeIndexedCb() : DecodeCb(Next()), ignoredPrefixes);
            }

            var text = DecodeMain(opcode);
            if (Indexed && !UsesIndex(opcode))
            {
                ignoredPrefixes++;
            }

            return Comment(text, ignoredPrefixes);
        }

        private static string Comment(string text, int ignoredPrefixes) =>
            ignoredPrefixes == 0 ? text : $"{text} ; {ignoredPrefixes} ignored prefix{(ignoredPrefixes > 1 ? "es" : string.Empty)}";

        /// <summary>Whether an unprefixed opcode involves HL, H, L or (HL), which DD/FD change.</summary>
        private static bool UsesIndex(byte opcode)
        {
            var x = opcode >> 6;
            var y = (opcode >> 3) & 7;
            var z = opcode & 7;
            var p = y >> 1;

            return x switch
            {
                0 => (z == 1 && (p == 2 || (y & 1) == 1)) // LD HL,nn / ADD HL,rr
                    || (z == 2 && p == 2)                  // LD (nn),HL / LD HL,(nn)
                    || (z == 3 && p == 2)                  // INC/DEC HL
                    || (z is 4 or 5 or 6 && y is 4 or 5 or 6),
                1 => opcode != 0x76 && (y is 4 or 5 or 6 || z is 4 or 5 or 6), // not HALT
                2 => z is 4 or 5 or 6,
                _ => opcode is 0xE1 or 0xE3 or 0xE5 or 0xE9 or 0xF9,
            };
        }

        private byte Next() => read((ushort)(start + _offset++));

        private string Byte() => $"${Next():X2}";

        private ushort Word()
        {
            var low = Next();
            return (ushort)(low | (Next() << 8));
        }

        private string Address(ushort value) => symbol?.Invoke(value) ?? $"${value:X4}";

        private string RelativeTarget()
        {
            var offset = (sbyte)Next();
            return Address((ushort)(start + _offset + offset));
        }

        /// <summary>(HL), or (IX+d) with its displacement, read the first time.</summary>
        private string Memory()
        {
            if (!Indexed)
            {
                return "(HL)";
            }

            _displacement ??= (sbyte)Next();
            var d = _displacement.Value;
            return d < 0 ? $"({_index}-${-d:X2})" : $"({_index}+${d:X2})";
        }

        /// <summary>r[i], with H and L turned into IXH/IXL (or IYH/IYL) unless (IX+d) is also used.</summary>
        private string Register(int i, bool plainHAndL = false)
        {
            if (i == 6)
            {
                return Memory();
            }

            if (Indexed && !plainHAndL && i is 4 or 5)
            {
                return _index + (i == 4 ? "H" : "L");
            }

            return Registers[i];
        }

        private string Pair(int p) => p == 2 ? _index : Pairs[p];

        private string Pair2(int p) => p == 2 ? _index : Pairs2[p];

        private string DecodeMain(byte opcode)
        {
            var x = opcode >> 6;
            var y = (opcode >> 3) & 7;
            var z = opcode & 7;
            var p = y >> 1;
            var q = y & 1;

            switch (x)
            {
                case 0:
                    return z switch
                    {
                        0 => y switch
                        {
                            0 => "NOP",
                            1 => "EX AF,AF'",
                            2 => $"DJNZ {RelativeTarget()}",
                            3 => $"JR {RelativeTarget()}",
                            _ => $"JR {Conditions[y - 4]},{RelativeTarget()}",
                        },
                        1 => q == 0 ? $"LD {Pair(p)},{Address(Word())}" : $"ADD {_index},{Pair(p)}",
                        2 => (p, q) switch
                        {
                            (0, 0) => "LD (BC),A",
                            (1, 0) => "LD (DE),A",
                            (2, 0) => $"LD ({Address(Word())}),{_index}",
                            (3, 0) => $"LD ({Address(Word())}),A",
                            (0, _) => "LD A,(BC)",
                            (1, _) => "LD A,(DE)",
                            (2, _) => $"LD {_index},({Address(Word())})",
                            _ => $"LD A,({Address(Word())})",
                        },
                        3 => $"{(q == 0 ? "INC" : "DEC")} {Pair(p)}",
                        4 => $"INC {Register(y)}",
                        5 => $"DEC {Register(y)}",
                        6 => $"LD {Register(y)},{Byte()}",
                        _ => AccumulatorOperations[y],
                    };

                case 1:
                    if (y == 6 && z == 6)
                    {
                        return "HALT";
                    }

                    // With (IX+d) on one side, H and L stay H and L on the other.
                    var plain = y == 6 || z == 6;
                    return $"LD {Register(y, plain)},{Register(z, plain)}";

                case 2:
                    return AluOperations[y] + Register(z);

                default:
                    return DecodeX3(y, z, p, q);
            }
        }

        private string DecodeX3(int y, int z, int p, int q)
        {
            switch (z)
            {
                case 0:
                    return $"RET {Conditions[y]}";
                case 1:
                    if (q == 0)
                    {
                        return $"POP {Pair2(p)}";
                    }

                    return p switch
                    {
                        0 => "RET",
                        1 => "EXX",
                        2 => $"JP ({_index})",
                        _ => $"LD SP,{_index}",
                    };
                case 2:
                    return $"JP {Conditions[y]},{Address(Word())}";
                case 3:
                    return y switch
                    {
                        0 => $"JP {Address(Word())}",
                        2 => $"OUT ({Byte()}),A",
                        3 => $"IN A,({Byte()})",
                        4 => $"EX (SP),{_index}",
                        5 => "EX DE,HL",
                        6 => "DI",
                        _ => "EI",
                    };
                case 4:
                    return $"CALL {Conditions[y]},{Address(Word())}";
                case 5:
                    return q == 0 ? $"PUSH {Pair2(p)}" : $"CALL {Address(Word())}";
                case 6:
                    return AluOperations[y] + Byte();
                default:
                    return $"RST ${y * 8:X2}";
            }
        }

        private static string DecodeCb(byte opcode)
        {
            var x = opcode >> 6;
            var y = (opcode >> 3) & 7;
            var register = Registers[opcode & 7];
            return x switch
            {
                0 => $"{Rotations[y]} {register}",
                1 => $"BIT {y},{register}",
                2 => $"RES {y},{register}",
                _ => $"SET {y},{register}",
            };
        }

        /// <summary>DDCB d op / FDCB d op; for z != 6 the result is also copied into r[z] (undocumented).</summary>
        private string DecodeIndexedCb()
        {
            var memory = Memory();
            var opcode = Next();
            var x = opcode >> 6;
            var y = (opcode >> 3) & 7;
            var z = opcode & 7;
            var copy = z == 6 || x == 1 ? string.Empty : "," + Registers[z];

            return x switch
            {
                0 => $"{Rotations[y]} {memory}{copy}",
                1 => $"BIT {y},{memory}",
                2 => $"RES {y},{memory}{copy}",
                _ => $"SET {y},{memory}{copy}",
            };
        }

        private string DecodeEd(byte opcode)
        {
            var x = opcode >> 6;
            var y = (opcode >> 3) & 7;
            var z = opcode & 7;
            var p = y >> 1;
            var q = y & 1;

            if (x == 2 && y >= 4 && z <= 3)
            {
                return BlockInstructions[y - 4][z];
            }

            if (x != 1)
            {
                return $"DB $ED,${opcode:X2}";
            }

            return z switch
            {
                0 => y == 6 ? "IN (C)" : $"IN {Registers[y]},(C)",
                1 => y == 6 ? "OUT (C),0" : $"OUT (C),{Registers[y]}",
                2 => $"{(q == 0 ? "SBC" : "ADC")} HL,{Pairs[p]}",
                3 => q == 0 ? $"LD ({Address(Word())}),{Pairs[p]}" : $"LD {Pairs[p]},({Address(Word())})",
                4 => "NEG",
                5 => y == 1 ? "RETI" : "RETN",
                6 => $"IM {InterruptModes[y]}",
                _ => y switch
                {
                    0 => "LD I,A",
                    1 => "LD R,A",
                    2 => "LD A,I",
                    3 => "LD A,R",
                    4 => "RRD",
                    5 => "RLD",
                    _ => $"DB $ED,${opcode:X2}",
                },
            };
        }
    }
}
