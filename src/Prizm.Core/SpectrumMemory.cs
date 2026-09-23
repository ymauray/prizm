// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Z80;

namespace Prizm.Core;

/// <summary>
/// The memory of a Spectrum model, as the CPU and the ULA see it: the CPU through the 64 KB
/// address space, the ULA through the 16 KB bank it displays.
/// </summary>
public abstract class SpectrumMemory : IMemory
{
    /// <summary>Size of a ROM or RAM bank, and of each quarter of the address space.</summary>
    public const int BankSize = 0x4000;

    /// <summary>Bytes of the screen at the start of the displayed bank: 6144 of pixels, 768 of attributes.</summary>
    public const int ScreenSize = 0x1B00;

    /// <summary>
    /// The 16 KB bank the ULA displays; the screen is at its start, so a screen address
    /// (0x4000-0x5AFF) is found at that address minus 0x4000.
    /// </summary>
    public abstract ReadOnlySpan<byte> Screen { get; }

    /// <summary>Told before each write to the displayed screen, so the picture can catch up first.</summary>
    internal IScreenWriteObserver? ScreenObserver { get; set; }

    /// <summary>Told about every write the CPU makes, including to ROM (debugger), when set.</summary>
    internal IBusWatch? Watch { get; set; }

    public abstract byte Read(ushort address);

    public abstract void Write(ushort address, byte value);

    public abstract int ContentionDelay(ushort address, long tStates);

    public abstract bool IsContended(ushort address);

    /// <summary>Lets the picture catch up before the displayed screen changes.</summary>
    protected void BeforeScreenChange() => ScreenObserver?.BeforeScreenWrite(Screen);
}
