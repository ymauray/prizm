// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The General Instrument AY-3-8912 sound chip of the 128K: three square-wave tone channels, a
/// noise generator and an envelope generator, clocked at half the CPU clock. Port 0xFFFD selects a
/// register (and reads it back), port 0xBFFD writes it.
/// </summary>
/// <remarks>
/// The chip runs in ticks of 8 of its clocks (16 T-states). Each tick the tone counters advance;
/// the noise and envelope counters advance every other tick. That gives the documented rates:
/// clock / (16 x period) for a tone, clock / (256 x period) for a whole envelope cycle. Output
/// changes are sent to the beeper, stamped with their T-state, and mixed there.
/// </remarks>
public sealed class Ay8912 : ISoundSource
{
    /// <summary>T-states per tick: 8 AY clocks, the AY running at half the CPU clock.</summary>
    private const int TicksTStates = 16;

    /// <summary>Bits that exist in each register; the others read back as 0.</summary>
    private static readonly byte[] RegisterMasks =
    [
        0xFF, 0x0F, 0xFF, 0x0F, 0xFF, 0x0F, 0x1F, 0xFF,
        0x1F, 0x1F, 0x1F, 0xFF, 0xFF, 0x0F, 0xFF, 0xFF,
    ];

    /// <summary>
    /// Output of a channel for each of the 16 volume levels, from the measured table of ayumi
    /// (Peter Sovietov, MIT licence): roughly logarithmic, 0 at level 0 and 1 at level 15.
    /// </summary>
    private static readonly double[] Levels =
    [
        0.0, 0.00999465934234, 0.0144502937362, 0.0210574502174,
        0.0307011520562, 0.0455481803616, 0.0644998855573, 0.107362478065,
        0.126588845655, 0.20498970016, 0.292210269322, 0.372838941024,
        0.492530708782, 0.635324635691, 0.805584802014, 1.0,
    ];

    private readonly byte[] _registers = new byte[16];
    private readonly int[] _toneCounters = new int[3];
    private readonly bool[] _toneHigh = new bool[3];

    private int _noiseCounter;

    /// <summary>17-bit noise shift register; bit 0 is the noise output.</summary>
    private int _noiseShift = 1;

    private int _envelopeCounter;
    private int _envelopeStep;
    private bool _envelopeRising;
    private bool _envelopeHolding;

    private bool _oddTick;
    private double _output;

    /// <summary>T-state (from the start of the current frame) of the next tick.</summary>
    private long _nextTick;

    /// <summary>The register that port 0xFFFD selected.</summary>
    public int SelectedRegister { get; private set; }

    /// <summary>Port 0xFFFD, written: selects a register (values above 15 select nothing).</summary>
    public void SelectRegister(byte value) => SelectedRegister = value;

    /// <summary>Port 0xFFFD, read: the selected register, with only its existing bits.</summary>
    public byte ReadSelected() => SelectedRegister < 16 ? _registers[SelectedRegister] : (byte)0xFF;

    /// <summary>Port 0xBFFD, written: sets the selected register.</summary>
    public void WriteSelected(byte value)
    {
        if (SelectedRegister >= 16)
        {
            return;
        }

        _registers[SelectedRegister] = (byte)(value & RegisterMasks[SelectedRegister]);

        // Writing the envelope shape restarts the envelope.
        if (SelectedRegister == 13)
        {
            _envelopeStep = 0;
            _envelopeCounter = 0;
            _envelopeHolding = false;
            _envelopeRising = (value & 0x04) != 0;
        }
    }

    /// <summary>The chip's output after the last tick, 0 to 1 (for tests).</summary>
    internal double CurrentOutput => _output;

    /// <summary>A register's value, for snapshots and tests.</summary>
    public byte Register(int index) => _registers[index];

    public void AdvanceTo(long time, Beeper beeper)
    {
        while (_nextTick <= time)
        {
            Tick();
            var output = Output();
            if (output != _output)
            {
                _output = output;
                beeper.SetChipLevel(_nextTick, output);
            }

            _nextTick += TicksTStates;
        }
    }

    public void EndFrame(int frameTStates) => _nextTick -= frameTStates;

    private void Tick()
    {
        for (var channel = 0; channel < 3; channel++)
        {
            // A period of 0 behaves as 1.
            var period = Math.Max(1, _registers[channel * 2] | (_registers[(channel * 2) + 1] << 8));
            if (++_toneCounters[channel] >= period)
            {
                _toneCounters[channel] = 0;
                _toneHigh[channel] = !_toneHigh[channel];
            }
        }

        _oddTick = !_oddTick;
        if (!_oddTick)
        {
            return;
        }

        if (++_noiseCounter >= Math.Max(1, _registers[6] & 0x1F))
        {
            _noiseCounter = 0;

            // 17-bit LFSR: the new bit is bit 0 xor bit 3.
            var bit = (_noiseShift ^ (_noiseShift >> 3)) & 1;
            _noiseShift = (_noiseShift >> 1) | (bit << 16);
        }

        if (++_envelopeCounter >= Math.Max(1, _registers[11] | (_registers[12] << 8)))
        {
            _envelopeCounter = 0;
            StepEnvelope();
        }
    }

    /// <summary>
    /// Moves the envelope one step of its 16. At the end of a ramp, shape bits decide what follows:
    /// CONTINUE (bit 3) clear drops to 0 and holds; HOLD (bit 0) holds at the end level, flipped by
    /// ALTERNATE (bit 1); otherwise the ramp restarts, reversed if ALTERNATE is set.
    /// </summary>
    private void StepEnvelope()
    {
        if (_envelopeHolding || ++_envelopeStep < 16)
        {
            return;
        }

        var shape = _registers[13];
        var attack = (shape & 0x04) != 0;
        var alternate = (shape & 0x02) != 0;

        if ((shape & 0x08) == 0)
        {
            _envelopeHolding = true;
            _envelopeRising = false;
            _envelopeStep = 15; // held at level 0
        }
        else if ((shape & 0x01) != 0)
        {
            _envelopeHolding = true;
            _envelopeRising = attack ^ alternate;
            _envelopeStep = 15; // held at the end level
        }
        else
        {
            _envelopeStep = 0;
            if (alternate)
            {
                _envelopeRising = !_envelopeRising;
            }
        }
    }

    private int EnvelopeLevel => _envelopeRising ? _envelopeStep : 15 - _envelopeStep;

    /// <summary>
    /// Sum of the three channels, scaled to 0-1. A channel sounds high when every source enabled
    /// for it in the mixer (register 7, bits clear = enabled) is high; a disabled source counts as high.
    /// </summary>
    private double Output()
    {
        var mixer = _registers[7];
        var noiseHigh = (_noiseShift & 1) != 0;
        var sum = 0.0;

        for (var channel = 0; channel < 3; channel++)
        {
            var toneOn = (mixer & (1 << channel)) == 0;
            var noiseOn = (mixer & (8 << channel)) == 0;
            var high = (!toneOn || _toneHigh[channel]) && (!noiseOn || noiseHigh);
            if (!high)
            {
                continue;
            }

            var amplitude = _registers[8 + channel];
            var level = (amplitude & 0x10) != 0 ? EnvelopeLevel : amplitude & 0x0F;
            sum += Levels[level];
        }

        return sum / 3;
    }
}
