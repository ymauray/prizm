# AGENTS.md — Consignes pour les agents de code

Ce fichier s'adresse à tout agent de code (Claude Code, Codex, etc.) travaillant sur **iSpectrum**.
Lire aussi **`OVERVIEW.md`** : architecture, détails techniques de la machine et plan par jalons.

## Projet

- Émulateur **ZX Spectrum** (48K d'abord, puis 128K) en **C#**.
- Projet personnel / open source.
- **Cible prioritaire : macOS Apple Silicon (`osx-arm64`)**. Linux et Windows sont un bonus :
  ne pas les casser volontairement, mais ne pas bloquer dessus.

## Environnement

- **.NET 10** (`net10.0`), SDK installé sur le Mac du développeur.
- Front-end : **Raylib-cs** (NuGet).
- Tests : **xUnit**.
- Gestion de version : Git.

## Architecture — règles à respecter

- `src/iSpectrum.Z80` : CPU Z80 **pur**. Aucune dépendance hormis la BCL. Accès au monde
  extérieur uniquement via `IMemory` et `IIo`.
- `src/iSpectrum.Core` : machine Spectrum (mémoire, ULA, clavier, beeper, formats de fichiers).
  **Aucune dépendance graphique ou audio.** Expose un framebuffer, un buffer audio et une API clavier.
- `src/iSpectrum.App` : seul projet autorisé à référencer Raylib-cs (fenêtre, rendu, audio, entrées).
- Les projets de tests ne référencent que `Z80` et `Core`, jamais `App`.

## Conventions de code

- Code, identifiants et commentaires **en anglais** ; documentation (`*.md`) en **français**.
- `Nullable` activé, `TreatWarningsAsErrors` activé, style C# standard (.editorconfig si ajouté).
- **Boucle chaude (CPU, ULA, rendu) : zéro allocation** par instruction ou par frame.
  Mémoire en `byte[]`, `Span<T>` pour les buffers, pas de LINQ, pas de boxing, pas de `events`.
- Préférer la clarté et la justesse à la micro-optimisation ; optimiser seulement après mesure.
- Le timing compte : chaque instruction Z80 doit incrémenter les **T-states exacts**.

## Tests — obligatoires

- Toute modification du Z80 doit garder au vert :
  - la suite **FUSE** (`tests.in` / `tests.expected`) ;
  - **ZEXDOC** (et idéalement **ZEXALL**) via le harnais CP/M.
- ZEXDOC et ZEXALL sont marqués `Category=Slow` : chacun exécute environ 47 milliards de T-states
  (40 s en Release, 4 min en Debug). Run rapide : `dotnet test --filter "Category!=Slow"`.
- Ajouter des tests unitaires pour : adressage écran, décodage des attributs, matrice clavier,
  chargeurs `.SNA` / `.Z80` / `.TAP`.
- Commande de référence : `dotnet test -c Release` à la racine. Si un test échoue, le relancer
  en Debug (`dotnet test --filter ...`) pour le déboguer.

## Façon de travailler

- Avancer **jalon par jalon** (voir `OVERVIEW.md` §7) ; ne pas commencer un jalon avant que
  le précédent soit fonctionnel et testé.
- Petits commits cohérents, un sujet par commit, message clair.
- Avant de terminer une tâche : `dotnet build` sans avertissement et `dotnet test -c Release` au vert.
- En cas de doute sur un comportement matériel, se référer à FUSE, au Sinclair Wiki et à
  *The Undocumented Z80 Documented* ; documenter le choix dans un commentaire.
- Mettre à jour `OVERVIEW.md` si l'architecture ou le plan changent.

## ROM et fichiers tiers

- ROM 48K Sinclair/Amstrad dans `roms/` : distribution autorisée avec un émulateur, copyright
  Amstrad à mentionner (README, fenêtre « À propos »), pas de vente des ROM.
- Fichiers de tests tiers (FUSE, ZEXDOC/ZEXALL) : conserver leurs licences et leur provenance
  dans `tests/.../README.md`.
- Ne jamais committer de jeux commerciaux ; les fichiers de test personnels vont dans un dossier
  ignoré par Git (ex. `local/`).
