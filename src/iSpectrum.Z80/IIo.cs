// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80;

/// <summary>I/O port space seen by the CPU (IN / OUT instructions).</summary>
public interface IIo
{
    byte In(ushort port);

    void Out(ushort port, byte value);

    /// <summary>
    /// T-states the machine makes the CPU wait at one of the contention points of an I/O cycle
    /// on <paramref name="port"/>, at <paramref name="tStates"/>. The CPU decides where those
    /// points are (see Z80Cpu); the machine only says how long each one lasts. 0 by default.
    /// </summary>
    int ContentionDelay(ushort port, long tStates) => 0;
}
