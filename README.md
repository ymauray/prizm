# iSpectrum

Émulateur ZX Spectrum **48K** et **128K** écrit en C# (.NET 10), pensé d'abord pour macOS sur
Apple Silicon. Il démarre les ROM d'origine, on y tape du BASIC au clavier du Mac, le beeper et
la puce son AY du 128K se font entendre, et il charge snapshots (`.SNA`, `.Z80`) et cassettes
(`.TAP`, `.TZX`), en temps réel ou instantanément, y compris les chargeurs turbo et protégés.

L'architecture, l'état d'avancement et le plan par jalons sont dans [`OVERVIEW.md`](OVERVIEW.md) ;
les règles de contribution (y compris pour les agents de code) dans [`AGENTS.md`](AGENTS.md).

## Lancer

```sh
dotnet run --project src/iSpectrum.App
```

## Menus

Une barre de menu, en haut de la fenêtre, réunit toutes les commandes avec leur raccourci :

| Menu | Commandes |
|---|---|
| **File** | Open... (Cmd+O), Save snapshot (Cmd+S), Show saved files (Cmd+F), Quit (Cmd+Q) |
| **Machine** | ZX Spectrum 48K (Cmd+1), ZX Spectrum 128K (Cmd+2), Reset (Cmd+R) |
| **Tape** | Insert tape... (Cmd+Shift+O), Play / Stop (Cmd+Shift+P), Rewind, New recording, Fast loading (Cmd+L), Turbo while loading (Cmd+T) |
| **Debug** | Show debugger (Cmd+D), Pause / Continue (Cmd+P), Step into (Cmd+I), Step over (Cmd+N), Step out (Cmd+U) |
| **Help** | About iSpectrum (licence et copyright des ROM) |

**File > Reload** (Cmd+Shift+R) rouvre le dernier fichier ouvert.

Les coches indiquent le modèle en cours et les options actives. Les libellés sont en anglais :
la police intégrée de Raylib n'a pas de lettres accentuées.

## Modèles

La machine démarre en 128K. **Cmd+1** allume un 48K, **Cmd+2** un 128K (qui démarre sur son
menu : les flèches choisissent, Entrée valide), **Cmd+R** redémarre la machine en cours. Dans le
BASIC 128, les mots-clés se tapent en toutes lettres : `PLAY "cdefgab"` fait jouer la puce AY.

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
tapent aussi comme les autres : l'émulateur passe lui-même en mode étendu, puis appuie sur
Symbol Shift et la touche. On peut toujours faire comme sur le Spectrum : Tab, puis Ctrl + la
touche. Pour quitter, fermez la fenêtre.

## Snapshots et cassettes

L'émulateur ouvre les snapshots `.SNA` et `.Z80` (48K et 128K) et les cassettes `.TAP` et
`.TZX` :

- glisser-déposer du fichier sur la fenêtre ;
- **Cmd+O** : sélecteur de fichier (macOS ; `zenity` sous Linux) ;
- en argument : `dotnet run --project src/iSpectrum.App -- jeu.tap`.

Une cassette redémarre la machine, qui tape `LOAD ""` d'elle-même (sur le 128K, elle choisit
« Tape Loader » dans le menu). Par défaut, elle se charge **en temps réel**, comme en 1983 : son de chargement, bandes de couleur dans la bordure, et
quelques minutes d'attente pour un gros jeu. **Turbo while loading** (Cmd+T) joue la cassette en accéléré, sans le son ; **Fast loading**
(Cmd+L) la court-circuite et copie chaque bloc en mémoire (instantané, sans signal, mais
seulement pour les blocs que la ROM charge à sa vitesse). Le titre de la fenêtre
indique le bloc de la cassette en cours de lecture. Un snapshot, lui, se charge dans le
modèle qu'il indique.

Une cassette `.TZX` décrit aussi les chargeurs turbo et protégés (Speedlock, Alkatraz…) : leurs
blocs se chargent toujours en temps réel, même en chargement rapide, et **Turbo while loading**
les accélère. La cassette démarre d'elle-même quand la ROM ou un chargeur écoute le signal, et
s'arrête aux blocs qui le demandent (entre deux niveaux, par exemple). Si un programme attend
qu'on relance la cassette sans l'écouter, **Tape > Play / Stop** (Cmd+Shift+P) la relance ;
**Tape > Rewind** la rembobine. Quand une cassette `.TZX` propose un menu (face A ou B, choix
du niveau…), elle s'arrête et l'affiche : les flèches et Entrée, un chiffre ou un clic
choisissent, Échap continue au bloc suivant. Pour un jeu en plusieurs faces ou cassettes, **Tape > Insert
tape...** (Cmd+Shift+O) met une autre cassette dans le lecteur sans redémarrer la machine,
contrairement à **File > Open**.

**Cmd+S** sauvegarde l'état de la machine en `.SNA` dans `~/Documents/iSpectrum/`, et
**Cmd+F** ouvre ce dossier dans le Finder.

