// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Z80;

/// <summary>
/// Zilog Z80 CPU. Talks to the outside world only through <see cref="IMemory"/> and <see cref="IIo"/>.
/// </summary>
public sealed partial class Z80Cpu
{
    private readonly IMemory _memory;
    private readonly IIo _io;

    // Main register set.
    public byte A, B, C, D, E, H, L;

    private byte _f;

    /// <summary>Flags. The CPU notes whether the current instruction writes them (see <see cref="_q"/>).</summary>
    public byte F
    {
        get => _f;
        set
        {
            _f = value;
            _flagsWritten = true;
        }
    }

    /// <summary>Whether the instruction being executed has written F.</summary>
    private bool _flagsWritten;

    /// <summary>
    /// The internal "Q" register: F as the last instruction left it if that instruction wrote the
    /// flags, 0 otherwise. SCF and CCF take bits 3 and 5 from (Q xor F) or A on Zilog Z80s
    /// (David Banks, 2018; checked by z80test's z80ccf).
    /// </summary>
    private byte _q;

    // Alternate register set (A', F', B', C', D', E', H', L').
    public byte A_, F_, B_, C_, D_, E_, H_, L_;

    public ushort IX, IY, SP, PC;

    /// <summary>Internal MEMPTR register; leaks into undocumented flags bits 3 and 5.</summary>
    public ushort WZ;

    public byte I, R;
    public bool IFF1, IFF2;
    public int IM;
    public bool Halted;

    /// <summary>
    /// Set by EI: interrupts are not accepted until the next instruction has run, so that
    /// EI followed by RET can return before a pending interrupt is taken.
    /// </summary>
    private bool _interruptsBlocked;

    /// <summary>T-states elapsed since the counter was last reset by the machine.</summary>
    public long TStates;

    public Z80Cpu(IMemory memory, IIo io)
    {
        _memory = memory;
        _io = io;
        Reset();
    }

    /// <summary>
    /// Power-on state. AF and SP read as 0xFFFF on real hardware; the other registers
    /// are undefined and are cleared here (same choice as FUSE).
    /// </summary>
    public void Reset()
    {
        A = F = 0xFF;
        B = C = D = E = H = L = 0;
        A_ = F_ = B_ = C_ = D_ = E_ = H_ = L_ = 0;
        IX = IY = 0;
        SP = 0xFFFF;
        PC = 0;
        WZ = 0;
        I = R = 0;
        IFF1 = IFF2 = false;
        IM = 0;
        Halted = false;
        _q = 0;
        _interruptsBlocked = false;
        TStates = 0;
    }

    /// <summary>
    /// Maskable interrupt request (INT), raised by the ULA once per frame. Returns false when
    /// the CPU does not accept it (IFF1 reset, or the instruction just executed was EI); the
    /// ULA holds INT for 32 T-states, so the machine may try again.
    /// </summary>
    /// <remarks>
    /// On the Spectrum nothing drives the data bus during the acknowledge, so it reads 0xFF:
    /// IM 0 then executes RST 38h like IM 1, and IM 2 takes its vector from (I * 256 + 0xFF).
    /// Timings follow FUSE: 13 T-states in IM 0/1 and 19 in IM 2.
    /// </remarks>
    public bool Interrupt()
    {
        if (!IFF1 || _interruptsBlocked)
        {
            return false;
        }

        // HALT leaves PC on its own opcode; the return address is the next instruction.
        if (Halted)
        {
            Halted = false;
            PC++;
        }

        IFF1 = IFF2 = false;
        _q = 0;

        // The acknowledge is an M1 cycle with 2 extra wait states: R advances, 7 T-states,
        // without contention (as in FUSE).
        IncrementR();
        TStates += 7;
        Push(PC);

        if (IM == 2)
        {
            var vector = (ushort)((I << 8) | 0xFF);
            var low = ReadByte(vector);
            PC = (ushort)(low | (ReadByte((ushort)(vector + 1)) << 8));
        }
        else
        {
            PC = 0x0038;
        }

        WZ = PC;
        return true;
    }

    // Bus access. Each helper adds the T-states of its machine cycle, so an instruction's
    // total is the sum of its cycles plus its internal cycles. Every cycle that puts an address
    // on the bus first gives the machine a chance to stall the CPU (contention); the addresses
    // and the order of these points follow FUSE, whose tests check them one by one.

