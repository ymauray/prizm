// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

/// <summary>
/// The TZX test files of libspectrum (FUSE's library), and the edges its own tests expect from
/// them (test/tape-edges.c), in its notation: (T-states, count, flags).
/// </summary>
public class TzxFileTests
{
    // libspectrum's flags, as its tests compare them.
    private const int Block = 1;
    private const int Stop = 2;
    private const int Stop48 = 4;
    private const int NoEdge = 8;
    private const int LevelLow = 16;
    private const int LevelHigh = 32;
    private const int EndOfTape = 256;

    private static TapeImage Load(string name) =>
        TzxFile.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Tape", "Libspectrum", name)));

    /// <summary>Every edge of a tape, in libspectrum's notation, up to its end.</summary>
    private static List<(int TStates, int Flags)> Edges(TapeImage tape)
    {
        var cursor = new TapeCursor(tape);
        var edges = new List<(int, int)>();
        while (!cursor.AtEnd)
        {
            Assert.True(edges.Count < 1_000_000, "The tape does not end.");
            var edge = cursor.Next();

            var flags = edge.Transition switch
            {
                TapeTransition.Low => LevelLow,
                TapeTransition.High => LevelHigh,
                TapeTransition.None when edge.Duration == 0 => NoEdge,
                _ => 0,
            };

            flags |= (edge.Flags & TapeEdgeFlags.EndOfBlock) != 0 ? Block : 0;
            flags |= (edge.Flags & TapeEdgeFlags.Stop) != 0 ? Stop : 0;
            flags |= (edge.Flags & TapeEdgeFlags.Stop48) != 0 ? Stop48 : 0;
            flags |= (edge.Flags & TapeEdgeFlags.EndOfTape) != 0 ? EndOfTape | Stop : 0;
            edges.Add((edge.Duration, flags));
        }

        return edges;
    }

    private static void AssertEdges((int TStates, int Count, int Flags)[] expected, TapeImage tape, int mask)
    {
        var actual = Edges(tape);
        var index = 0;
        foreach (var (tStates, count, flags) in expected)
        {
            for (var i = 0; i < count; i++, index++)
            {
                Assert.True(index < actual.Count, $"The tape ends after {actual.Count} edges.");
                Assert.Equal((tStates, flags & mask), (actual[index].TStates, actual[index].Flags & mask));
            }
        }

        Assert.Equal(index, actual.Count);
    }

    /// <summary>complete-tzx.tzx, written by complete-tzx.pl: one block of nearly every kind.</summary>
    [Fact]
    public void CompleteTzx_PlaysEveryBlock()
    {
        (int, int, int)[] expected =
        [
            // Standard speed data block: pilot, sync 1 and 2, the bits of 0xAA, pause.
            (2168, 3223, 0), (667, 1, 0), (735, 1, 0),
            (1710, 2, 0), (855, 2, 0), (1710, 2, 0), (855, 2, 0),
            (1710, 2, 0), (855, 2, 0), (1710, 2, 0), (855, 2, 0),
            (8207500, 1, 0),

            // Turbo speed data block: pilot, sync 1 and 2, bytes 1 and 2, bits of bytes 3 and 4, pause.
            (1000, 5, 0), (123, 1, 0), (456, 1, 0),
            (789, 16, 0), (400, 16, 0),
            (789, 2, 0), (400, 2, 0), (789, 2, 0), (400, 2, 0),
            (789, 2, 0), (400, 2, 0), (789, 2, 0), (400, 2, 0),
            (400, 2, 0), (789, 2, 0), (400, 2, 0), (789, 2, 0),
            (3454500, 1, 0),

            // Pure tone, list of pulses.
            (535, 666, 0),
            (772, 1, 0), (297, 1, 0), (692, 1, 0),

            // Pure data: bytes 1 and 2, 6 bits of byte 3, pause.
            (1639, 16, 0), (552, 16, 0), (1639, 12, 0), (1939000, 1, 0),

            // Pause block; group start and end; a jump over a pure tone; loop start.
            (2163000, 1, 0),
            (0, 1, NoEdge), (0, 1, NoEdge),
            (0, 1, NoEdge),
            (0, 1, NoEdge),

            // Three times: a pure tone, the loop end.
            (837, 185, 0), (0, 1, NoEdge),
            (837, 185, 0), (0, 1, NoEdge),
            (837, 185, 0), (0, 1, NoEdge),

            // Stop the tape if in 48K mode.
            (0, 1, Stop48 | NoEdge),

            // Text description, message, archive info, hardware info, custom info.
            (0, 1, NoEdge), (0, 1, NoEdge), (0, 1, NoEdge), (0, 1, NoEdge), (0, 1, NoEdge),

            // Pure tone, the end of the tape.
            (820, 940, 0), (820, 1, Stop),
        ];

        AssertEdges(expected, Load("complete-tzx.tzx"), Stop | Stop48 | NoEdge);
    }

