// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Z80;

namespace Prizm.Z80.Tests;

public class Z80CpuTests
{
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
                $"Prizm.Z80 must not reference {reference.Name}");
        }
    }
}
