# iSpectrum

Émulateur ZX Spectrum 48K écrit en C# (.NET 10), pensé d'abord pour macOS sur Apple Silicon.
Il démarre la ROM d'origine jusqu'à l'écran « © 1982 Sinclair Research Ltd » ; le clavier,
le son et le chargement de programmes viendront ensuite.

L'architecture, l'état d'avancement et le plan par jalons sont dans [`OVERVIEW.md`](OVERVIEW.md) ;
les règles de contribution (y compris pour les agents de code) dans [`AGENTS.md`](AGENTS.md).

## Lancer

```sh
dotnet run --project src/iSpectrum.App
```

## Tester

```sh
dotnet test -c Release                   # tout, environ 40 s
dotnet test --filter "Category!=Slow"    # sans ZEXDOC ni ZEXALL, quelques secondes
```

## ROM et fichiers tiers

La ROM du Spectrum 48K (`roms/48.rom`) est © 1982 Sinclair Research Ltd, droits détenus par
Amstrad, et n'est pas couverte par la licence du projet. Amstrad autorise sa redistribution,
mais pas sa vente ni son intégration dans du matériel. Voir [`roms/README.md`](roms/README.md).

Les fichiers de test FUSE et ZEXDOC/ZEXALL sont sous licence GNU GPL v2 ou ultérieure ; leur
provenance est décrite dans le `README.md` de leurs dossiers respectifs.

## Licence

Copyright © 2026 les contributeurs d'iSpectrum.

iSpectrum est un logiciel libre, distribué sous licence **GNU GPL version 2 ou ultérieure**
(`GPL-2.0-or-later`) : vous pouvez le redistribuer et le modifier selon les termes de la GPL,
version 2 ou (à votre choix) toute version ultérieure. Il est fourni sans aucune garantie.
Texte complet dans [`LICENSE`](LICENSE).

La ROM (`roms/`) n'est pas couverte par cette licence (voir plus haut).