`SAVE` enregistre dans ce même dossier, instantanément, dans un `.TAP` nommé d'après le
programme (`SAVE "jeu"` crée `jeu.tap`). Comme sur une cassette laissée en enregistrement, les
`SAVE` suivants s'ajoutent au même fichier ; **Tape > New recording** fait commencer le
prochain dans un nouveau fichier. Un programme qui appelle la routine `SA-BYTES` de la ROM
(`0x04C2`) est enregistré de la même façon. Ne jamais committer de jeux commerciaux ou de
fichiers sous copyright dans le dépôt.

## Écrire ses programmes

iSpectrum est pensé pour développer en assembleur Z80 avec
[sjasmplus](https://github.com/z00m128/sjasmplus). L'exemple `examples/hello.asm` affiche un
message par la ROM :

```sh
mkdir -p build && cd build
sjasmplus --sym=hello.sym ../examples/hello.asm
```

sjasmplus écrit dans le dossier courant `hello.sna` (un snapshot, pour développer : il se charge
instantanément et démarre sur `start`), `hello.tap` (une cassette, pour partager ; elle contient
presque toute la RAM et se charge lentement en temps réel) et `hello.sym` (les symboles).
Ouvrez `hello.sna` : le fichier `hello.sym` voisin est chargé avec lui. Après chaque
modification, réassemblez puis faites **Cmd+Shift+R** : le programme est rechargé, en gardant
les points d'arrêt.

## Débogueur

**Cmd+D** affiche le débogueur : l'image passe en taille double, avec la mémoire et une ligne
de commande en dessous, et à droite l'état, les registres, les drapeaux, le désassemblage (avec
les symboles) et les points d'arrêt, dans la police du Spectrum. Un clic sur une ligne du
désassemblage pose ou retire un point d'arrêt (`*`). Pendant une pause, l'écran montre la
mémoire telle qu'elle est à cet instant, et le clavier va à la ligne de commande :

| Commande | Effet |
|---|---|
| `s`, `n`, `u`, `c`, `p` | pas à pas, pas par-dessus (CALL, RST, DJNZ, LDIR…), sortie de routine, reprise, pause |
| `b ADRESSE` | pose ou retire un point d'arrêt |
| `w ADRESSE` | arrête après toute écriture à cette adresse |
| `pr PORT`, `pw PORT` | arrête après une lecture ou une écriture de port (`$FE` : tout port pair de l'ULA) |
| `clear` | retire tous les points d'arrêt et surveillances |
| `m ADRESSE`, `d [ADRESSE]` | vue mémoire ; désassemblage à partir d'une adresse (seul : suit PC) |

Une adresse s'écrit `$8000`, `0x8000`, `8000h`, `32768`, ou avec un symbole : `start`,
`start.next`, `score+1`. Le désassembleur ne sait pas distinguer le code des données : les
octets qui suivent une étiquette de données (comme `message`) s'affichent comme des
instructions ; la vue mémoire les montre tels quels.

## Tester

```sh
dotnet test -c Release                   # tout, environ 50 s
dotnet test --filter "Category!=Slow"    # sans ZEXDOC ni ZEXALL, quelques secondes
```

### Tests de timing (Richard Butler)

Les 72 tests de timing de Richard Butler (`TimingTests.cs`) vérifient la durée d'instructions en
mémoire contendue et le bus flottant. Faute de licence open source formellement déclarée, leurs
fichiers ne sont pas committés dans le dépôt (les tests sont automatiquement ignorés s'ils sont absents).

Pour les exécuter, téléchargez `timing_tests_48k_v1.0.z80` et les quatre fichiers `.scr` associés depuis
[EmulatorTestSuites](https://github.com/MrKWatkins/EmulatorTestSuites/tree/main/src/MrKWatkins.EmulatorTestSuites.ZXSpectrum/Timing)
ou [zxspectrum4.net](https://www.zxspectrum4.net/op_timing.php), puis déposez-les dans `local/` :

```sh
mkdir -p local/timing-tests/screens
# Déposez timing_tests_48k_v1.0.z80 dans local/timing-tests/
# Déposez 35-contended.scr, 35-uncontended.scr, 36-contended.scr, 37-contended.scr dans local/timing-tests/screens/
```

## ROM et fichiers tiers

La ROM du Spectrum 48K (`roms/48.rom`) est © 1982 Sinclair Research Ltd, droits détenus par
Amstrad, et n'est pas couverte par la licence du projet. Amstrad autorise sa redistribution,
mais pas sa vente ni son intégration dans du matériel. Voir [`roms/README.md`](roms/README.md).

Les fichiers de test FUSE, ZEXDOC/ZEXALL, z80test et zxtests sont sous licence libre (GPL ou MIT) ;
leur provenance est décrite dans le `README.md` de leurs dossiers respectifs. Les tests de timing de
Richard Butler se déposent dans `local/timing-tests/` (voir section *Tester* ci-dessus).

## Licence

Copyright © 2026 les contributeurs d'iSpectrum.

iSpectrum est un logiciel libre, distribué sous licence **GNU GPL version 2 ou ultérieure**
(`GPL-2.0-or-later`) : vous pouvez le redistribuer et le modifier selon les termes de la GPL,
version 2 ou (à votre choix) toute version ultérieure. Il est fourni sans aucune garantie.
Texte complet dans [`LICENSE`](LICENSE).

La ROM (`roms/`) n'est pas couverte par cette licence (voir plus haut).
