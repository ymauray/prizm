// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Diagnostics;
using System.Numerics;
using iSpectrum.App;
using iSpectrum.Core;
using iSpectrum.Core.Snapshots;
using iSpectrum.Core.Tape;
using Raylib_cs;

const int Scale = 3;
const int FramesPerSecond = 50;

// With sound, the loop redraws at the display's refresh rate (capped here) and runs as many
// frames as the audio needs; this caps the catch-up after a stall, such as a window drag.
const int MaxRedrawsPerSecond = 120;
const int MaxFramesPerRedraw = 4;

// In turbo, frames run for this long per redraw, leaving time to draw.
const double TurboMillisecondsPerRedraw = 12;
const string Title = "iSpectrum";

var rom48 = ReadRom("48.rom");
var rom128 = (ReadRom("128-0.rom"), ReadRom("128-1.rom"));

var keyboardInput = new KeyboardInput();
var fileChooser = new FileChooser();
var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var snapshotFolder = Path.Combine(documents, "iSpectrum");
var turboTimer = new Stopwatch();

// Tapes load in real time, with their sound and stripes, unless fast loading is on (Cmd+L);
// turbo (Cmd+T) runs real-time loading as fast as the Mac can.
var fastLoad = false;
var turbo = false;
string? tapeName = null;
var shownBlock = -1;

// The model new machines are built as: Cmd+1 for the 48K, Cmd+2 for the 128K.
var is128 = false;
Spectrum spectrum = NewMachine();

Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
Raylib.InitWindow(Ula.FrameWidth * Scale, Ula.FrameHeight * Scale, Title);

using var audio = new AudioOutput();
Raylib.SetTargetFPS(audio.IsReady ? MaxRedrawsPerSecond : FramesPerSecond);

// Escape is the Spectrum's BREAK, not a way to quit: close the window to quit.
Raylib.SetExitKey(KeyboardKey.Null);

// The frame buffer is RGBA8888, the pixel format of an image made by GenImageColor.
var image = Raylib.GenImageColor(Ula.FrameWidth, Ula.FrameHeight, Color.Black);
var texture = Raylib.LoadTextureFromImage(image);
Raylib.UnloadImage(image);
Raylib.SetTextureFilter(texture, TextureFilter.Point);

var source = new Rectangle(0, 0, Ula.FrameWidth, Ula.FrameHeight);

// A file can be given on the command line: dotnet run --project src/iSpectrum.App -- game.tap
if (args.Length > 0)
{
    Open(args[0]);
}

while (!Raylib.WindowShouldClose())
{
    if (Raylib.IsFileDropped())
    {
        foreach (var path in Raylib.GetDroppedFiles())
        {
            if (CanOpen(path))
            {
                Open(path);
                break;
            }
        }
    }

    if (fileChooser.PollResult() is { } chosen)
    {
        Open(chosen);
    }

    HandleShortcuts();

    // Keyboard events must be read on every redraw: Raylib drops them at the next one.
    keyboardInput.Update(spectrum.Keyboard);

    if (turbo && IsLoading())
    {
        // As many frames as fit in the time budget, without picture or sound, then one drawn
        // frame so that the screen keeps moving.
        turboTimer.Restart();
        spectrum.Headless = true;
        while (turboTimer.Elapsed.TotalMilliseconds < TurboMillisecondsPerRedraw && IsLoading())
        {
            RunFrame();
        }

        spectrum.Headless = false;
        RunFrame();
        audio.Pump();
    }
    else if (audio.IsReady)
    {
        // The sound card sets the pace: run frames until enough sound is waiting.
        for (var frame = 0; frame < MaxFramesPerRedraw && audio.Queued < AudioOutput.TargetQueued; frame++)
        {
            RunFrame();
            audio.Write(spectrum.AudioSamples);
        }

        audio.Pump();
    }
    else
    {
        RunFrame();
    }

    ShowTapeProgress();
    Raylib.UpdateTexture(texture, spectrum.FrameBuffer);

    Raylib.BeginDrawing();
    var destination = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0, Color.White);
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(texture);
Raylib.CloseWindow();

void RunFrame()
{
    spectrum.RunFrame();
    keyboardInput.FrameRan();
}

