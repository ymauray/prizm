// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using System.Diagnostics;
using System.Numerics;
using iSpectrum.App;
using iSpectrum.Core;
using iSpectrum.Core.Debugging;
using Debugger = iSpectrum.Core.Debugging.Debugger;
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

// With the debugger shown, the picture is drawn twice its size to leave room for the panel.
const int DebugScale = 2;

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

// The model new machines are built as (Machine menu).
var is128 = false;
Spectrum spectrum = NewMachine();

// The debugger (Debug menu), attached to the machine while its panel is shown. Symbols come
// from a .sym file next to the file opened; Reload opens that file again.
Debugger? debugger = null;
DebuggerCommands? debuggerCommands = null;
DebuggerPanel? debuggerPanel = null;
var showDebugger = false;
var symbols = new SymbolTable();
string? lastOpened = null;

Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
Raylib.InitWindow(Ula.FrameWidth * Scale, (Ula.FrameHeight * Scale) + MenuBar.Height, Title);

using var audio = new AudioOutput();
Raylib.SetTargetFPS(audio.IsReady ? MaxRedrawsPerSecond : FramesPerSecond);

// Escape is the Spectrum's BREAK, not a way to quit: close the window to quit.
Raylib.SetExitKey(KeyboardKey.Null);

// The frame buffer is RGBA8888, the pixel format of an image made by GenImageColor.
var image = Raylib.GenImageColor(Ula.FrameWidth, Ula.FrameHeight, Color.Black);
var texture = Raylib.LoadTextureFromImage(image);
Raylib.UnloadImage(image);
Raylib.SetTextureFilter(texture, TextureFilter.Point);
using var font = new SpectrumFont(rom48);

var source = new Rectangle(0, 0, Ula.FrameWidth, Ula.FrameHeight);
var quit = false;

// Every command, with its shortcut; the menu bar shows them and answers the shortcuts too.
var menuBar = new MenuBar(
[
    new Menu("File",
    [
        new MenuItem("Open...", KeyboardKey.O, ChooseFile),
        new MenuItem("Reload", KeyboardKey.R, Reload, Shift: true),
        new MenuItem("Save snapshot", KeyboardKey.S, SaveSnapshot),
        new MenuItem("Show snapshot folder", KeyboardKey.F, () => HostShell.OpenFolder(snapshotFolder)),
        MenuItem.Separator,
        new MenuItem("Quit", KeyboardKey.Q, () => quit = true),
    ]),
    new Menu("Machine",
    [
        new MenuItem("ZX Spectrum 48K", KeyboardKey.One, () => SwitchModel(to128: false), () => !is128),
        new MenuItem("ZX Spectrum 128K", KeyboardKey.Two, () => SwitchModel(to128: true), () => is128),
        MenuItem.Separator,
        new MenuItem("Reset", KeyboardKey.R, Reset),
    ]),
    new Menu("Tape",
    [
        new MenuItem("Fast loading", KeyboardKey.L, ToggleFastLoad, () => fastLoad),
        new MenuItem("Turbo while loading", KeyboardKey.T, ToggleTurbo, () => turbo),
    ]),
    new Menu("Debug",
    [
        new MenuItem("Show debugger", KeyboardKey.D, ToggleDebugger, () => showDebugger),
        MenuItem.Separator,
        new MenuItem("Pause / Continue", KeyboardKey.P, () => Debug(d => { if (d.IsPaused) { d.Resume(); } else { d.Pause(); } })),
        new MenuItem("Step into", KeyboardKey.I, () => Debug(d => d.StepInto())),
        new MenuItem("Step over (next)", KeyboardKey.N, () => Debug(d => d.StepOver())),
        new MenuItem("Step out", KeyboardKey.U, () => Debug(d => d.StepOut())),
    ]),
]);

// A file can be given on the command line: dotnet run --project src/iSpectrum.App -- game.tap
if (args.Length > 0)
{
    Open(args[0]);
}

while (!Raylib.WindowShouldClose() && !quit)
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

    menuBar.HandleShortcuts();
    menuBar.Update();
    if (showDebugger)
    {
        debuggerPanel!.Update();
    }

    // Keyboard events must be read on every redraw: Raylib drops them at the next one. While
    // the debugger is paused, they go to its command line instead.
    if (!(showDebugger && debuggerPanel!.HasKeyboard))
    {
        keyboardInput.Update(spectrum.Keyboard);
    }

    if (IsPaused())
    {
        audio.Pump();
    }
    else if (turbo && IsLoading())
    {
        // As many frames as fit in the time budget, without picture or sound, then one drawn
        // frame so that the screen keeps moving.
        turboTimer.Restart();
        spectrum.Headless = true;
        while (turboTimer.Elapsed.TotalMilliseconds < TurboMillisecondsPerRedraw && IsLoading() && !IsPaused())
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
        for (var frame = 0; frame < MaxFramesPerRedraw && audio.Queued < AudioOutput.TargetQueued && !IsPaused(); frame++)
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

    // Stopped in the middle of a frame, the machine shows its memory as it is now, so that each
    // step that writes to the screen can be seen.
    if (IsPaused())
    {
        spectrum.Ula.DrawNow(spectrum.Memory.Screen);
    }

    Raylib.UpdateTexture(texture, spectrum.FrameBuffer);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    if (showDebugger)
    {
        // The picture at twice its size, memory under it, the debugger's column on the right.
        var picture = new Rectangle(0, MenuBar.Height, Ula.FrameWidth * DebugScale, Ula.FrameHeight * DebugScale);
        Raylib.DrawTexturePro(texture, source, picture, Vector2.Zero, 0, Color.White);
        var bottom = MenuBar.Height + (Ula.FrameHeight * DebugScale);
        debuggerPanel!.DrawBottom(0, bottom, Raylib.GetScreenHeight() - bottom);
        debuggerPanel.DrawColumn(Ula.FrameWidth * DebugScale, MenuBar.Height, Raylib.GetScreenHeight() - MenuBar.Height);
    }
    else
    {
        var destination = new Rectangle(0, MenuBar.Height, Raylib.GetScreenWidth(), Raylib.GetScreenHeight() - MenuBar.Height);
        Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0, Color.White);
    }

    menuBar.Draw(Raylib.GetScreenWidth());
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(texture);
Raylib.CloseWindow();

