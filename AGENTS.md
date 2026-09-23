# AGENTS.md — Consignes pour les agents de code

Ce fichier s'adresse à tout agent de code (Claude Code, Codex, etc.) travaillant sur **Prizm**.
Lire aussi **`OVERVIEW.md`** : état d'avancement (§0), architecture, détails techniques de la
machine et plan par jalons.

## Projet

- Émulateur **ZX Spectrum** (48K et 128K) en **C#**.
- Projet personnel / open source, sous licence **GPL-2.0-or-later** (`LICENSE`), sauf la ROM.
- **Cible prioritaire : macOS Apple Silicon (`osx-arm64`)**. Linux et Windows sont un bonus :
  ne pas les casser volontairement, mais ne pas bloquer dessus.

## Environnement

- **.NET 10** (`net10.0`), SDK installé sur le Mac du développeur.
- Front-end : **Raylib-cs** (NuGet).
- Tests : **xUnit**.
- Gestion de version : Git.

## Architecture — règles à respecter

- `src/Prizm.Z80` : CPU Z80 **pur**. Aucune dépendance hormis la BCL. Accès au monde
  extérieur uniquement via `IMemory` et `IIo`.
- `src/Prizm.Core` : machine Spectrum (mémoire, ULA, clavier, beeper, formats de fichiers).
  **Aucune dépendance graphique ou audio.** Expose un framebuffer, un buffer audio et une API clavier.
- `src/Prizm.App` : seul projet autorisé à référencer Raylib-cs (fenêtre, rendu, audio, entrées).
- Les projets de tests ne référencent que `Z80` et `Core`, jamais `App`.

## Conventions de code

- Code, identifiants et commentaires **en anglais** ; documentation (`*.md`) en **français**.
- Chaque fichier `.cs` commence par l'en-tête de licence :
  `// SPDX-License-Identifier: GPL-2.0-or-later` puis
  `// Copyright (C) 2026 The Prizm contributors`.
- N'ajouter une dépendance que si sa licence est compatible avec la GPL v2 (MIT, BSD, zlib…).
  Apache 2.0 n'est compatible qu'avec la GPL v3 : réservé aux outils qui ne sont pas distribués
  avec le programme (xUnit, par exemple).
- `Nullable` activé, `TreatWarningsAsErrors` activé, style C# standard (.editorconfig si ajouté).
- **Boucle chaude (CPU, ULA, rendu) : zéro allocation** par instruction ou par frame.
  Mémoire en `byte[]`, `Span<T>` pour les buffers, pas de LINQ, pas de boxing, pas de `events`.
- Préférer la clarté et la justesse à la micro-optimisation ; optimiser seulement après mesure.
- Le timing compte : chaque instruction Z80 doit incrémenter les **T-states exacts**.

## Tests — obligatoires

- Toute modification du Z80 doit garder au vert :
  - la suite **FUSE** (`tests.in` / `tests.expected`) ;
  - **ZEXDOC** et **ZEXALL** via le harnais CP/M (les deux passent depuis le jalon 1) ;
  - les six programmes de **z80test** (`tests/Prizm.Core.Tests/Z80Test`).
- ZEXDOC et ZEXALL sont marqués `Category=Slow` : chacun exécute environ 47 milliards de T-states
  (environ 50 s en Release, plusieurs minutes en Debug). Run rapide : `dotnet test --filter "Category!=Slow"`.
- Ajouter des tests unitaires pour : adressage écran, décodage des attributs, matrice clavier,
  chargeurs `.SNA` / `.Z80` / `.TAP`.
- Commande de référence : `dotnet test -c Release` à la racine. Si un test échoue, le relancer
  en Debug (`dotnet test --filter ...`) pour le déboguer.

## Façon de travailler

- Avancer **jalon par jalon** (voir `OVERVIEW.md` §7) ; ne pas commencer un jalon avant que
  le précédent soit fonctionnel et testé.
- Une branche par jalon (par exemple `jalon-8-128k`), fusionnée dans `main` à la fin du jalon
  avec un commit de fusion (`git merge --no-ff`) ; le tag `jalon-N` est posé sur `main`. La
  branche du jalon peut ensuite être supprimée (`git branch -d`) : le tag et le commit de fusion
  gardent son historique.
- Petits commits cohérents, un sujet par commit, message clair.
- Avant de terminer une tâche : `dotnet build` sans avertissement et `dotnet test -c Release` au vert.
- En cas de doute sur un comportement matériel, se référer à FUSE, au Sinclair Wiki et à
  *The Undocumented Z80 Documented* ; documenter le choix dans un commentaire.
- Mettre à jour `OVERVIEW.md` si l'architecture ou le plan changent, et son §0 (état
  d'avancement) à la fin de chaque jalon. Chaque jalon terminé reçoit un tag annoté `jalon-N`.

## ROM et fichiers tiers

- ROM 48K Sinclair/Amstrad dans `roms/` : redistribution autorisée par Amstrad, qui garde le
  copyright (à mentionner : README, fenêtre « À propos ») ; interdiction de vendre les ROM ou de
  les intégrer dans du matériel. Texte exact dans `roms/README.md`.
- Fichiers de tests tiers (FUSE, ZEXDOC/ZEXALL, z80test, zxtests) : conserver leurs licences et leur
  provenance dans `tests/.../README.md`. Le dossier `local/` est réservé aux tests de timing de
  Richard Butler (`timing_tests_48k_v1.0.z80` et ses 4 écrans `.scr` dans `local/timing-tests/`,
  obtenus depuis <https://github.com/MrKWatkins/EmulatorTestSuites> ou <https://www.zxspectrum4.net/op_timing.php>)
  dont la licence n'est pas formellement déclarée en open source. Les tests qui en dépendent sont
  alors ignorés s'il manque (voir `tests/Prizm.Core.Tests/ThirdParty/LocalFile.cs` et `README.md`).
- Ne jamais committer de jeux commerciaux ni de fichiers sous copyright.
