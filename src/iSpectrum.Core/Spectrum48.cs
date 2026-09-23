// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;
using iSpectrum.Z80;

namespace iSpectrum.Core;

/// <summary>ZX Spectrum 48K: Z80, memory and ULA, run one 50 Hz frame at a time.</summary>
public sealed class Spectrum48
{


    /// <summary>LD-BYTES, the ROM routine that loads a block from tape.</summary>
    private const ushort LdBytes = 0x0556;

    /// <summary>
    /// LD-BREAK, inside LD-BYTES once it has saved its arguments: the expected flag byte in A',
    /// LOAD (carry set) or VERIFY in F', the destination in IX and the length in DE.
    /// </summary>
    private const ushort LdBytesReady = 0x056B;

    /// <summary>The end of LD-BYTES: LD A,H / CP 1 / RET. H = 0 (checksum right) sets carry: success.</summary>
    private const ushort LdBytesExit = 0x05DF;

    /// <summary>Whether the interrupt of the current frame has been taken.</summary>
    private bool _interruptTaken;

    /// <summary>Set between frames: the next <see cref="Step"/> starts a new one.</summary>
    private bool _frameStarting = true;

    /// <summary>The 48K's clock, frame and contention timings.</summary>
    public SpectrumTimings Timings { get; } = SpectrumTimings.Spectrum48;

    public Spectrum48(ReadOnlySpan<byte> rom)
    {
        Memory = new Memory48K(rom);
        Ula = new Ula(Timings);
        Cpu = new Z80Cpu(Memory, Ula);
        Ula.Connect(Cpu, Memory);
        Memory.ScreenObserver = Ula;
    }

    public Z80Cpu Cpu { get; }

    public Memory48K Memory { get; }

    public Ula Ula { get; }

    /// <summary>Keys held down; the front-end updates it between frames.</summary>
    public Keyboard Keyboard => Ula.Keyboard;

    /// <summary>The tape deck. It starts playing when the ROM starts loading.</summary>
    public TapePlayer Tape => Ula.Tape;

    /// <summary>
    /// Runs without drawing the picture or making sound, for tests and fast-forwarding; the
    /// machine itself behaves exactly the same.
    /// </summary>
    public bool Headless
    {
        get => Ula.Headless;
        set
        {
            Ula.Headless = value;
            Ula.Beeper.Muted = value;
        }
    }

    /// <summary>Types key strokes on this machine, one frame at a time.</summary>
    public AutoTyper AutoTyper { get; } = new();

    /// <summary>
    /// Frames to wait after power-on before typing: the ROM needs about 85 to test the RAM and
    /// show its copyright message.
    /// </summary>
    public const int BootFrames = 100;

    /// <summary>
    /// When set, LD-BYTES is intercepted and each block is copied straight into memory instead of
    /// being played; when clear, the ROM reads the tape signal in real time.
    /// </summary>
    public bool FastLoad { get; set; }

    /// <summary>The picture of the last completed frame (see <see cref="Ula.FrameBuffer"/>).</summary>
    public ReadOnlySpan<uint> FrameBuffer => Ula.FrameBuffer;

    /// <summary>The sound of the last completed frame (see <see cref="Beeper"/>).</summary>
    public ReadOnlySpan<short> AudioSamples => Ula.Beeper.Samples;

    /// <summary>
    /// Runs one frame. The interrupt is requested while INT is held, so a CPU that has just run
    /// EI still takes it once the next instruction is done; the last instruction may overrun
    /// the frame, and the overrun is carried into the next one.
    /// </summary>
    public void RunFrame()
    {
        do
        {
            Step();
        }
        while (!_frameStarting);
    }

    /// <summary>
    /// Runs one instruction (after taking the interrupt if it is due), and ends the frame when
    /// its last T-state has passed. For debuggers and tests that stop in the middle of a frame.
    /// </summary>
    public void Step()
    {
        if (_frameStarting)
        {
            _frameStarting = false;
            Ula.Beeper.StartFrame();
            AutoTyper.NextFrame(Keyboard);
        }

        if (!_interruptTaken && Cpu.TStates < Timings.InterruptLength)
        {
            _interruptTaken = Cpu.Interrupt();
        }

        if (FastLoad)
        {
            if (Cpu.PC == LdBytesReady && !Tape.AtEnd)
            {
                LoadBlockAtOnce();
            }
        }
        else if (Cpu.PC == LdBytes && !Tape.IsPlaying)
        {
            // Press "play" when the ROM starts listening to the tape.
            Tape.Play(Cpu.TStates);
        }

        Cpu.Step();

        if (Cpu.TStates >= Timings.FrameTStates)
        {
            Ula.EndFrameSound(Cpu.TStates, Timings.FrameTStates);
            Cpu.TStates -= Timings.FrameTStates;
            Ula.EndFrame(Memory.Contents);
            _interruptTaken = false;
            _frameStarting = true;
        }
    }

    /// <summary>
    /// Does the work of LD-BYTES on the next tape block, then jumps to the routine's end, which
    /// turns H into the result: H is the XOR of every byte read, checksum included, so 0 when the
    /// block is intact. A block with the wrong flag is used up and fails, as on a real tape: the
    /// ROM then tries the next one. Technique from FUSE's tape traps.
    /// </summary>
    private void LoadBlockAtOnce()
    {
        var block = Tape.TakeBlock()!;
        var load = (Cpu.F_ & 0x01) != 0;
        Cpu.H = block.Length > 0 && block[0] == Cpu.A_ ? CopyBlock(block, load) : (byte)0xFF;
        Cpu.PC = LdBytesExit;
    }

    /// <summary>
    /// Loads (or verifies) DE bytes at IX, as LD-BYTES does, moving IX and DE along. Returns the
    /// XOR of the flag, the data and the checksum byte, or 0xFF if the block is too short or a
    /// verified byte differs.
    /// </summary>
    private byte CopyBlock(byte[] block, bool load)
    {
        var parity = block[0];
        var index = 1;

        while (Cpu.D != 0 || Cpu.E != 0)
        {
            if (index >= block.Length)
            {
                return 0xFF;
            }

            var value = block[index++];
            if (load)
            {
                Memory.Write(Cpu.IX, value);
            }
            else if (Memory.Read(Cpu.IX) != value)
            {
                return 0xFF;
            }

            parity ^= value;
            Cpu.IX++;
            var length = (ushort)(((Cpu.D << 8) | Cpu.E) - 1);
            (Cpu.D, Cpu.E) = ((byte)(length >> 8), (byte)length);
        }

        // The byte after the data is the checksum.
        return index < block.Length ? (byte)(parity ^ block[index]) : (byte)0xFF;
    }
}
