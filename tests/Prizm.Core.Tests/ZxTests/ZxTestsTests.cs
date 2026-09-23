// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using Prizm.Core.Tape;

namespace Prizm.Core.Tests.ZxTests;

public class ZxTestsTests
{
    [Theory]
    [InlineData("btime.tap", "btime")]
    [InlineData("stime.tap", "stime")]
    [InlineData("ulatest3.tap", "ulatest3")]
    public void TapFiles_ParseAndContainExpectedPrograms(string fileName, string expectedName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ZxTests", fileName);
        var bytes = File.ReadAllBytes(path);

        var tap = TapFile.Parse(bytes);
        Assert.True(tap.Blocks.Count >= 2, $"Expected at least 2 blocks in {fileName}");

        var tapeImage = TapeImage.FromTap(tap);
        Assert.Equal(tap.Blocks.Count, tapeImage.Blocks.Count);

        // First block is a BASIC program header with flag byte 0x00.
        var header = tap.Blocks[0];
        Assert.Equal(0x00, header[0]); // Header flag
        Assert.Equal(0x00, header[1]); // Program type

        var programName = System.Text.Encoding.ASCII.GetString(header, 2, 10).TrimEnd();
        Assert.Equal(expectedName, programName);
    }
}
