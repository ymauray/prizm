// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The iSpectrum contributors

namespace iSpectrum.Core;

/// <summary>
/// The 40 keys of the Spectrum. Each value encodes its place in the matrix: half-row in bits 3-5
/// (selected by address line A8 + row when reading port 0xFE), bit number in bits 0-2.
/// </summary>
public enum SpectrumKey
{
    CapsShift = (0 << 3) | 0, Z = (0 << 3) | 1, X = (0 << 3) | 2, C = (0 << 3) | 3, V = (0 << 3) | 4,
    A = (1 << 3) | 0, S = (1 << 3) | 1, D = (1 << 3) | 2, F = (1 << 3) | 3, G = (1 << 3) | 4,
    Q = (2 << 3) | 0, W = (2 << 3) | 1, E = (2 << 3) | 2, R = (2 << 3) | 3, T = (2 << 3) | 4,
    D1 = (3 << 3) | 0, D2 = (3 << 3) | 1, D3 = (3 << 3) | 2, D4 = (3 << 3) | 3, D5 = (3 << 3) | 4,
    D0 = (4 << 3) | 0, D9 = (4 << 3) | 1, D8 = (4 << 3) | 2, D7 = (4 << 3) | 3, D6 = (4 << 3) | 4,
    P = (5 << 3) | 0, O = (5 << 3) | 1, I = (5 << 3) | 2, U = (5 << 3) | 3, Y = (5 << 3) | 4,
    Enter = (6 << 3) | 0, L = (6 << 3) | 1, K = (6 << 3) | 2, J = (6 << 3) | 3, H = (6 << 3) | 4,
    Space = (7 << 3) | 0, SymbolShift = (7 << 3) | 1, M = (7 << 3) | 2, N = (7 << 3) | 3, B = (7 << 3) | 4,
}
