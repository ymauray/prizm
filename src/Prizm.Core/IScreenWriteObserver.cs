// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core;

/// <summary>Something that must act before the screen memory changes: the ULA drawing the picture.</summary>
internal interface IScreenWriteObserver
{
    /// <summary><paramref name="screen"/> is the displayed bank, as <see cref="SpectrumMemory.Screen"/>.</summary>
    void BeforeScreenWrite(ReadOnlySpan<byte> screen);
}
