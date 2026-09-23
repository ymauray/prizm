namespace iSpectrum.Core;

/// <summary>
/// The 16 Spectrum colours (8 normal, then the same 8 bright; both blacks are equal), as
/// 32-bit values whose bytes are R, G, B, A in memory order (RGBA8888 on little-endian hosts).
/// </summary>
public static class Palette
{
    // Normal colours use 0xD7 per channel, bright ones 0xFF. Real sets vary; this is a common choice.
    private const int Normal = 0xD7;
    private const int Bright = 0xFF;

    public static readonly uint[] Colors = Build();

    /// <summary>Palette index: bits 0-2 are the colour (blue, red, green), bit 3 is bright.</summary>
    public static int Index(int color, bool bright) => color | (bright ? 8 : 0);

    private static uint[] Build()
    {
        var colors = new uint[16];
        for (var i = 0; i < 16; i++)
        {
            var level = (i & 8) != 0 ? Bright : Normal;
            var blue = (i & 1) != 0 ? level : 0;
            var red = (i & 2) != 0 ? level : 0;
            var green = (i & 4) != 0 ? level : 0;
            colors[i] = 0xFF000000u | (uint)(blue << 16) | (uint)(green << 8) | (uint)red;
        }

        return colors;
    }
}
