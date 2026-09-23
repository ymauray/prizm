// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// Something that changes the sound at its own pace (the tape, the AY chip), and tells the beeper
/// at which T-state. Every source is brought up to a given T-state before any sound event at that
/// T-state, so that the beeper always receives its changes in time order.
/// </summary>
public interface ISoundSource
{
    /// <summary>Runs up to <paramref name="time"/> (a T-state of the current frame), telling the beeper about each change.</summary>
    void AdvanceTo(long time, Beeper beeper);

    /// <summary>Makes the source's times relative to the next frame.</summary>
    void EndFrame(int frameTStates);
}
