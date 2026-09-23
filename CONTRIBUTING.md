# Contribuer à Prizm

Merci de vous intéresser à Prizm ! Ce document résume les consignes et conventions pour contribuer au projet. Pour une description détaillée de l'architecture et de la feuille de route, consultez [`OVERVIEW.md`](OVERVIEW.md) et [`AGENTS.md`](AGENTS.md).

## Principes généraux

- **Langue** : le code, les identifiants, les commentaires et les messages de commit sont rédigés en **anglais**. La documentation (`*.md`) est en **français**.
- **Licence** : Prizm est distribué sous licence **GPL-2.0-or-later**. Chaque nouveau fichier `.cs` doit comporter l'en-tête de licence standard :
  ```csharp
  // SPDX-License-Identifier: GPL-2.0-or-later
  // Copyright (C) 2026 The Prizm contributors
  ```
- **Dépendances** : toute nouvelle dépendance doit avoir une licence compatible avec la GPL v2 (MIT, BSD, zlib…).
- **Copyright** : ne jamais committer de ROMs protégées ou de jeux commerciaux sous copyright dans le dépôt.

## Architecture

Le projet est découpé en trois couches strictes :

1. `src/Prizm.Z80` : CPU Z80 pur. Aucune dépendance hormis la bibliothèque standard .NET (BCL). Accès mémoire et entrées/sorties exclusivement via `IMemory` et `IIo`.
2. `src/Prizm.Core` : logique de la machine ZX Spectrum (mémoire paginée, ULA, contention, clavier, beeper, formats SNA, Z80, TAP, TZX). **Aucune dépendance graphique ou audio directe.** Expose des tampons bruts (framebuffer, audio, matrice clavier).
3. `src/Prizm.App` : frontal utilisateur et boucle multimédia (Raylib-cs, fenêtre, rendu, audio, entrées, menus).
4. `tests/` : suites de tests unitaires et d'intégration, référençant uniquement `Prizm.Z80` et `Prizm.Core`.

## Performance et boucle chaude

Sur la boucle chaude (exécution CPU, génération vidéo ULA, rendu par frame) :
- **Zéro allocation par instruction ou par frame**.
- Utiliser des tableaux préalloués (`byte[]`), des `Span<T>` et `ReadOnlySpan<T>`.
- Éviter LINQ, le boxing et les événements C# (`event`) dans le chemin critique d'émulation.
- Respecter scrupuleusement le timing matériel (T-states exacts par instruction et par cycle mémoire).

## Tests obligatoires

Avant de soumettre une pull request, vérifiez que l'ensemble de la suite de tests est au vert :

```sh
# Tests rapides (exclut les suites ZEXDOC et ZEXALL de 50 s)
dotnet test --filter "Category!=Slow"

# Suite complète en Release
dotnet test -c Release
```

Toute modification touchant le processeur Z80 ou la machine doit préserver le succès des suites :
- Tests de FUSE (`tests.in` / `tests.expected`)
- Harnais CP/M pour ZEXDOC et ZEXALL
- Les six suites de conformité `z80test`

## Proposer une contribution

1. **Discussions & Issues** : pour une nouvelle fonctionnalité ou un changement majeur, commencez par ouvrir une discussion ou une issue pour échanger avec le mainteneur.
2. **Branche** : créez une branche dédiée à votre correctif ou fonctionnalité (ex. : `feature/turbo-tape` ou `fix/contention-timing`).
3. **Commits** : effectuez des commits atomiques avec des messages clairs et concis en anglais.
4. **Pull Request** : ouvrez une Pull Request vers la branche `main` en remplissant le template de PR fourni.
