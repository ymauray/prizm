using System.Numerics;
using iSpectrum.Core;
using Raylib_cs;

const int Scale = 3;
const int FramesPerSecond = 50;

var romPath = Path.Combine(AppContext.BaseDirectory, "roms", "48.rom");
var spectrum = new Spectrum48(File.ReadAllBytes(romPath));

Raylib.InitWindow(Ula.FrameWidth * Scale, Ula.FrameHeight * Scale, "iSpectrum");
Raylib.SetTargetFPS(FramesPerSecond);

// The frame buffer is RGBA8888, the pixel format of an image made by GenImageColor.
var image = Raylib.GenImageColor(Ula.FrameWidth, Ula.FrameHeight, Color.Black);
var texture = Raylib.LoadTextureFromImage(image);
Raylib.UnloadImage(image);
Raylib.SetTextureFilter(texture, TextureFilter.Point);

var source = new Rectangle(0, 0, Ula.FrameWidth, Ula.FrameHeight);

while (!Raylib.WindowShouldClose())
{
    spectrum.RunFrame();
    Raylib.UpdateTexture(texture, spectrum.FrameBuffer);

    Raylib.BeginDrawing();
    var destination = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0, Color.White);
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(texture);
Raylib.CloseWindow();
