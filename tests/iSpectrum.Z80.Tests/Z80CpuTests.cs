using iSpectrum.Z80;

namespace iSpectrum.Z80.Tests;

public class Z80CpuTests
{
    private sealed class FlatMemory : IMemory
    {
        private readonly byte[] _ram = new byte[0x10000];

        public byte Read(ushort address) => _ram[address];

        public void Write(ushort address, byte value) => _ram[address] = value;
    }

    private sealed class NullIo : IIo
    {
        public byte In(ushort port) => 0xFF;

        public void Out(ushort port, byte value) { }
    }

    [Fact]
    public void Reset_SetsPowerOnState()
    {
        var cpu = new Z80Cpu(new FlatMemory(), new NullIo());

        Assert.Equal(0xFF, cpu.A);
        Assert.Equal(0xFF, cpu.F);
        Assert.Equal(0xFFFF, cpu.SP);
        Assert.Equal(0, cpu.PC);
        Assert.False(cpu.IFF1);
        Assert.Equal(0, cpu.IM);
        Assert.Equal(0, cpu.TStates);
    }

    [Fact]
    public void Z80Assembly_DependsOnlyOnTheBcl()
    {
        var references = typeof(Z80Cpu).Assembly.GetReferencedAssemblies();

        foreach (var reference in references)
        {
            Assert.True(
                reference.Name is not null
                    && (reference.Name == "netstandard" || reference.Name.StartsWith("System", StringComparison.Ordinal)),
                $"iSpectrum.Z80 must not reference {reference.Name}");
        }
    }
}
