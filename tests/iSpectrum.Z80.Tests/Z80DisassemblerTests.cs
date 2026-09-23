// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80.Tests;

public class Z80DisassemblerTests
{
    private const ushort Start = 0x8000;

    /// <summary>"DD 36 05" as bytes (xUnit tells test cases apart by strings, not byte arrays).</summary>
    private static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));

    private static DisassembledInstruction Disassemble(byte[] code, Func<ushort, string?>? symbol = null)
    {
        var memory = new FlatMemory();
        memory.Load(Start, code);
        return Z80Disassembler.Disassemble(memory.Read, Start, symbol);
    }

    [Theory]
    [InlineData("NOP", "00")]
    [InlineData("LD BC,$1234", "01 34 12")]
    [InlineData("LD (BC),A", "02")]
    [InlineData("EX AF,AF'", "08")]
    [InlineData("DJNZ $8000", "10 FE")]
    [InlineData("JR $8012", "18 10")]
    [InlineData("JR NZ,$7FF2", "20 F0")]
    [InlineData("LD ($5C00),HL", "22 00 5C")]
    [InlineData("LD A,($4000)", "3A 00 40")]
    [InlineData("INC (HL)", "34")]
    [InlineData("LD (HL),$FF", "36 FF")]
    [InlineData("SCF", "37")]
    [InlineData("LD B,C", "41")]
    [InlineData("HALT", "76")]
    [InlineData("ADD A,(HL)", "86")]
    [InlineData("XOR A", "AF")]
    [InlineData("RET NZ", "C0")]
    [InlineData("POP AF", "F1")]
    [InlineData("JP $0000", "C3 00 00")]
    [InlineData("OUT ($FE),A", "D3 FE")]
    [InlineData("EX (SP),HL", "E3")]
    [InlineData("JP (HL)", "E9")]
    [InlineData("CP $20", "FE 20")]
    [InlineData("RST $38", "FF")]
    [InlineData("RLC B", "CB 00")]
    [InlineData("SLL A", "CB 37")]
    [InlineData("BIT 7,(HL)", "CB 7E")]
    [InlineData("SET 0,E", "CB C3")]
    [InlineData("IN B,(C)", "ED 40")]
    [InlineData("IN (C)", "ED 70")]
    [InlineData("OUT (C),0", "ED 71")]
    [InlineData("SBC HL,DE", "ED 52")]
    [InlineData("LD ($8000),SP", "ED 73 00 80")]
    [InlineData("NEG", "ED 44")]
    [InlineData("IM 2", "ED 5E")]
    [InlineData("LD A,R", "ED 5F")]
    [InlineData("LDIR", "ED B0")]
    [InlineData("OTDR", "ED BB")]
    [InlineData("DB $ED,$00", "ED 00")]
    [InlineData("LD IX,$5C3A", "DD 21 3A 5C")]
    [InlineData("ADD IY,IY", "FD 29")]
    [InlineData("INC IXH", "DD 24")]
    [InlineData("LD IYL,$10", "FD 2E 10")]
    [InlineData("LD (IX+$05),$AA", "DD 36 05 AA")]
    [InlineData("LD H,(IX-$03)", "DD 66 FD")]
    [InlineData("LD (IY+$00),L", "FD 75 00")]
    [InlineData("LD IXH,IXL", "DD 65")]
    [InlineData("SUB (IY+$7F)", "FD 96 7F")]
    [InlineData("JP (IX)", "DD E9")]
    [InlineData("EX (SP),IY", "FD E3")]
    [InlineData("BIT 3,(IX+$02)", "DD CB 02 5E")]
    [InlineData("RLC (IX+$02),B", "DD CB 02 00")]
    [InlineData("SET 7,(IY-$01)", "FD CB FF FE")]
    [InlineData("NOP ; 1 ignored prefix", "DD 00")]
    [InlineData("EX DE,HL ; 1 ignored prefix", "DD EB")]
    [InlineData("LD IY,$0000 ; 1 ignored prefix", "DD FD 21 00 00")]
    [InlineData("LDIR ; 1 ignored prefix", "FD ED B0")]
    public void Instruction_IsWrittenAsExpected(string expected, string hex)
    {
        var code = Bytes(hex);
        var instruction = Disassemble(code);

        Assert.Equal(expected, instruction.Text);
        Assert.Equal(code.Length, instruction.Length);
    }

    [Fact]
    public void KnownAddresses_AreWrittenAsSymbols()
    {
        string? Symbols(ushort address) => address switch
        {
            0x9000 => "PrintScore",
            0x8005 => "Loop",
            0x5C00 => "Buffer",
            _ => null,
        };

        Assert.Equal("CALL PrintScore", Disassemble([0xCD, 0x00, 0x90], Symbols).Text);
        Assert.Equal("JR NZ,Loop", Disassemble([0x20, 0x03], Symbols).Text);
        Assert.Equal("LD HL,(Buffer)", Disassemble([0x2A, 0x00, 0x5C], Symbols).Text);
        Assert.Equal("LD A,$00", Disassemble([0x3E, 0x00], Symbols).Text); // 8-bit values stay numbers
    }

    public static TheoryData<string> AllOpcodes()
    {
        var data = new TheoryData<string>();
        foreach (var prefix in new[] { "", "CB ", "ED ", "DD ", "FD ", "DD CB 05 ", "FD CB 05 " })
        {
            for (var opcode = 0; opcode < 256; opcode++)
            {
                data.Add($"{prefix}{opcode:X2}");
            }
        }

        return data;
    }

    /// <summary>
    /// The disassembler and the CPU must agree on where every instruction ends: executed on its
    /// own, an instruction that does not jump leaves PC just after the bytes it was shown with.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllOpcodes))]
    public void Length_MatchesWhatTheCpuConsumes(string opcode)
    {
        // Operand bytes that keep every instruction simple: small displacements and values.
        byte[] code = [.. Bytes(opcode), 0x02, 0x03, 0x04, 0x05];
        var instruction = Disassemble(code);
        if (ChangesFlowOfControl(instruction.Text))
        {
            return;
        }

        var memory = new FlatMemory();
        memory.Load(Start, code);
        var cpu = new Z80Cpu(memory, new NullIo()) { PC = Start, SP = 0xF000 };

        // A single iteration of the repeating block instructions: the I/O ones count in B, the
        // others in BC.
        var ioBlock = instruction.Text.StartsWith("IN", StringComparison.Ordinal)
            || instruction.Text.StartsWith("OT", StringComparison.Ordinal);
        (cpu.B, cpu.C) = ioBlock ? ((byte)1, (byte)0) : ((byte)0, (byte)1);
        cpu.Step();

        Assert.Equal(instruction.Length, cpu.PC - Start);
    }

    private static bool ChangesFlowOfControl(string text) =>
        text.StartsWith("JP", StringComparison.Ordinal) || text.StartsWith("JR", StringComparison.Ordinal)
        || text.StartsWith("CALL", StringComparison.Ordinal) || text.StartsWith("RET", StringComparison.Ordinal)
        || text.StartsWith("RST", StringComparison.Ordinal) || text.StartsWith("HALT", StringComparison.Ordinal)
        || text.StartsWith("DJNZ", StringComparison.Ordinal);
}
