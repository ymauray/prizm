namespace iSpectrum.Z80;

/// <summary>64 KB address space seen by the CPU.</summary>
public interface IMemory
{
    byte Read(ushort address);

    void Write(ushort address, byte value);
}