    [Fact]
    public void TrailingPauseBlock_StartsLow_AndPausesLow()
    {
        (int, int, int)[] expected =
        [
            // Standard speed data block: the tape starts low; then the bits of 0xFF, no pause.
            (2168, 1, LevelLow), (2168, 3222, 0), (667, 1, 0), (735, 1, 0),
            (1710, 16, 0),
            (0, 1, NoEdge),

            // A one-second pause block, held low.
            (3500000, 1, LevelLow),
        ];

        AssertEdges(expected, Load("trailing-pause-block.tzx"), NoEdge | LevelLow | LevelHigh);
    }

    [Fact]
    public void RawDataBlock_GivesOnePulsePerRunOfSamples()
    {
        // Samples of 10 T-states: 0xF0, 0xF0, then the top 4 bits of 0xB0.
        (int, int, int)[] expected =
        [
            (40, 1, LevelHigh), (40, 1, LevelLow), (40, 1, LevelHigh), (40, 1, LevelLow),
            (10, 1, LevelHigh), (10, 1, LevelLow), (20, 1, LevelHigh),

            // The edge that ends the last pulse, at the end of the tape.
            (0, 1, Block | Stop | EndOfTape),
        ];

        AssertEdges(expected, Load("raw-data-block.tzx"), 0xFFFF);
    }

    [Fact]
    public void GeneralizedDataBlock_WithoutPilot_PlaysItsSymbols()
    {
        var expected = new List<(int, int, int)> { (0, 1, LevelHigh | Block) };

        // Two bytes of 0xFF, each bit symbol 1: two pulses of 771 and 1542 T-states.
        for (var bit = 0; bit < 16; bit++)
        {
            expected.Add((771, 1, 0));
            expected.Add((1542, 1, 0));
        }

        // The edge that ends the last pulse, at the end of the tape.
        expected.Add((0, 1, Block | Stop | EndOfTape));

        AssertEdges([.. expected], Load("no-pilot-gdb.tzx"), 0x1FF);
    }

    [Theory]
    [InlineData("loop.tzx")]
    [InlineData("loop2.tzx")]
    [InlineData("loopend.tzx")]
    [InlineData("jump.tzx")]
    [InlineData("empty-drb.tzx")]
    [InlineData("pure-data-usedbits-zero.tzx")]
    [InlineData("turbo-zeropilot.tzx")]
    public void OddButValidFiles_PlayToTheirEnd(string name)
    {
        Assert.NotEmpty(Edges(Load(name)));
    }

    [Theory]
    [InlineData("invalid.tzx")]
    [InlineData("invalid-gdb.tzx")]
    [InlineData("invalid-archiveinfo.tzx")]
    [InlineData("invalid-hardwareinfo.tzx")]
    [InlineData("invalid-custominfo.tzx")]
    public void CorruptFiles_AreRejected(string name)
    {
        Assert.Throws<InvalidDataException>(() => Load(name));
    }

    [Fact]
    public void AFileWithoutTheSignature_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => TzxFile.Parse("ZXTape?\u001A\u0001\u0014"u8));
    }

    [Fact]
    public void StandardAndRomSpeedTurboBlocks_CanBeFastLoaded_OtherTurboBlocksCannot()
    {
        byte[] data = [0xFF, 1, 2, 3];
        var tape = TapeBuilder.Tzx(
            TapeBuilder.StandardBlock(data),
            TapeBuilder.TurboBlock(data, TapePlayer.ZeroPulse, TapePlayer.OnePulse),
            TapeBuilder.TurboBlock(data, 400, 800));

        Assert.Equal(data, tape.Blocks[0].RomData);
        Assert.Equal(data, tape.Blocks[1].RomData);
        Assert.Null(tape.Blocks[2].RomData);
    }
}
