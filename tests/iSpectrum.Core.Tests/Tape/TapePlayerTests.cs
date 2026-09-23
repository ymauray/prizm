// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Tape;

namespace iSpectrum.Core.Tests.Tape;

public class TapePlayerTests
{
    private readonly TapePlayer _player = new();
    private readonly Beeper _beeper = new();

    /// <summary>The edge times the ROM's timings give for one data block, starting at 0.</summary>
    private static List<long> ExpectedEdges(byte[] block)
    {
        var edges = new List<long>();
        long time = 0;

        void Pulse(int length)
        {
            time += length;
            edges.Add(time);
        }

        for (var i = 0; i < TapePlayer.DataPilotPulses; i++)
        {
            Pulse(TapePlayer.PilotPulse);
        }

        Pulse(TapePlayer.FirstSyncPulse);
        Pulse(TapePlayer.SecondSyncPulse);

        foreach (var value in block)
        {
            for (var mask = 0x80; mask != 0; mask >>= 1)
            {
                var length = (value & mask) != 0 ? TapePlayer.OnePulse : TapePlayer.ZeroPulse;
                Pulse(length);
                Pulse(length);
            }
        }

        return edges;
    }

    [Fact]
    public void DataBlock_FlipsTheLevelAtEachExpectedEdge()
    {
        var block = TapFile.MakeBlock(0xFF, [0xA5]);
        _player.Insert(TapeBuilder.Tap(block));
        _player.Play(0);

        var level = _player.Level;
        foreach (var edge in ExpectedEdges(block))
        {
            _player.AdvanceTo(edge - 1, _beeper);
            Assert.Equal(level, _player.Level);

            _player.AdvanceTo(edge, _beeper);
            level = !level;
            Assert.Equal(level, _player.Level);
        }
    }

    [Fact]
    public void Tape_StopsAfterThePauseThatFollowsTheLastBlock()
    {
        var block = TapFile.MakeBlock(0xFF, [0x00]);
        _player.Insert(TapeBuilder.Tap(block));
        _player.Play(0);
        var lastEdge = ExpectedEdges(block)[^1];

        _player.AdvanceTo(lastEdge + TapePlayer.PauseTStates - 1, _beeper);
        Assert.True(_player.IsPlaying);

        _player.AdvanceTo(lastEdge + TapePlayer.PauseTStates, _beeper);
        Assert.False(_player.IsPlaying);
        Assert.True(_player.AtEnd);
    }

    [Fact]
    public void Headers_HaveTheLongerPilotTone()
    {
        _player.Insert(TapeBuilder.Tap(TapFile.MakeBlock(0x00, [0x00])));
        _player.Play(0);

        // After the 3223 pulses of a data pilot, a header is still in its pilot tone: the next
        // edge is one pilot pulse later, not a sync pulse.
        var afterDataPilot = (long)TapePlayer.DataPilotPulses * TapePlayer.PilotPulse;
        _player.AdvanceTo(afterDataPilot, _beeper);
        var level = _player.Level;
        _player.AdvanceTo(afterDataPilot + TapePlayer.FirstSyncPulse, _beeper);
        Assert.Equal(level, _player.Level);
    }

    [Fact]
    public void TakeBlock_HandsOutTheBlocksInOrder()
    {
        _player.Insert(TapeBuilder.HelloProgram());

        Assert.Equal(0x00, _player.TakeBlock()![0]);
        Assert.Equal(0xFF, _player.TakeBlock()![0]);
        Assert.Null(_player.TakeBlock());
    }

    [Fact]
    public void TakeBlock_PassesOverBlocksWithoutSound()
    {
        byte[] data = [0xFF, 0x42, 0xBD];
        _player.Insert(TapeBuilder.Tzx(TapeBuilder.TextBlock("title"), TapeBuilder.PauseBlock(500), TapeBuilder.StandardBlock(data)));

        Assert.Equal(data, _player.TakeBlock());
        Assert.True(_player.AtEnd);
    }

    [Fact]
    public void TakeBlock_LeavesATurboBlockToBePlayed()
    {
        _player.Insert(TapeBuilder.Tzx(TapeBuilder.TurboBlock([0xFF, 0x00], 400, 800)));

        Assert.Null(_player.TakeBlock());
        Assert.Equal(0, _player.CurrentBlock);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StopIf48KBlock_StopsTheTapeOnA48KOnly(bool is48K)
    {
        byte[] stopIf48K = [0x2A, 0, 0, 0, 0];
        _player.Is48K = is48K;
        _player.Insert(TapeBuilder.Tzx(stopIf48K, TapeBuilder.StandardBlock([0xFF, 0x00])));
        _player.Play(0);

        _player.AdvanceTo(10_000, _beeper);

        Assert.Equal(!is48K, _player.IsPlaying);
        Assert.Equal(1, _player.CurrentBlock);
    }

    [Fact]
    public void ATapeThatLoopsWithoutSound_IsStopped()
    {
        // A jump back to the text block before it: round and round, without a sound.
        byte[] jumpBack = [0x23, 0xFF, 0xFF];
        _player.Insert(TapeBuilder.Tzx(TapeBuilder.TextBlock("again"), jumpBack));
        _player.Play(0);

        _player.AdvanceTo(0, _beeper);

        Assert.False(_player.IsPlaying);
    }

    /// <summary>
    /// Out Run's tape ends with a data block without a pause, then a block that stops the tape:
    /// the edge that ends its last pulse must come, and last long enough for the loader to see.
    /// </summary>
    [Fact]
    public void ALastPulseWithoutPause_EndsWithAnEdge_BeforeTheTapeStops()
    {
        // Pure data: pulses of 500 (0) and 1000 (1), 8 bits used, no pause, one byte 0x00.
        byte[] pureData = [0x14, 0xF4, 0x01, 0xE8, 0x03, 8, 0, 0, 1, 0, 0, 0x00];
        _player.Insert(TapeBuilder.Tzx(pureData, TapeBuilder.PauseBlock(0)));
        _player.Play(0);
        const long end = 16 * 500;

        _player.AdvanceTo(end - 1, _beeper);
        var level = _player.Level;
        _player.AdvanceTo(end, _beeper);
        Assert.NotEqual(level, _player.Level);

        _player.AdvanceTo(end + 3000, _beeper);
        Assert.True(_player.IsPlaying);

        _player.AdvanceTo(end + 3500, _beeper);
        Assert.False(_player.IsPlaying);
    }
}
