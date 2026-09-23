// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Z80;

/// <summary>I/O port space seen by the CPU (IN / OUT instructions).</summary>
public interface IIo
{
    byte In(ushort port);

    void Out(ushort port, byte value);
}
