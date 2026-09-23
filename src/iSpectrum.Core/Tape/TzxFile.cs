// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.IO.Compression;

namespace iSpectrum.Core.Tape;

/// <summary>
/// Reads a .TZX tape image: a signature, then blocks, each starting with its ID. Besides the
/// ROM's blocks, TZX describes turbo loaders (other timings), raw pulses, sampled sound, and
/// control blocks (loops, jumps, pauses that stop the tape).
/// </summary>
/// <remarks>
/// Format: the TZX specification, version 1.20 (World of Spectrum). Durations are in T-states
/// of a 3.5 MHz clock; like FUSE, they are played unchanged on the 128K.
/// </remarks>
public static class TzxFile
{
    /// <summary>T-states per millisecond, for pauses.</summary>
    public const int TStatesPerMillisecond = 3500;

    /// <summary>Beyond this many edges in one block, the file is taken as corrupt rather than expanded.</summary>
    private const int MaxEdgesPerBlock = 50_000_000;

    private static ReadOnlySpan<byte> Signature => "ZXTape!\u001A"u8;

    public static TapeImage Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10 || !data[..8].SequenceEqual(Signature))
        {
            throw new InvalidDataException("Not a TZX file: the \"ZXTape!\" signature is missing.");
        }

        if (data[8] != 1)
        {
            throw new InvalidDataException($"TZX version {data[8]}.{data[9]:D2} is not supported.");
        }

        var blocks = new List<TapeBlock>();
        var reader = new Reader(data[10..]);
        while (!reader.AtEnd)
        {
            var id = reader.Byte();
            try
            {
                blocks.Add(ReadBlock(id, ref reader));
            }
            catch (InvalidDataException e)
            {
                throw new InvalidDataException($"Block {blocks.Count + 1} (ID 0x{id:X2}): {e.Message}", e);
            }
        }

        return new TapeImage(blocks);
    }

    private static TapeBlock ReadBlock(byte id, ref Reader reader)
    {
        switch (id)
        {
            case 0x10:
            {
                var pause = reader.Word() * TStatesPerMillisecond;
                return TapeBlock.Standard(reader.Bytes(reader.Word()).ToArray(), pause);
            }

            case 0x11:
                return Turbo(ref reader);

            case 0x12:
            {
                var length = reader.Word();
                return Sound(id, [new ToneSegment(length, reader.Word())]);
            }

            case 0x13:
            {
                var edges = new TapeEdge[reader.Byte()];
                for (var i = 0; i < edges.Length; i++)
                {
                    edges[i] = new TapeEdge(reader.Word(), TapeTransition.Toggle);
                }

                return Sound(id, [new EdgeListSegment(edges)]);
            }

            case 0x14:
            {
                var zero = reader.Word();
                var one = reader.Word();
                var usedBits = reader.Byte();
                var pause = reader.Word() * TStatesPerMillisecond;
                var bytes = reader.Bytes(reader.Three()).ToArray();
                return Sound(id, [new DataSegment(bytes, BitCount(bytes.Length, usedBits), zero, one)], pause);
            }

            case 0x15:
                return Raw(ref reader);

            case 0x18:
            {
                var body = reader.Sub(reader.DWord());
                return Csw(ref body);
            }

            case 0x19:
            {
                var body = reader.Sub(reader.DWord());
                return Generalized(ref body);
            }

            case 0x20:
                return new TapeBlock { Kind = TapeBlockKind.Pause, Id = id, PauseTStates = reader.Word() * TStatesPerMillisecond };

            case 0x21: // Group start: a name.
            case 0x30: // Text description.
                reader.Bytes(reader.Byte());
                return Info(id);

            case 0x22: // Group end.
                return Info(id);

            case 0x23:
                return new TapeBlock { Kind = TapeBlockKind.Jump, Id = id, Value = (short)reader.Word() };

            case 0x24:
                return new TapeBlock { Kind = TapeBlockKind.LoopStart, Id = id, Value = reader.Word() };

            case 0x25:
                return new TapeBlock { Kind = TapeBlockKind.LoopEnd, Id = id };

            case 0x26:
            {
                var offsets = new int[reader.Word()];
                for (var i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = (short)reader.Word();
                }

                return new TapeBlock { Kind = TapeBlockKind.CallSequence, Id = id, Offsets = offsets };
            }

            case 0x27:
                return new TapeBlock { Kind = TapeBlockKind.Return, Id = id };

            case 0x28: // Select block: a menu for the user of the tape, not played.
                reader.Bytes(reader.Word());
                return Info(id);

            case 0x2A:
                reader.Bytes(reader.DWord());
                return new TapeBlock { Kind = TapeBlockKind.Stop48, Id = id };

            case 0x2B:
            {
                var body = reader.Sub(reader.DWord());
                return new TapeBlock { Kind = TapeBlockKind.SetLevel, Id = id, Value = body.Byte() & 1 };
            }

            case 0x31: // Message: a display time, then the text.
                reader.Byte();
                reader.Bytes(reader.Byte());
                return Info(id);

            case 0x32:
            {
                // Archive info: strings, each with an ID and a length. The block's own length
                // is ignored, as libspectrum does: some tools write it without the count byte.
                reader.Word();
                var count = reader.Byte();
                for (var i = 0; i < count; i++)
                {
                    reader.Byte();
                    reader.Bytes(reader.Byte());
                }

                return Info(id);
            }

            case 0x33: // Hardware type: three bytes per entry.
                reader.Bytes(reader.Byte() * 3);
                return Info(id);

            case 0x34: // Emulation info (deprecated).
                reader.Bytes(8);
                return Info(id);

            case 0x35: // Custom info: a 16-character name, then the data.
                reader.Bytes(16);
                reader.Bytes(reader.DWord());
                return Info(id);

            case 0x40: // Snapshot (deprecated): a type, then the data.
                reader.Byte();
                reader.Bytes(reader.Three());
                return Info(id);

            case 0x5A: // "Glue": the header of another TZX file, left by joining two files.
                reader.Bytes(9);
                return Info(id);

            default:
                // Blocks added after version 1.10 start with their length, so they can be
                // skipped. So do the deprecated C64 blocks 0x16 and 0x17.
                reader.Bytes(reader.DWord());
                return Info(id);
        }
    }

    private static TapeBlock Info(byte id) => new() { Kind = TapeBlockKind.Info, Id = id };

    private static TapeBlock Sound(byte id, PulseSegment[] segments) =>
        new() { Kind = TapeBlockKind.Sound, Id = id, Segments = segments };

    private static TapeBlock Sound(byte id, PulseSegment[] segments, int pauseTStates) =>
        new() { Kind = TapeBlockKind.Sound, Id = id, Segments = segments, HasPause = true, PauseTStates = pauseTStates };

    /// <summary>Bits in a data block whose last byte uses only its top <paramref name="usedBits"/> bits.</summary>
    private static int BitCount(int length, int usedBits) =>
        length == 0 ? 0 : ((length - 1) * 8) + Math.Clamp(usedBits, 0, 8);

    /// <summary>Block 0x11: a data block with its own timings, as turbo loaders read.</summary>
    private static TapeBlock Turbo(ref Reader reader)
    {
        var pilot = reader.Word();
        var sync1 = reader.Word();
        var sync2 = reader.Word();
        var zero = reader.Word();
        var one = reader.Word();
        var pilotPulses = reader.Word();
        var usedBits = reader.Byte();
        var pause = reader.Word() * TStatesPerMillisecond;
        var bytes = reader.Bytes(reader.Three()).ToArray();

        // Data sent at the ROM's speed can be fast-loaded whatever its pilot tone.
        var romSpeed = zero == TapePlayer.ZeroPulse && one == TapePlayer.OnePulse && usedBits == 8;

        return new TapeBlock
        {
            Kind = TapeBlockKind.Sound,
            Id = 0x11,
            HasPause = true,
            PauseTStates = pause,
            RomData = romSpeed ? bytes : null,
            Segments =
            [
                new ToneSegment(pilot, pilotPulses),
                new EdgeListSegment([new(sync1, TapeTransition.Toggle), new(sync2, TapeTransition.Toggle)]),
                new DataSegment(bytes, BitCount(bytes.Length, usedBits), zero, one),
            ],
        };
    }

    /// <summary>
    /// Block 0x15, direct recording: one bit per sample (1 high, 0 low), most significant bit
    /// first. Each run of equal samples becomes one pulse at that level.
    /// </summary>
    private static TapeBlock Raw(ref Reader reader)
    {
        var sampleLength = reader.Word();
        var pause = reader.Word() * TStatesPerMillisecond;
        var usedBits = reader.Byte();
        var bytes = reader.Bytes(reader.Three());
        var bits = BitCount(bytes.Length, usedBits);

        var edges = new List<TapeEdge>();
        var run = 0;
        for (var i = 0; i < bits; i++)
        {
            var high = (bytes[i >> 3] & (0x80 >> (i & 7))) != 0;
            run++;

            var last = i == bits - 1;
            if (last || high != ((bytes[(i + 1) >> 3] & (0x80 >> ((i + 1) & 7))) != 0))
            {
                edges.Add(new TapeEdge(run * sampleLength, high ? TapeTransition.High : TapeTransition.Low));
                run = 0;
            }
        }

        return Sound(0x15, [new EdgeListSegment([.. edges])], pause);
    }

    /// <summary>
    /// Block 0x18: a CSW recording, the length of each pulse in samples, run-length encoded
    /// (a 0 is followed by a 4-byte count), possibly compressed with zlib (Z-RLE).
    /// </summary>
    private static TapeBlock Csw(ref Reader reader)
    {
        var pause = reader.Word() * TStatesPerMillisecond;
        var sampleRate = reader.Three();
        var compression = reader.Byte();
        reader.DWord(); // Number of pulses, known once decoded.
        var packed = reader.Bytes(reader.Remaining);

        if (sampleRate == 0)
        {
            throw new InvalidDataException("The CSW sample rate is 0.");
        }

        var rle = compression switch
        {
            1 => packed.ToArray(),
            2 => Inflate(packed),
            _ => throw new InvalidDataException($"Unknown CSW compression {compression}."),
        };

        var edges = new List<TapeEdge>();
        var samples = new Reader(rle);
        while (!samples.AtEnd)
        {
            long count = samples.Byte();
            if (count == 0)
            {
                count = samples.DWord();
            }

            var tStates = count * TapePlayer.ClockHz / sampleRate;
            edges.Add(new TapeEdge((int)Math.Min(tStates, int.MaxValue), TapeTransition.Toggle));
            CheckSize(edges.Count);
        }

        return Sound(0x18, [new EdgeListSegment([.. edges])], pause);
    }

    private static byte[] Inflate(ReadOnlySpan<byte> packed)
    {
        try
        {
            using var input = new MemoryStream(packed.ToArray());
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException($"The CSW data cannot be decompressed: {e.Message}", e);
        }
    }

    /// <summary>
    /// Block 0x19, generalized data: an alphabet of symbols (each a few pulses, and what the
    /// level does before the first), then a pilot made of runs of symbols, and data where each
    /// symbol takes as many bits as the alphabet needs. Decoded into edges when read.
    /// </summary>
    private static TapeBlock Generalized(ref Reader reader)
    {
        var pause = reader.Word() * TStatesPerMillisecond;
        var pilotRuns = reader.DWord();
        var pilotPulsesPerSymbol = reader.Byte();
        var pilotAlphabet = AlphabetSize(reader.Byte());
        var dataSymbols = reader.DWord();
        var dataPulsesPerSymbol = reader.Byte();
        var dataAlphabet = AlphabetSize(reader.Byte());

        var edges = new List<TapeEdge>();

        if (pilotRuns > 0)
        {
            var symbols = ReadSymbols(ref reader, pilotAlphabet, pilotPulsesPerSymbol);
            for (long i = 0; i < pilotRuns; i++)
            {
                var symbol = reader.Byte();
                var repeats = reader.Word();
                if (symbol >= symbols.Length)
                {
                    throw new InvalidDataException($"The pilot uses symbol {symbol}, beyond the {symbols.Length} defined.");
                }

                for (var r = 0; r < repeats; r++)
                {
                    AddSymbol(edges, symbols[symbol]);
                }
            }
        }

        if (dataSymbols > 0)
        {
            var symbols = ReadSymbols(ref reader, dataAlphabet, dataPulsesPerSymbol);
            var bitsPerSymbol = 0;
            while ((1 << bitsPerSymbol) < dataAlphabet)
            {
                bitsPerSymbol++;
            }

            var streamLength = ((dataSymbols * bitsPerSymbol) + 7) / 8;
            if (streamLength > reader.Remaining)
            {
                throw new InvalidDataException($"The data needs {streamLength} bytes, but the block holds {reader.Remaining}.");
            }

            var stream = reader.Bytes((int)streamLength);
            long bit = 0;
            for (long i = 0; i < dataSymbols; i++)
            {
                var symbol = 0;
                for (var b = 0; b < bitsPerSymbol; b++, bit++)
                {
                    symbol = (symbol << 1) | ((stream[(int)(bit >> 3)] >> (7 - (int)(bit & 7))) & 1);
                }

                if (symbol >= symbols.Length)
                {
                    throw new InvalidDataException($"The data uses symbol {symbol}, beyond the {symbols.Length} defined.");
                }

                AddSymbol(edges, symbols[symbol]);
            }
        }

        return Sound(0x19, [new EdgeListSegment([.. edges])], pause);
    }

    /// <summary>An alphabet size of 0 stands for 256.</summary>
    private static int AlphabetSize(byte size) => size == 0 ? 256 : size;

    /// <summary>A symbol: what the level does before its first pulse, then its pulse lengths.</summary>
    private readonly record struct Symbol(TapeTransition First, int[] Pulses);

    private static Symbol[] ReadSymbols(ref Reader reader, int count, int pulsesPerSymbol)
    {
        var symbols = new Symbol[count];
        for (var i = 0; i < count; i++)
        {
            // Bits 0-1: 0 flips the level, 1 keeps it, 2 forces it low, 3 forces it high.
            var first = (reader.Byte() & 3) switch
            {
                0 => TapeTransition.Toggle,
                1 => TapeTransition.None,
                2 => TapeTransition.Low,
                _ => TapeTransition.High,
            };

            // A pulse of 0 ends the symbol early.
            var pulses = new List<int>();
            var ended = false;
            for (var j = 0; j < pulsesPerSymbol; j++)
            {
                var length = reader.Word();
                ended |= length == 0;
                if (!ended)
                {
                    pulses.Add(length);
                }
            }

            symbols[i] = new Symbol(first, [.. pulses]);
        }

        return symbols;
    }

    private static void AddSymbol(List<TapeEdge> edges, Symbol symbol)
    {
        for (var j = 0; j < symbol.Pulses.Length; j++)
        {
            edges.Add(new TapeEdge(symbol.Pulses[j], j == 0 ? symbol.First : TapeTransition.Toggle));
        }

        CheckSize(edges.Count);
    }

    private static void CheckSize(int edges)
    {
        if (edges > MaxEdgesPerBlock)
        {
            throw new InvalidDataException($"The block holds more than {MaxEdgesPerBlock} pulses.");
        }
    }

    /// <summary>Reads little-endian values, failing on a truncated block.</summary>
    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _position;

        public readonly bool AtEnd => _position >= _data.Length;

        public readonly int Remaining => _data.Length - _position;

        public byte Byte() => Bytes(1)[0];

        public int Word()
        {
            var bytes = Bytes(2);
            return bytes[0] | (bytes[1] << 8);
        }

        public int Three()
        {
            var bytes = Bytes(3);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        public int DWord()
        {
            var bytes = Bytes(4);
            var value = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            return value > int.MaxValue ? throw Truncated() : (int)value;
        }

        public ReadOnlySpan<byte> Bytes(int count)
        {
            if (count > Remaining)
            {
                throw Truncated();
            }

            var bytes = _data.Slice(_position, count);
            _position += count;
            return bytes;
        }

        /// <summary>A reader over the next <paramref name="length"/> bytes, which this one skips.</summary>
        public Reader Sub(int length) => new(Bytes(length));

        private static InvalidDataException Truncated() => new("The block is truncated.");
    }
}
