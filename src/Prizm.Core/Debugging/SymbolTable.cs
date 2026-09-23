// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

using System.Globalization;
using System.Text.RegularExpressions;

namespace Prizm.Core.Debugging;

/// <summary>
/// Names for addresses, read from an assembler's symbol file, and the reverse. Also reads the
/// addresses typed in the debugger: a number or a name, plus or minus an offset.
/// </summary>
/// <remarks>
/// Accepted lines: <c>name: EQU 0x00008000</c> (sjasmplus <c>--sym</c>), <c>name EQU 8000h</c>
/// (pasmo and others), <c>name = $8000</c> (z88dk <c>.map</c>); anything else is skipped. Numbers
/// may be written 0x8000, $8000, #8000, 8000h or in decimal; only the low 16 bits are kept.
/// Names are matched without regard to case.
/// </remarks>
public sealed partial class SymbolTable
{
    private readonly Dictionary<string, ushort> _addresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, string> _names = [];

    public int Count => _addresses.Count;

    public static SymbolTable Parse(string text)
    {
        var table = new SymbolTable();
        foreach (var line in text.Split('\n'))
        {
            var match = SymbolLine().Match(line);
            if (match.Success && TryParseNumber(match.Groups["value"].Value, out var address))
            {
                table.Add(match.Groups["name"].Value, address);
            }
        }

        return table;
    }

    public void Add(string name, ushort address)
    {
        _addresses[name] = address;

        // A label beats a local label (with a dot) for the same address; otherwise the first wins.
        if (!_names.TryGetValue(address, out var existing) || (existing.Contains('.') && !name.Contains('.')))
        {
            _names[address] = name;
        }
    }

    /// <summary>The name to show for an address, or null.</summary>
    public string? NameAt(ushort address) => _names.GetValueOrDefault(address);

    public bool TryGetAddress(string name, out ushort address) => _addresses.TryGetValue(name, out address);

    /// <summary>
    /// Reads an address typed by the user: a number, or a symbol, optionally followed by + or -
    /// and a number (<c>start+3</c>, <c>$9000</c>, <c>score-1</c>).
    /// </summary>
    public bool TryParseAddress(string text, out ushort address)
    {
        address = 0;
        var match = AddressExpression().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var baseText = match.Groups["base"].Value;
        if (!TryParseNumber(baseText, out var value) && !TryGetAddress(baseText, out value))
        {
            return false;
        }

        if (match.Groups["offset"].Success)
        {
            if (!TryParseNumber(match.Groups["offset"].Value, out var offset))
            {
                return false;
            }

            value = (ushort)(match.Groups["sign"].Value == "-" ? value - offset : value + offset);
        }

        address = value;
        return true;
    }

    /// <summary>0x8000, $8000, #8000, 8000h or 32768; the low 16 bits of the value.</summary>
    public static bool TryParseNumber(string text, out ushort value)
    {
        value = 0;
        text = text.Trim();
        string digits;
        var style = NumberStyles.AllowHexSpecifier;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = text[2..];
        }
        else if (text.StartsWith('$') || text.StartsWith('#'))
        {
            digits = text[1..];
        }
        else if (text.EndsWith('h') || text.EndsWith('H'))
        {
            digits = text[..^1];
        }
        else
        {
            digits = text;
            style = NumberStyles.None;
        }

        if (digits.Length == 0 || !long.TryParse(digits, style, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        value = (ushort)number;
        return true;
    }

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z_.@?][\w.@?!]*)\s*:?\s*(?:EQU\b|=)\s*(?<value>[$#]?[0-9A-Fa-fxXhH]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SymbolLine();

    [GeneratedRegex(@"^(?<base>[$#]?[\w.@?!]+)(?:\s*(?<sign>[+-])\s*(?<offset>[$#]?[0-9A-Fa-fxXhH]+))?$")]
    private static partial Regex AddressExpression();
}
