# GitHub Copilot Instructions for Prizm

Consignes essentielles pour les assistants de code (voir le document complet et normatif dans [`AGENTS.md`](../AGENTS.md) et [`OVERVIEW.md`](../OVERVIEW.md)) :

- **Architecture en couches strictes** :
  - `src/Prizm.Z80` : CPU Z80 pur, aucune dépendance hormis la BCL, accès via `IMemory` et `IIo`.
  - `src/Prizm.Core` : machine ZX Spectrum, aucune dépendance graphique ou audio directe.
  - `src/Prizm.App` : seul projet autorisé à référencer Raylib-cs.
  - Les tests ne référencent que `Prizm.Z80` et `Prizm.Core`.
- **Conventions de code** :
  - Code C#, identifiants et commentaires en **anglais**. Documentation (`*.md`) en **français**.
  - En-tête de licence sur chaque fichier `.cs` :
    ```csharp
    // SPDX-License-Identifier: GPL-2.0-or-later
    // Copyright (C) 2026 The Prizm contributors
    ```
- **Performance** :
  - Boucle chaude (CPU, ULA, audio, rendu) : **zéro allocation** par instruction ou par frame (Span, tableaux préalloués, pas de LINQ ni boxing).
  - Incrémentation exacte des T-states pour chaque instruction Z80.
- **Tests** :
  - Commande de référence : `dotnet test -c Release`.
  - Ne jamais casser les suites FUSE, ZEXDOC, ZEXALL ni z80test.
