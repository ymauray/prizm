namespace iSpectrum.Z80;

/// <summary>
/// Zilog Z80 CPU. Talks to the outside world only through <see cref="IMemory"/> and <see cref="IIo"/>.
/// </summary>
public sealed class Z80Cpu
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

    /// <summary>Fetches, decodes and executes one instruction, adding its exact T-states.</summary>
    public void Step() => throw new NotImplementedException();

    /// <summary>Maskable interrupt request (INT), raised by the ULA once per frame.</summary>
    public void Interrupt() => throw new NotImplementedException();
}
