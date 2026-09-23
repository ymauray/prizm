// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80.Tests;

/// <summary>64 KB of plain RAM.</summary>
internal sealed class FlatMemory : IMemory
{
    private readonly byte[] _ram = new byte[0x10000];

    public void Load(ushort address, params byte[] data) => data.CopyTo(_ram, address);

    public byte Read(ushort address) => _ram[address];

    public void Write(ushort address, byte value) => _ram[address] = value;
}

/// <summary>No device: reads return 0xFF, writes are ignored.</summary>
internal sealed class NullIo : IIo
{
    public byte In(ushort port) => 0xFF;

    public void Out(ushort port, byte value) { }
}
