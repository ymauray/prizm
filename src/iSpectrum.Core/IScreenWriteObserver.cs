// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>Something that must act before the screen memory changes: the ULA drawing the picture.</summary>
internal interface IScreenWriteObserver
{
    void BeforeScreenWrite(ReadOnlySpan<byte> memory);
}
