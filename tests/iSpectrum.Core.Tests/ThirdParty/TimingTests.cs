// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

using iSpectrum.Core.Snapshots;

namespace iSpectrum.Core.Tests.ThirdParty;

/// <summary>
/// Richard Butler's 48K timing tests (zxspectrum4.net): each test times a group of instructions,
/// in uncontended or contended memory, against an interrupt, and compares three measures (R, a
/// loop counter, SP) with the values the program holds for real machines. The harness follows
/// MrKWatkins/EmulatorTestSuites. Tests 35 to 37 time port reads against the floating bus, so
/// they start with a prepared screen (from the same place) in the screen memory.
/// </summary>
/// <remarks>
/// The program is not in the repository (licence to check): put timing_tests_48k_v1.0.z80, from
/// https://github.com/MrKWatkins/EmulatorTestSuites (src/MrKWatkins.EmulatorTestSuites.ZXSpectrum/Timing)
/// or https://www.zxspectrum4.net/op_timing.php, in local/timing-tests/, and the four .scr files
/// of that folder's Screens/ subfolder in local/timing-tests/screens/.
/// </remarks>
public class TimingTests
{
    private const string ProgramFile = "timing-tests/timing_tests_48k_v1.0.z80";
    private const string Source = "tests/iSpectrum.Core.Tests/ThirdParty/TimingTests.cs";

    private const ushort UsrRoutine = 0x34B6;
    private const ushort TestCode = 0xC000;
    private const ushort Stop = 0xBC28;
    private const ushort TestNumber = 40000;
    private const ushort ContendedFlag = 40002;
    private const ushort Results = 0xEF00;
    private const ushort ExpectedResults = 0xE200;
    private const int LateTimingOffset = 512;
    private const int MaxSteps = 5_000_000;

    public static TheoryData<int, bool> Tests()
    {
        var data = new TheoryData<int, bool>();
        foreach (var contended in new[] { false, true })
        {
            for (var test = 1; test <= 34; test++)
            {
                data.Add(test, contended);
            }
        }

        // The floating-bus variants of the original BASIC program.
        data.Add(35, false);
        data.Add(35, true);
        data.Add(36, true);
        data.Add(37, true);
        return data;
    }

    [LocalFileTheory(ProgramFile, Source)]
    [MemberData(nameof(Tests))]
    public void Timing(int test, bool contended)
    {
        // Test 0 tells early-timing machines (R = 2) from late-timing ones (R = 122).
        var detection = Run(0, false, out _);
        Assert.True(detection is (2, 0, 49478) or (122, 0, 49478), $"Timing type not recognised: {detection}");
        var late = detection.R == 122;

        var actual = Run(test, contended, out var spectrum);

        Assert.Equal(Expected(spectrum, test, contended, late), actual);
    }

    /// <summary>
    /// Loads the program, enters the test code through the ROM's USR routine (RANDOMIZE USR 49152),
    /// then runs from the start of a frame, with the machine's interrupts, up to the stop address.
    /// </summary>
    private static (byte R, ushort Loop, ushort Sp) Run(int test, bool contended, out Spectrum48 spectrum)
    {
        spectrum = new Spectrum48(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", "48.rom")))
        {
            Headless = true,
        };
        Z80Format.Load(spectrum, File.ReadAllBytes(LocalFile.Path(ProgramFile)));

        var cpu = spectrum.Cpu;
        cpu.SP = 0xFFFE;
        spectrum.Memory.Write(TestNumber, (byte)test);
        spectrum.Memory.Write(ContendedFlag, contended ? (byte)1 : (byte)0);
        cpu.PC = UsrRoutine;
        (cpu.B, cpu.C) = (TestCode >> 8, TestCode & 0xFF);

        while (cpu.PC != TestCode)
        {
            cpu.Step();
        }

        if (test >= 35)
        {
            var screen = File.ReadAllBytes(LocalFile.Path($"timing-tests/screens/{test}-{(contended ? "contended" : "uncontended")}.scr"));
            spectrum.Memory.LoadRam(0x4000, screen);
        }

        cpu.TStates = 0;
        for (var step = 0; cpu.PC != Stop; step++)
        {
            Assert.True(step < MaxSteps, $"The test did not reach 0x{Stop:X4}; PC = 0x{cpu.PC:X4}.");
            spectrum.Step();
        }

        return Read(spectrum, Results);
    }

    private static (byte R, ushort Loop, ushort Sp) Expected(Spectrum48 spectrum, int test, bool contended, bool late) =>
        Read(spectrum, (ushort)(ExpectedResults + (test * 10) + (contended ? 5 : 0) + (late ? LateTimingOffset : 0)));

    private static (byte R, ushort Loop, ushort Sp) Read(Spectrum48 spectrum, ushort address)
    {
        var memory = spectrum.Memory;
        ushort Word(int offset) =>
            (ushort)(memory.Read((ushort)(address + offset)) | (memory.Read((ushort)(address + offset + 1)) << 8));
        return (memory.Read(address), Word(1), Word(3));
    }
}
