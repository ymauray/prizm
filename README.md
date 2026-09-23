# iSpectrum

Émulateur ZX Spectrum 48K écrit en C# (.NET 10), pensé d'abord pour macOS sur Apple Silicon.
Il démarre la ROM d'origine, on y tape du BASIC au clavier du Mac, et il charge les snapshots
`.SNA` et `.Z80` ; le son et les cassettes viendront ensuite.

L'architecture, l'état d'avancement et le plan par jalons sont dans [`OVERVIEW.md`](OVERVIEW.md) ;
les règles de contribution (y compris pour les agents de code) dans [`AGENTS.md`](AGENTS.md).

## Lancer

```sh
dotnet run --project src/iSpectrum.App
```

## Clavier

Les caractères sont traduits : tapez-les comme d'habitude sur votre clavier (`"`, `+`, `:`…),
quelle que soit sa disposition, et l'émulateur appuie sur les touches Spectrum qui les donnent.

| Mac | Spectrum |
|---|---|
| Maj seule | Caps Shift |
| Ctrl seul | Symbol Shift |
| Ctrl + touche | combinaison brute, touches lues par position (QWERTY) |
| Entrée | ENTER |
| Retour arrière | DELETE |
| Échap | BREAK |
| Flèches | curseurs |
| Tab | mode étendu (Caps Shift + Symbol Shift) |
| F1 … F9 | Caps Shift + 1 … 9 (F1 EDIT, F2 CAPS LOCK, F9 GRAPHICS) |

Sur Mac, les touches F1 à F9 demandent Fn, sauf si elles sont réglées comme touches de fonction
standard dans les réglages du clavier. Les caractères du mode étendu (`[ ] { } ~ | \ ©`) se
tapent comme sur le Spectrum : Tab, puis Ctrl + la touche. Pour quitter, fermez la fenêtre.

## Snapshots

L'émulateur charge les snapshots `.SNA` et `.Z80` (48K) :

- glisser-déposer du fichier sur la fenêtre ;
- **Cmd+O** : sélecteur de fichier (macOS ; `zenity` sous Linux) ;
- en argument : `dotnet run --project src/iSpectrum.App -- jeu.z80`.

**Cmd+S** sauvegarde l'état de la machine en `.SNA` dans `~/Documents/iSpectrum/`, et
**Cmd+F** ouvre ce dossier dans le Finder. Les jeux ne doivent jamais être ajoutés au dépôt :
rangez vos fichiers personnels dans `local/`, ignoré par Git.

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