void RunFrame()
{
    if (showDebugger)
    {
        debugger!.RunFrame();
    }
    else
    {
        spectrum.RunFrame();
    }

    keyboardInput.FrameRan();
}

bool IsPaused() => showDebugger && debugger!.IsPaused;

// Shows or hides the debugger: shown, it runs the machine and the window widens for its panel;
// hidden, the machine runs on its own again.
void ToggleDebugger()
{
    showDebugger = !showDebugger;
    if (showDebugger)
    {
        if (debugger is null)
        {
            debugger = new Debugger(spectrum);
            debuggerCommands = new DebuggerCommands(debugger) { Symbols = symbols };
            debuggerPanel = new DebuggerPanel(debugger, debuggerCommands, font);
        }
        else
        {
            debugger.Attach(spectrum);
        }

        Raylib.SetWindowSize((Ula.FrameWidth * DebugScale) + DebuggerPanel.ColumnWidth, (Ula.FrameHeight * Scale) + MenuBar.Height);
    }
    else
    {
        debugger!.Resume();
        debugger.Detach();
        Raylib.SetWindowSize(Ula.FrameWidth * Scale, (Ula.FrameHeight * Scale) + MenuBar.Height);
    }
}

// A Debug menu command: shows the debugger first if needed.
void Debug(Action<Debugger> command)
{
    if (!showDebugger)
    {
        ToggleDebugger();
    }

    command(debugger!);
}

// Every new machine goes through here, so that the debugger follows it with its breakpoints.
void SetMachine(Spectrum machine)
{
    spectrum = machine;
    if (showDebugger)
    {
        debugger!.Attach(machine);
    }
}

// Reads the .sym file next to an opened file, if there is one.
void LoadSymbols(string path)
{
    var symbolFile = Path.ChangeExtension(path, ".sym");
    symbols = File.Exists(symbolFile) ? SymbolTable.Parse(File.ReadAllText(symbolFile)) : new SymbolTable();
    if (debuggerCommands is not null)
    {
        debuggerCommands.Symbols = symbols;
    }
}

void Reload()
{
    if (lastOpened is null)
    {
        Report("nothing to reload: open a file first");
        return;
    }

    Open(lastOpened);
}

// Typing LOAD "" or playing the tape: what turbo speeds up.
bool IsLoading() => spectrum.AutoTyper.IsBusy || spectrum.Tape.IsPlaying;

void ChooseFile()
{
    if (!fileChooser.IsOpen && !fileChooser.TryOpen(Directory.Exists(snapshotFolder) ? snapshotFolder : documents))
    {
        Report("no file chooser on this system: drop a file on the window instead");
    }
}

void SwitchModel(bool to128)
{
    is128 = to128;
    Reset();
}

void ToggleFastLoad()
{
    fastLoad = !fastLoad;
    spectrum.FastLoad = fastLoad;
    Report(fastLoad ? "fast tape loading" : "real-time tape loading");
}

void ToggleTurbo()
{
    turbo = !turbo;
    Report(turbo ? "turbo while loading" : "normal speed while loading");
}

bool CanOpen(string path) => Snapshot.IsSupported(path) || IsTape(path);

bool IsTape(string path) => Path.GetExtension(path).Equals(".tap", StringComparison.OrdinalIgnoreCase);

void Open(string path)
{
    lastOpened = path;
    LoadSymbols(path);
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
        SetMachine(machine);
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
        // The snapshot says which model it needs; the machine switches to it.
        var data = File.ReadAllBytes(path);
        var needs128 = Snapshot.IsSpectrum128(path, data);
        Spectrum loaded = needs128
            ? new Spectrum128(rom128.Item1, rom128.Item2) { FastLoad = fastLoad }
            : new Spectrum48(rom48) { FastLoad = fastLoad };
        Snapshot.Load(loaded, path, data);
        SetMachine(loaded);
        is128 = needs128;
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
        File.WriteAllBytes(path, SnaFormat.Save(spectrum));
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
    SetMachine(NewMachine());
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