// Typing LOAD "" or playing the tape: what turbo speeds up.
bool IsLoading() => spectrum.AutoTyper.IsBusy || spectrum.Tape.IsPlaying;

// Cmd+S saves a snapshot, Cmd+O chooses a file to open, Cmd+F shows the snapshot folder,
// Cmd+L switches fast tape loading, Cmd+T switches turbo, Cmd+1 and Cmd+2 power on a 48K or a
// 128K (Cmd+M is macOS's Minimize), Cmd+R resets.
void HandleShortcuts()
{
    if (!Raylib.IsKeyDown(KeyboardKey.LeftSuper) && !Raylib.IsKeyDown(KeyboardKey.RightSuper))
    {
        return;
    }

    if (Raylib.IsKeyPressed(KeyboardKey.S))
    {
        SaveSnapshot();
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.O) && !fileChooser.IsOpen)
    {
        if (!fileChooser.TryOpen(Directory.Exists(snapshotFolder) ? snapshotFolder : documents))
        {
            Report("no file chooser on this system: drop a file on the window instead");
        }
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.F))
    {
        HostShell.OpenFolder(snapshotFolder);
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.L))
    {
        fastLoad = !fastLoad;
        spectrum.FastLoad = fastLoad;
        Report(fastLoad ? "fast tape loading" : "real-time tape loading");
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.T))
    {
        turbo = !turbo;
        Report(turbo ? "turbo while loading" : "normal speed while loading");
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Two))
    {
        is128 = Raylib.IsKeyPressed(KeyboardKey.Two);
        Reset();
    }
    else if (Raylib.IsKeyPressed(KeyboardKey.R))
    {
        Reset();
    }
}

bool CanOpen(string path) => Snapshot.IsSupported(path) || IsTape(path);

bool IsTape(string path) => Path.GetExtension(path).Equals(".tap", StringComparison.OrdinalIgnoreCase);

void Open(string path)
{
    if (IsTape(path))
    {
        InsertTape(path);
    }
    else
    {
        LoadSnapshot(path);
    }
}

// A tape starts on a freshly powered-on machine, which types LOAD "" once it has booted.
void InsertTape(string path)
{
    try
    {
        var tape = TapFile.Parse(File.ReadAllBytes(path));
        var machine = NewMachine();
        machine.Tape.Insert(tape);
        machine.LoadTapeAfterBoot();
        spectrum = machine;
        tapeName = Path.GetFileName(path);
        shownBlock = -1;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
    {
        Report($"cannot load {Path.GetFileName(path)}: {e.Message}");
    }
}

// Loads into a fresh machine first, so that a bad file leaves the running one untouched.
void LoadSnapshot(string path)
{
    try
    {
        var loaded = new Spectrum48(rom48) { FastLoad = fastLoad };
        Snapshot.Load(loaded, path, File.ReadAllBytes(path));
        spectrum = loaded;
        tapeName = null;
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
        if (spectrum is not Spectrum48 spectrum48)
        {
            Report("128K snapshots cannot be saved yet");
            return;
        }

        File.WriteAllBytes(path, SnaFormat.Save(spectrum48));
        Report($"saved {path}");
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        Report($"cannot save: {e.Message}");
    }
}

byte[] ReadRom(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", name));

Spectrum NewMachine() => is128
    ? new Spectrum128(rom128.Item1, rom128.Item2) { FastLoad = fastLoad }
    : new Spectrum48(rom48) { FastLoad = fastLoad };

// A freshly powered-on machine of the current model.
void Reset()
{
    spectrum = NewMachine();
    tapeName = null;
    Report(is128 ? "ZX Spectrum 128K" : "ZX Spectrum 48K");
}

// Shows which block of the tape is loading, when that changes.
void ShowTapeProgress()
{
    var tape = spectrum.Tape;
    if (tapeName is null || tape.CurrentBlock == shownBlock)
    {
        return;
    }

    shownBlock = tape.CurrentBlock;
    Report(tape.AtEnd
        ? $"{tapeName} (end of tape)"
        : $"{tapeName} (block {tape.CurrentBlock + 1}/{tape.BlockCount})");
}

// There is no on-screen text yet: messages go to the window title and the console.
void Report(string message)
{
    Raylib.SetWindowTitle($"{Title} — {message}");
    Console.WriteLine(message);
}
