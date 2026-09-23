namespace iSpectrum.Z80.Tests.Fuse;

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

        var memory = new FuseMemory();
        foreach (var block in input.Memory)
        {
            memory.Load(block);
        }

        var cpu = new Z80Cpu(memory, new FuseIo());
        SetState(cpu, input.State);

        // The last instruction is allowed to finish past the requested T-state count.
        while (cpu.TStates < input.State.TStates)
        {
            cpu.Step();
        }

        Assert.Equal(expected.State, GetState(cpu));

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
            var actual = memory.Read((ushort)address);
            var wanted = expectedMemory.Read((ushort)address);
            Assert.True(actual == wanted, $"Memory at {address:X4}: expected {wanted:X2}, got {actual:X2}");
        }
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

    private sealed class FuseMemory : IMemory
    {
        private readonly byte[] _ram = new byte[0x10000];

        public void Load(FuseMemoryBlock block)
        {
            for (var j = 0; j < block.Bytes.Length; j++)
            {
                _ram[(ushort)(block.Address + j)] = block.Bytes[j];
            }
        }

        public byte Read(ushort address) => _ram[address];

        public void Write(ushort address, byte value) => _ram[address] = value;
    }

    /// <summary>FUSE's test harness returns the high byte of the port on reads.</summary>
    private sealed class FuseIo : IIo
    {
        public byte In(ushort port) => (byte)(port >> 8);

        public void Out(ushort port, byte value) { }
    }
}
