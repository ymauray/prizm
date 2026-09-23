using System.Text;
using Xunit.Abstractions;

namespace iSpectrum.Z80.Tests.Zex;

// ZEXDOC and ZEXALL each run about 47 billion T-states (under a minute in Release, several in
// Debug). They live in separate classes so that xUnit runs them in parallel.

[Trait("Category", "Slow")]
public class ZexdocTests(ITestOutputHelper output)
{
    [Fact]
    public void Zexdoc() => new CpmHarness(output).RunExerciser("zexdoc.com");
}

[Trait("Category", "Slow")]
public class ZexallTests(ITestOutputHelper output)
{
    [Fact]
    public void Zexall() => new CpmHarness(output).RunExerciser("zexall.com");
}

/// <summary>Minimal CP/M environment: the program at 0x0100 and the two BDOS console calls.</summary>
public sealed class CpmHarness(ITestOutputHelper output)
{
    /// <summary>Runs a ZEX exerciser; it fails if any group reports an error.</summary>
    public void RunExerciser(string program)
    {
        var console = RunCpmProgram(Path.Combine(AppContext.BaseDirectory, "Zex", program));

        Assert.DoesNotContain("ERROR", console);
        Assert.Contains("Tests complete", console);
    }

    private string RunCpmProgram(string path)
    {
        var memory = new CpmMemory();
        var image = File.ReadAllBytes(path);
        memory.Load(0x0100, image);

        // BDOS entry at 0x0005 is a RET: the call is serviced in C# just before it executes.
        // The word at 0x0006 is the top of the TPA, which the programs use as their stack.
        memory.Write(0x0005, 0xC9);
        memory.Write(0x0006, 0x00);
        memory.Write(0x0007, 0xF0);

        var cpu = new Z80Cpu(memory, new NullIo())
        {
            PC = 0x0100,
            SP = 0xF000,
        };

        var console = new StringBuilder();
        var line = new StringBuilder();

        // Returning to 0x0000 is the CP/M warm boot: the program has finished.
        while (cpu.PC != 0x0000)
        {
            if (cpu.PC == 0x0005)
            {
                Bdos(cpu, memory, console, line);
            }

            cpu.Step();
        }

        FlushLine(line);
        output.WriteLine($"{cpu.TStates:N0} T-states");
        return console.ToString();
    }

    private void Bdos(Z80Cpu cpu, CpmMemory memory, StringBuilder console, StringBuilder line)
    {
        switch (cpu.C)
        {
            case 2:
                Print((char)cpu.E, console, line);
                break;

            case 9:
                for (var address = (ushort)((cpu.D << 8) | cpu.E); memory.Read(address) != '$'; address++)
                {
                    Print((char)memory.Read(address), console, line);
                }

                break;
        }
    }

    private void Print(char c, StringBuilder console, StringBuilder line)
    {
        console.Append(c);
        if (c == '\n')
        {
            FlushLine(line);
        }
        else if (c != '\r')
        {
            line.Append(c);
        }
    }

    private void FlushLine(StringBuilder line)
    {
        if (line.Length > 0)
        {
            output.WriteLine(line.ToString());
            line.Clear();
        }
    }

    private sealed class CpmMemory : IMemory
    {
        private readonly byte[] _ram = new byte[0x10000];

        public void Load(ushort address, byte[] data) => data.CopyTo(_ram, address);

        public byte Read(ushort address) => _ram[address];

        public void Write(ushort address, byte value) => _ram[address] = value;
    }

    private sealed class NullIo : IIo
    {
        public byte In(ushort port) => 0xFF;

        public void Out(ushort port, byte value) { }
    }
}
