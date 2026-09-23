// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>Told about every memory write and port access the CPU makes (the debugger's watchpoints).</summary>
internal interface IBusWatch
{
    void OnMemoryWrite(ushort address, byte value);

    void OnPortRead(ushort port, byte value);

    void OnPortWrite(ushort port, byte value);
}