    /// <summary>Opcode fetch (M1): 4 T-states, increments the low 7 bits of R.</summary>
    private byte FetchOpcode()
    {
        Contend(PC);
        TStates += 4;
        var opcode = _memory.Read(PC++);
        IncrementR();
        return opcode;
    }

    /// <summary>R counts M1 cycles in its low 7 bits; bit 7 only changes through LD R,A.</summary>
    private void IncrementR() => R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

    private byte ReadByte(ushort address)
    {
        Contend(address);
        TStates += 3;
        return _memory.Read(address);
    }

    private void WriteByte(ushort address, byte value)
    {
        Contend(address);
        TStates += 3;
        _memory.Write(address, value);
    }

    private byte ReadOperand() => ReadByte(PC++);

    private ushort ReadOperandWord()
    {
        var low = ReadOperand();
        return (ushort)(low | (ReadOperand() << 8));
    }

    private byte ReadPort(ushort port)
    {
        StartIoCycle(port);
        var value = _io.In(port);
        EndIoCycle(port);
        return value;
    }

    private void WritePort(ushort port, byte value)
    {
        StartIoCycle(port);
        _io.Out(port, value);
        EndIoCycle(port);
    }

    // An I/O cycle lasts 4 T-states; the device is read or written after the first. Where the
    // machine may stall it depends on the port (ZX Spectrum pattern, as in FUSE):
    //   high byte not contended, even port (ULA): 1, then contention point + 3
    //   high byte not contended, odd port:        1, then 3
    //   high byte contended, even port:           contention point + 1, then contention point + 3
    //   high byte contended, odd port:            contention point + 1, then 3 x (contention point + 1)

    private void StartIoCycle(ushort port)
    {
        if (_memory.IsContended(port))
        {
            ContendPort(port);
        }

        TStates += 1;
    }

    private void EndIoCycle(ushort port)
    {
        if ((port & 1) == 0)
        {
            ContendPort(port);
            TStates += 3;
        }
        else if (_memory.IsContended(port))
        {
            for (var i = 0; i < 3; i++)
            {
                ContendPort(port);
                TStates += 1;
            }
        }
        else
        {
            TStates += 3;
        }
    }

    private void Contend(ushort address) => TStates += _memory.ContentionDelay(address, TStates);

    private void ContendPort(ushort port) => TStates += _io.ContentionDelay(port, TStates);

    /// <summary>
    /// Internal cycles, 1 T-state each, during which the Z80 keeps <paramref name="address"/> on
    /// the bus, so each can be stalled like a memory access.
    /// </summary>
    private void Internal(ushort address, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Contend(address);
            TStates += 1;
        }
    }

    /// <summary>I and R, which the Z80 puts on the address bus during most internal cycles.</summary>
    private ushort IR => (ushort)((I << 8) | R);

    private void Push(ushort value)
    {
        WriteByte(--SP, (byte)(value >> 8));
        WriteByte(--SP, (byte)value);
    }

    private ushort Pop()
    {
        var low = ReadByte(SP++);
        return (ushort)(low | (ReadByte(SP++) << 8));
    }

    // Register pairs.

    private const int IndexHL = 0;
    private const int IndexIX = 1;
    private const int IndexIY = 2;

    /// <summary>Which of HL, IX or IY the current instruction uses, as selected by a DD or FD prefix.</summary>
    private int _index;

    /// <summary>HL, or IX / IY when the current instruction has a DD / FD prefix.</summary>
    private ushort IndexRegister
    {
        get => _index switch
        {
            IndexHL => HL,
            IndexIX => IX,
            _ => IY,
        };
        set
        {
            switch (_index)
            {
                case IndexHL: HL = value; break;
                case IndexIX: IX = value; break;
                default: IY = value; break;
            }
        }
    }

    private ushort BC { get => (ushort)((B << 8) | C); set { B = (byte)(value >> 8); C = (byte)value; } }

    private ushort DE { get => (ushort)((D << 8) | E); set { D = (byte)(value >> 8); E = (byte)value; } }

    private ushort HL { get => (ushort)((H << 8) | L); set { H = (byte)(value >> 8); L = (byte)value; } }

    /// <summary>AF, for PUSH and POP. POP AF loads the flags without computing them: Q stays 0.</summary>
    private ushort AF { get => (ushort)((A << 8) | F); set { A = (byte)(value >> 8); _f = (byte)value; } }
}
