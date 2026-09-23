// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Core.Debugging;

namespace Prizm.Core.Tests.Debugging;

public class SymbolTableTests
{
    /// <summary>A symbol file written by sjasmplus 1.24.0 (--sym) for a small program of ours.</summary>
    private static SymbolTable Sjasmplus() =>
        SymbolTable.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Debugging", "hello.sym")));

    [Fact]
    public void SjasmplusSymbols_AreRead()
    {
        var table = Sjasmplus();

        Assert.Equal(6, table.Count);
        Assert.True(table.TryGetAddress("start", out var start));
        Assert.Equal(0x8000, start);
        Assert.True(table.TryGetAddress("start.next", out var next));
        Assert.Equal(0x8008, next);
        Assert.True(table.TryGetAddress("chan_open", out var chanOpen)); // any case
        Assert.Equal(0x1601, chanOpen);
    }

    [Fact]
    public void NameAt_GivesTheNameToShowForAnAddress()
    {
        var table = Sjasmplus();

        Assert.Equal("start", table.NameAt(0x8000));
        Assert.Equal("message", table.NameAt(0x8011));
        Assert.Null(table.NameAt(0x8001));
    }

    [Fact]
    public void ALabelBeatsALocalLabel_ForTheSameAddress()
    {
        var table = SymbolTable.Parse("loop.inner: EQU 0x9000\nloop: EQU 0x9000\n");

        Assert.Equal("loop", table.NameAt(0x9000));
    }

    [Theory]
    [InlineData("score EQU 0A000h")]      // pasmo and others
    [InlineData("score equ $A000")]
    [InlineData("score = $A000 ; addr, local, , game")] // z88dk .map
    [InlineData("  score: EQU #A000")]
    [InlineData("score EQU 40960")]
    public void OtherAssemblers_Formats_AreRead(string line)
    {
        var table = SymbolTable.Parse(line);

        Assert.True(table.TryGetAddress("score", out var address));
        Assert.Equal(0xA000, address);
    }

    [Fact]
    public void OtherLines_AreSkipped()
    {
        var table = SymbolTable.Parse("; a comment\n\n    ld a,2\nstart: EQU 0x00008000\n");

        Assert.Equal(1, table.Count);
    }

    [Theory]
    [InlineData("$8000", 0x8000)]
    [InlineData("0x8000", 0x8000)]
    [InlineData("8000h", 0x8000)]
    [InlineData("32768", 0x8000)]
    [InlineData("start", 0x8000)]
    [InlineData("start+3", 0x8003)]
    [InlineData("message - 1", 0x8010)]
    [InlineData("start.next+$10", 0x8018)]
    public void TypedAddresses_AreRead(string text, int expected)
    {
        Assert.True(Sjasmplus().TryParseAddress(text, out var address));
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nowhere")]
    [InlineData("start+")]
    [InlineData("$zz")]
    public void UnknownOrMalformedAddresses_AreRefused(string text) =>
        Assert.False(Sjasmplus().TryParseAddress(text, out _));
}
