// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Numerics;
using iSpectrum.App;
using iSpectrum.Core;
using iSpectrum.Core.Snapshots;
using Raylib_cs;

const int Scale = 3;
const int FramesPerSecond = 50;
const string Title = "iSpectrum";

var rom = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom"));
var spectrum = new Spectrum48(rom);
var keyboardInput = new KeyboardInput();
var fileChooser = new FileChooser();
var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var snapshotFolder = Path.Combine(documents, "iSpectrum");

Raylib.InitWindow(Ula.FrameWidth * Scale, Ula.FrameHeight * Scale, Title);
Raylib.SetTargetFPS(FramesPerSecond);

// Escape is the Spectrum's BREAK, not a way to quit: close the window to quit.
Raylib.SetExitKey(KeyboardKey.Null);

// The frame buffer is RGBA8888, the pixel format of an image made by GenImageColor.
var image = Raylib.GenImageColor(Ula.FrameWidth, Ula.FrameHeight, Color.Black);
var texture = Raylib.LoadTextureFromImage(image);
Raylib.UnloadImage(image);
Raylib.SetTextureFilter(texture, TextureFilter.Point);

var source = new Rectangle(0, 0, Ula.FrameWidth, Ula.FrameHeight);

// A snapshot can be given on the command line: dotnet run --project src/iSpectrum.App -- game.z80
if (args.Length > 0)
{
    LoadSnapshot(args[0]);
}

while (!Raylib.WindowShouldClose())
{
    if (Raylib.IsFileDropped())
    {
        foreach (var path in Raylib.GetDroppedFiles())
        {
            if (Snapshot.IsSupported(path))
            {
                LoadSnapshot(path);
                break;
            }
        }
    }

    if (fileChooser.PollResult() is { } chosen)
    {
        LoadSnapshot(chosen);
    }

    // Cmd+S saves a snapshot, Cmd+O chooses one to load, Cmd+F shows the snapshot folder.
    var command = Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper);
    if (command && Raylib.IsKeyPressed(KeyboardKey.S))
    {
        SaveSnapshot();
    }
    else if (command && Raylib.IsKeyPressed(KeyboardKey.O) && !fileChooser.IsOpen)
    {
        if (!fileChooser.TryOpen(Directory.Exists(snapshotFolder) ? snapshotFolder : documents))
        {
            Report("no file chooser on this system: drop a file on the window instead");
        }
    }
    else if (command && Raylib.IsKeyPressed(KeyboardKey.F))
    {
        HostShell.OpenFolder(snapshotFolder);
    }

    keyboardInput.Update(spectrum.Keyboard);
    spectrum.RunFrame();
    Raylib.UpdateTexture(texture, spectrum.FrameBuffer);

    Raylib.BeginDrawing();
    var destination = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0, Color.White);
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(texture);
Raylib.CloseWindow();

// Loads into a fresh machine first, so that a bad file leaves the running one untouched.
void LoadSnapshot(string path)
{
    try
    {
        var loaded = new Spectrum48(rom);
        Snapshot.Load(loaded, path, File.ReadAllBytes(path));
        spectrum = loaded;
        Report(Path.GetFileName(path));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
    {
        Report($"cannot load {Path.GetFileName(path)}: {e.Message}");
    }
}

void SaveSnapshot()
{
    try
    {
        Directory.CreateDirectory(snapshotFolder);
        var path = Path.Combine(snapshotFolder, $"iSpectrum-{DateTime.Now:yyyyMMdd-HHmmss}.sna");
        File.WriteAllBytes(path, SnaFormat.Save(spectrum));
        Report($"saved {path}");
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        Report($"cannot save: {e.Message}");
    }
}

// There is no on-screen text yet: messages go to the window title and the console.
void Report(string message)
{
    Raylib.SetWindowTitle($"{Title} — {message}");
    Console.WriteLine(message);
}
