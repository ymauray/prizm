// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Z80;

namespace iSpectrum.Core.Tests;

public class UlaTests
{
    private readonly byte[] _memory = new byte[0x10000];
    private readonly Ula _ula = new();

    private uint Pixel(int x, int y) =>
        _ula.FrameBuffer[((y + Ula.BorderTop) * Ula.FrameWidth) + x + Ula.BorderLeft];

    private static uint Color(int color, bool bright = false) => Palette.Colors[Palette.Index(color, bright)];

    [Fact]
    public void SetPixels_UseInk_AndClearPixels_UsePaper()
    {
        // Leftmost pixel set; ink blue (1), paper yellow (6).
        _memory[ScreenLayout.PixelAddress(0, 0)] = 0b1000_0000;
        _memory[ScreenLayout.AttributeAddress(0, 0)] = (6 << 3) | 1;

        _ula.EndFrame(_memory);

        Assert.Equal(Color(1), Pixel(0, 0));
        Assert.Equal(Color(6), Pixel(1, 0));
    }

    [Fact]
    public void BrightAttribute_SelectsTheBrightColours()
    {
        _memory[ScreenLayout.PixelAddress(8, 8)] = 0xFF;
        _memory[ScreenLayout.AttributeAddress(8, 8)] = 0x40 | 2; // bright, red ink

        _ula.EndFrame(_memory);

        Assert.Equal(Color(2, bright: true), Pixel(8, 8));
        Assert.NotEqual(Color(2), Pixel(8, 8));
    }

    [Fact]
    public void FlashAttribute_SwapsInkAndPaperEvery16Frames()
    {
        _memory[ScreenLayout.PixelAddress(0, 0)] = 0xFF;
        _memory[ScreenLayout.AttributeAddress(0, 0)] = 0x80 | (7 << 3); // flash, white paper, black ink

        for (var frame = 0; frame < 16; frame++)
        {
            _ula.EndFrame(_memory);
            Assert.Equal(Color(0), Pixel(0, 0));
        }

        for (var frame = 16; frame < 32; frame++)
        {
            _ula.EndFrame(_memory);
            Assert.Equal(Color(7), Pixel(0, 0));
        }

        _ula.EndFrame(_memory);
        Assert.Equal(Color(0), Pixel(0, 0));
    }

    [Fact]
    public void BorderChange_ShowsFromTheBeamPositionAtWhichItHappened()
    {
        var cpu = new Z80Cpu(new FlatRam(), _ula);
        _ula.Connect(cpu, new Memory48K(new byte[Memory48K.RomSize]));
        _ula.Border = 1;

        // Blue until the beam reaches the left border of screen line 100 (32 pixels, 16 T-states
        // before its first screen pixel), then red.
        cpu.TStates = Ula.FirstPixelTState + (100 * Ula.LineTStates) - 16;
        _ula.Out(0x00FE, 0x02);
        _ula.EndFrame(_memory);

        var row = (100 + Ula.BorderTop) * Ula.FrameWidth;
        Assert.Equal(Color(1), _ula.FrameBuffer[row - 1]);           // right border of line 99
        Assert.Equal(Color(2), _ula.FrameBuffer[row]);               // left border of line 100
        Assert.Equal(Color(1), _ula.FrameBuffer[0]);                 // top border
        Assert.Equal(Color(2), _ula.FrameBuffer[^1]);                // bottom border
    }

    [Fact]
    public void ScreenWrite_WhileTheBeamIsOnScreen_ChangesOnlyTheLinesBelowIt()
    {
        // White paper everywhere, except that character row 12 (pixel lines 96-103) turns red
        // when the beam starts pixel line 100: lines 96-99 keep the old colour.
        var spectrum = new Spectrum48(new byte[Memory48K.RomSize]);
        for (var address = 0x5800; address < 0x5B00; address++)
        {
            spectrum.Memory.Write((ushort)address, 7 << 3);
        }

        spectrum.Cpu.TStates = Ula.FirstPixelTState + (100 * Ula.LineTStates);
        spectrum.Memory.Write(ScreenLayout.AttributeAddress(0, 96), 2 << 3);
        spectrum.Ula.EndFrame(spectrum.Memory.Contents);

        uint ScreenPixel(int y) => spectrum.FrameBuffer[((y + Ula.BorderTop) * Ula.FrameWidth) + Ula.BorderLeft];
        Assert.Equal(Color(7), ScreenPixel(99));
        Assert.Equal(Color(2), ScreenPixel(100));
        Assert.Equal(Color(2), ScreenPixel(103));
    }

    [Fact]
    public void BorderChanges_StartOverAtTheNextFrame()
    {
        _ula.Out(0x00FE, 0x04);
        _ula.EndFrame(_memory);
        _ula.EndFrame(_memory);

        Assert.Equal(Color(4), _ula.FrameBuffer[0]);
    }

    [Fact]
    public void Border_TakesBits0To2OfAnEvenPortWrite()
    {
        _ula.Out(0x00FE, 0xF3); // only bits 0-2 count: 3 = magenta
        _ula.Out(0x00FF, 0x01); // odd port: not the ULA

        _ula.EndFrame(_memory);

        Assert.Equal(3, _ula.Border);
        Assert.Equal(Color(3), _ula.FrameBuffer[0]);
        Assert.Equal(Color(3), _ula.FrameBuffer[^1]);
    }

    private sealed class FlatRam : IMemory
    {
        public byte Read(ushort address) => 0;

        public void Write(ushort address, byte value) { }
    }
}
