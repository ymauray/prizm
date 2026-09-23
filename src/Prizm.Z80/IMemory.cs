// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Z80;

/// <summary>64 KB address space seen by the CPU.</summary>
public interface IMemory
{
    byte Read(ushort address);

    void Write(ushort address, byte value);

    /// <summary>
    /// T-states the machine makes the CPU wait before a cycle that puts <paramref name="address"/>
    /// on the bus at <paramref name="tStates"/>: memory reads and writes, opcode fetches, and the
    /// internal cycles during which the Z80 keeps an address on the bus. The ZX Spectrum's ULA
    /// uses this to stall the CPU while it reads the screen ("contention"). 0 by default.
    /// </summary>
    int ContentionDelay(ushort address, long tStates) => 0;

    /// <summary>
    /// Whether <paramref name="address"/> is in contended memory. It also decides the contention
    /// pattern of an I/O cycle, through the high byte of the port address. False by default.
    /// </summary>
    bool IsContended(ushort address) => false;
}
