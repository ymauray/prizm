// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Z80.Tests.Fuse;

/// <summary>Runs every test of the FUSE Z80 suite (tests.in against tests.expected).</summary>
[Trait("Category", "Fuse")]
public class FuseTests
{
    private static readonly Lazy<(Dictionary<string, FuseInput> Inputs, Dictionary<string, FuseExpected> Expected)> Suite =
        new(LoadSuite);

    public static TheoryData<string> TestNames()
    {
        var names = new TheoryData<string>();

        foreach (var name in Suite.Value.Inputs.Keys)
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(TestNames))]
    public void Instruction(string name)
    {
        var input = Suite.Value.Inputs[name];
        var expected = Suite.Value.Expected[name];

        var events = new List<FuseEvent>();
        var memory = new FuseMemory(events);
        foreach (var block in input.Memory)
        {
            memory.Load(block);
        }

        var io = new FuseIo(events);
        var cpu = new Z80Cpu(memory, io);
        memory.Clock = io.Clock = cpu;
        SetState(cpu, input.State);

        // The last instruction is allowed to finish past the requested T-state count.
        while (cpu.TStates < input.State.TStates)
        {
            cpu.Step();
        }

        AssertSameState(name, expected.State, GetState(cpu));
        AssertSameEvents(expected.Events, events);

        var expectedMemory = new FuseMemory();
        foreach (var block in input.Memory)
        {
            expectedMemory.Load(block);
        }

        foreach (var block in expected.ChangedMemory)
        {
            expectedMemory.Load(block);
        }

        for (var address = 0; address < 0x10000; address++)
        {
            var actual = memory.Peek((ushort)address);
            var wanted = expectedMemory.Peek((ushort)address);
            Assert.True(actual == wanted, $"Memory at {address:X4}: expected {wanted:X2}, got {actual:X2}");
        }
    }

    /// <summary>
    /// Cases that stop a block instruction right after one repeat. FUSE predates David Banks's
    /// 2018 findings: while INIR, OTIR, CPDR and the like repeat, flag bits 5 and 3 come from PC,
    /// the I/O ones also recompute H and P/V, and MEMPTR becomes PC + 1. z80test, measured on a
    /// real 48K, checks that behaviour, so here it wins over FUSE's expected values.
    /// </summary>
    private static readonly HashSet<string> InterruptedBlockCases = ["edb2_1", "edb3_1", "edb9_2", "edba_1", "edbb_1"];

    /// <summary>Flag bits a repeating block instruction sets differently from FUSE: 5, H, 3, P/V.</summary>
    private const int InterruptedBlockFlags = 0x3C;

    private static void AssertSameState(string name, FuseCpuState expected, FuseCpuState actual)
    {
        if (!InterruptedBlockCases.Contains(name))
        {
            Assert.Equal(expected, actual);
            return;
        }

        // Everything else still has to match FUSE exactly.
        Assert.Equal(0, (expected.AF ^ actual.AF) & ~InterruptedBlockFlags);
        Assert.Equal((ushort)(actual.PC + 1), actual.MEMPTR);
        Assert.Equal(expected with { AF = actual.AF, MEMPTR = actual.MEMPTR }, actual);
    }

    /// <summary>
    /// Every bus event, in order and at the right T-state: memory reads and writes, port reads
    /// and writes, and the contention points (MC for memory and internal cycles, PC for I/O).
    /// </summary>
    private static void AssertSameEvents(IReadOnlyList<FuseEvent> expected, List<FuseEvent> actual)
    {
        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            Assert.True(expected[i] == actual[i], $"Bus event {i}: expected {expected[i]}, got {actual[i]}");
        }

        Assert.True(expected.Count == actual.Count, $"Expected {expected.Count} bus events, got {actual.Count}");
    }

    private static (Dictionary<string, FuseInput>, Dictionary<string, FuseExpected>) LoadSuite()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fuse");
        var inputs = FuseTestFile.ParseInput(Path.Combine(directory, "tests.in")).ToDictionary(t => t.Name);
        var expected = FuseTestFile.ParseExpected(Path.Combine(directory, "tests.expected")).ToDictionary(t => t.Name);

        return (inputs, expected);
    }

    private static void SetState(Z80Cpu cpu, FuseCpuState s)
    {
        (cpu.A, cpu.F) = Split(s.AF);
        (cpu.B, cpu.C) = Split(s.BC);
        (cpu.D, cpu.E) = Split(s.DE);
        (cpu.H, cpu.L) = Split(s.HL);
        (cpu.A_, cpu.F_) = Split(s.AF_);
        (cpu.B_, cpu.C_) = Split(s.BC_);
        (cpu.D_, cpu.E_) = Split(s.DE_);
        (cpu.H_, cpu.L_) = Split(s.HL_);
        cpu.IX = s.IX;
        cpu.IY = s.IY;
        cpu.SP = s.SP;
        cpu.PC = s.PC;
        cpu.WZ = s.MEMPTR;
        cpu.I = s.I;
        cpu.R = s.R;
        cpu.IFF1 = s.IFF1;
        cpu.IFF2 = s.IFF2;
        cpu.IM = s.IM;
        cpu.Halted = s.Halted;

        // In tests.in the T-state field is the run length; the run always starts at 0.
        cpu.TStates = 0;
    }

    private static FuseCpuState GetState(Z80Cpu cpu) => new(
        Join(cpu.A, cpu.F), Join(cpu.B, cpu.C), Join(cpu.D, cpu.E), Join(cpu.H, cpu.L),
        Join(cpu.A_, cpu.F_), Join(cpu.B_, cpu.C_), Join(cpu.D_, cpu.E_), Join(cpu.H_, cpu.L_),
        cpu.IX, cpu.IY, cpu.SP, cpu.PC, cpu.WZ,
        cpu.I, cpu.R, cpu.IFF1, cpu.IFF2, cpu.IM, cpu.Halted, (int)cpu.TStates);

    private static (byte High, byte Low) Split(ushort value) => ((byte)(value >> 8), (byte)value);

    private static ushort Join(byte high, byte low) => (ushort)((high << 8) | low);

    /// <summary>
    /// Flat RAM that logs every access like FUSE's test harness. Contention points are logged
    /// (MC) but add no delay; 0x4000-0x7FFF counts as contended, which shapes I/O cycles.
    /// </summary>
    private sealed class FuseMemory(List<FuseEvent>? events = null) : IMemory
    {
        private readonly byte[] _ram = new byte[0x10000];

        public Z80Cpu? Clock { get; set; }

        private int Now => (int)(Clock?.TStates ?? 0);

        public void Load(FuseMemoryBlock block)
        {
            for (var j = 0; j < block.Bytes.Length; j++)
            {
                _ram[(ushort)(block.Address + j)] = block.Bytes[j];
            }
        }

        public byte Peek(ushort address) => _ram[address];

        public byte Read(ushort address)
        {
            events?.Add(new FuseEvent(Now, "MR", address, _ram[address]));
            return _ram[address];
        }

        public void Write(ushort address, byte value)
        {
            events?.Add(new FuseEvent(Now, "MW", address, value));
            _ram[address] = value;
        }

        public int ContentionDelay(ushort address, long tStates)
        {
            events?.Add(new FuseEvent((int)tStates, "MC", address, null));
            return 0;
        }

        public bool IsContended(ushort address) => (address & 0xC000) == 0x4000;
    }

    /// <summary>FUSE's test harness returns the high byte of the port on reads, and logs every access.</summary>
    private sealed class FuseIo(List<FuseEvent> events) : IIo
    {
        public Z80Cpu? Clock { get; set; }

        private int Now => (int)(Clock?.TStates ?? 0);

        public byte In(ushort port)
        {
            var value = (byte)(port >> 8);
            events.Add(new FuseEvent(Now, "PR", port, value));
            return value;
        }

        public void Out(ushort port, byte value) => events.Add(new FuseEvent(Now, "PW", port, value));

        public int ContentionDelay(ushort port, long tStates)
        {
            events.Add(new FuseEvent((int)tStates, "PC", port, null));
            return 0;
        }
    }
}
