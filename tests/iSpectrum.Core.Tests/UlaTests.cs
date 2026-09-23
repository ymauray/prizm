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
    public void Border_TakesBits0To2OfAnEvenPortWrite()
    {
        _ula.Out(0x00FE, 0xF3); // only bits 0-2 count: 3 = magenta
        _ula.Out(0x00FF, 0x01); // odd port: not the ULA

        _ula.EndFrame(_memory);

        Assert.Equal(3, _ula.Border);
        Assert.Equal(Color(3), _ula.FrameBuffer[0]);
        Assert.Equal(Color(3), _ula.FrameBuffer[^1]);
    }
}
