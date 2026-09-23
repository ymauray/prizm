namespace iSpectrum.Z80;

/// <summary>
/// Zilog Z80 CPU. Talks to the outside world only through <see cref="IMemory"/> and <see cref="IIo"/>.
/// </summary>
public sealed partial class Z80Cpu
{
    private readonly IMemory _memory;
    private readonly IIo _io;

    // Main register set.
    public byte A, F, B, C, D, E, H, L;

    // Alternate register set (A', F', B', C', D', E', H', L').
    public byte A_, F_, B_, C_, D_, E_, H_, L_;

    public ushort IX, IY, SP, PC;

    /// <summary>Internal MEMPTR register; leaks into undocumented flags bits 3 and 5.</summary>
    public ushort WZ;

    public byte I, R;
    public bool IFF1, IFF2;
    public int IM;
    public bool Halted;

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
        TStates = 0;
    }

    /// <summary>Maskable interrupt request (INT), raised by the ULA once per frame.</summary>
    public void Interrupt() => throw new NotImplementedException();

    // Bus access. Each helper adds the T-states of its machine cycle, so an instruction's
    // total is the sum of its cycles plus the explicit internal cycles (Internal).

    /// <summary>Opcode fetch (M1): 4 T-states, increments the low 7 bits of R.</summary>
    private byte FetchOpcode()
    {
        var opcode = _memory.Read(PC++);
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));
        TStates += 4;
        return opcode;
    }

    private byte ReadByte(ushort address)
    {
        TStates += 3;
        return _memory.Read(address);
    }

    private void WriteByte(ushort address, byte value)
    {
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
        TStates += 4;
        return _io.In(port);
    }

    private void WritePort(ushort port, byte value)
    {
        TStates += 4;
        _io.Out(port, value);
    }

    /// <summary>Internal cycles where the bus is idle.</summary>
    private void Internal(int tStates) => TStates += tStates;

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

    private ushort AF { get => (ushort)((A << 8) | F); set { A = (byte)(value >> 8); F = (byte)value; } }
}
