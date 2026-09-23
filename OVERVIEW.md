# iSpectrum — Émulateur ZX Spectrum en C#

Projet personnel / open source d'émulateur ZX Spectrum écrit en C# (.NET), sous licence
GPL-2.0-or-later (voir `LICENSE`), sauf la ROM (voir §8).

- **Plateforme cible prioritaire** : macOS (Apple Silicon, `osx-arm64`)
- **Plateformes secondaires** : Linux et Windows, si ça fonctionne « gratuitement » grâce à .NET et aux bibliothèques choisies
- **Machine émulée en priorité** : ZX Spectrum 48K, puis 128K

---

## 0. État d'avancement

**Jalons 1 à 11 terminés** (tags Git `jalon-1` à `jalon-11`) : iSpectrum émule le ZX Spectrum
**48K** et le **128K** (mémoire paginée, puce son AY-3-8912), avec un **débogueur intégré**
pensé pour développer en assembleur (sjasmplus, symboles, rechargement en une touche). On y tape du BASIC au clavier du
Mac, le son sort, il charge et sauvegarde des snapshots `.SNA` et `.Z80`, charge des cassettes
`.TAP` et `.TZX` en temps réel (son et bandes dans la bordure) ou instantanément, chargeurs
turbo et protégés compris (essayés : Speedlock 1, 2, 4 et 7, Alkatraz), et reproduit la
contention mémoire et le bus flottant de l'ULA, avec une image dessinée au fil du faisceau. Deux
jeux librement redistribuables de David Hembrow tournent : *Miner* (1983, `.z80`) et *Corona-V*
(2020, `.tap`).

### Ce qui existe

- `src/iSpectrum.Z80` : CPU complet — toutes les instructions, préfixes `CB`, `ED`, `DD`, `FD`,
  `DDCB`, `FDCB`, opcodes non documentés, bits 3 et 5 des drapeaux, MEMPTR, registre interne
  « Q » ; interruptions IM 0/1/2, `HALT`, retard après `EI` ; un point de contention à chaque
  cycle de bus (`IMemory.ContentionDelay`, `IMemory.IsContended`, `IIo.ContentionDelay`, 0 par
  défaut) ; `Z80Disassembler` (toutes les instructions, symboles).
- `src/iSpectrum.Core`, la machine :
  - `Spectrum` (base commune : CPU, ULA, cassette, boucle de frame, `Step`, chargement rapide,
    mode `Headless`), `Spectrum48`, `Spectrum128` ;
  - `SpectrumTimings` (horloge, frame, ligne, premier pixel, table de contention, par modèle) ;
  - `SpectrumMemory` (vue du CPU et banque affichée par l'ULA), `Memory48K`, `Memory128K` ;
  - `Ula` (port `0xFE` : clavier, EAR, bordure, haut-parleur ; bus flottant ; image) ;
  - `Beeper` (mélangeur : haut-parleur, cassette, AY), `ISoundSource`, `Ay8912` ;
  - `Keyboard`, `SpectrumKey`, `SpectrumCharacters`, `AutoTyper`, `ScreenLayout`, `Palette` ;
  - `Tape/` : `TapFile`, `TzxFile` (tous les blocs de TZX 1.20), `TapeImage` et `TapeBlock`
    (la cassette, quel que soit le format), `TapeCursor` (parcours front par front : boucles,
    sauts, appels), `TapePlayer` (le lecteur, et la détection de chargeur), `TapeRecorder`
    (les blocs sauvegardés par la ROM) ; `Snapshots/` :
    `SnaFormat`, `Z80Format`, `Snapshot` ;
  - `Debugging/` : `Debugger` (pause, pas à pas, points d'arrêt et surveillances),
    `SymbolTable` (fichiers de symboles), `DebuggerCommands` (ligne de commande).
- `src/iSpectrum.App` : fenêtre Raylib-cs (image ×3 sous une barre de menu) ; `MenuBar` (menus
  File, Machine, Tape, Debug, Help dessinés dans la fenêtre ; menus et raccourcis partagent une
  seule liste de commandes), `DebuggerPanel` et `SpectrumFont` (le débogueur, dans la police de
  la ROM), `AboutBox` (licence et copyright des ROM, dans la même police), `KeyboardInput`
  (clavier du Mac traduit, mode étendu compris), `AudioOutput` (son, et cadence de
  l'émulation), `HostShell` (sélecteur de fichier, Finder). Messages dans le titre de la fenêtre.
- `examples/hello.asm` : premier programme pour sjasmplus (snapshot, cassette et symboles).

### Tests

- CPU : suite FUSE (1356 tests, événements de bus compris), ZEXDOC, ZEXALL et les six programmes
  de z80test (catégorie `Slow`), interruptions ; désassembleur (57 textes attendus, et 1792
  opcodes dont la longueur est vérifiée en les exécutant sur le CPU).
- Débogueur : pas à pas, pas par-dessus, sortie de routine, points d'arrêt, surveillances
  mémoire et port, changement de machine ; symboles lus dans un vrai fichier de sjasmplus ;
  ligne de commande.
- Machine : adressage écran, attributs, bordure au fil du faisceau, contention, bus flottant,
  mémoire 48K et 128K (pagination, verrou, banques contendues), clavier, beeper, AY (registres,
  hauteur, enveloppes), cassette (fronts du signal, blocs TZX d'arrêt, détection de
  chargeur), snapshots des deux modèles, mode sans affichage.
- TZX : les fichiers de test de libspectrum (GPL), dont `complete-tzx.tzx` (un bloc de presque
  chaque type), comparés front par front aux listes que libspectrum attend ; fichiers
  corrompus rejetés, boucles et sauts qui finissent.
- Sur les vraies ROM, en relisant l'écran avec la police de la ROM : démarrage du 48K et du
  menu 128K, `PRINT` tapé au clavier (caractères du mode étendu compris, en 48K et dans
  l'éditeur du BASIC 128), `BEEP 1,0` (hauteur vérifiée), `PLAY "c"` dans le BASIC
  128, `LOAD ""` depuis le signal et en mode rapide (48K, et « Tape Loader » du 128K), depuis
  un `.TZX` (blocs d'information, bloc d'arrêt, bloc turbo joué en temps réel en mode rapide),
  un programme BASIC qui survit à une sauvegarde puis un chargement dans chaque format ;
  `SAVE` (en-tête et données identiques à ceux attendus, relus sur une autre machine, dans le
  BASIC 48 et le BASIC 128) et `SA-BYTES` appelé depuis du code machine.
- Timing (Richard Butler) : 72 tests de durée d'instructions et de bus flottant, comparés aux
  mesures sur machine réelle ; ignorés si `local/timing-tests/` est absent.

### Choix de comportement (détaillés en commentaire dans le code)

CPU :

- `SCF` / `CCF` : bits 3 et 5 pris dans `(Q xor F) or A` (Z80 Zilog). Q vaut F si l'instruction
  précédente a écrit les drapeaux, 0 sinon ; `POP AF` et `EX AF,AF'` laissent Q à 0.
- Instructions de bloc interrompues pendant une répétition : bits 5 et 3 de F pris dans les bits
  13 et 11 de PC ; pour les entrées-sorties, H et P/V recalculés et MEMPTR = PC + 1 (David Banks,
  2018, comme MAME). Cinq cas FUSE, antérieurs, suivent ici la machine réelle.
- `OUT (C),0` envoie 0 (Z80 NMOS). Pendant l'acquittement d'une interruption le bus lit `0xFF` :
  IM 0 se comporte comme IM 1, IM 2 lit son vecteur en `I × 256 + 0xFF`. L'acquittement n'est
  pas contendu ; un `JR` non pris ne lit pas son déplacement (comme FUSE).
- `Interrupt()` renvoie `false` si l'interruption est refusée ; la machine la redemande tant que
  INT est maintenue (32 T-states sur le 48K, 36 sur le 128K).

Timings et ULA :

- 48K : 3,5 MHz, frame de 69 888 T-states, lignes de 224, premier pixel à 14 336, premier cycle
  contendu à 14 335. 128K : 3,5469 MHz, 70 908, 228, 14 362 et 14 361 (d'après FUSE, dont le bus
  flottant commence 26 T-states plus tard sur le 128K). Machines « early timing ».
- Contention : motif 6, 5, 4, 3, 2, 1, 0, 0 par groupe de 8 T-states, sur les 128 premiers
  T-states des 192 lignes d'écran ; RAM `0x4000`-`0x7FFF` sur le 48K, banques impaires sur le
  128K. Points de contention du CPU et motif des entrées-sorties comme FUSE.
- Bus flottant (ports impairs) : pixels en +3, attribut en +4, paire suivante en +5 et +6 dans
  chaque groupe de 8 T-states d'une ligne, `0xFF` ailleurs ; le Z80 capture la donnée à la fin
  du cycle d'entrée-sortie, 3 T-states après l'appel à `In` (seul décalage qui satisfait les
  tests 35 à 37 de Richard Butler).
- Image : 320×256 (écran 256×192 et bordure de 32 pixels). Changements de bordure horodatés ;
  zone d'écran dessinée juste avant chaque écriture dans l'écran affiché, et le reste en fin de
  frame. Approximations : bordure au pixel près (l'ULA procède par 8 pixels), bordure réelle
  plus haute et asymétrique. Palette : 0xD7 (normal) et 0xFF (vif).
- Port `0xFE` en lecture : demi-rangées du clavier sur les bits 0-4, bits 5 et 7 à 1, bit 6
  (EAR) = signal de la cassette quand elle joue, sinon bit 4 du dernier `OUT` (carte « issue 3 »).
- La RAM démarre à zéro.

128K :

- Port `0x7FFD` décodé sur A15 = 0 et A1 = 0 ; bits 0-2 banque en `0xC000`, bit 3 écran fantôme
  (banque 7), bit 4 ROM, bit 5 verrou jusqu'au reset. L'image rattrape le faisceau avant un
  changement d'écran. Non émulé : la lecture du port `0x7FFD`, qui sur le vrai 128K y écrit le
  bus flottant.
- AY : ports `0xFFFD` (A15 = A14 = 1, A1 = 0 ; sélection et relecture) et `0xBFFD` (A15 = 1,
  A14 = 0, A1 = 0 ; écriture). Tics de 16 T-states (8 cycles AY), bruit et enveloppe un tic sur
  deux ; volumes d'après la table mesurée d'ayumi (MIT). Registres 14-15 (ports d'E/S : RS-232,
  pavé numérique) non reliés.
- Chargement de cassette : « Tape Loader » du menu (ENTRÉE après 150 frames) ; les interceptions
  de `LD-BYTES` et de `SA-BYTES` ne s'appliquent que quand la ROM 1 est paginée.
- Sauvegarde sur cassette toujours instantanée, comme le piège de FUSE : `SA-BYTES` est
  intercepté à `SA-FLAG` (`0x04D0`, drapeau dans A, début dans IX, longueur dans DE), le bloc
  complet part dans `TapeRecorder`, et la ROM reprend au `RET` de `0x053E`. L'App ajoute chaque
  bloc à un `.TAP` de `~/Documents/iSpectrum/` nommé d'après le premier en-tête, comme une
  cassette restée en enregistrement ; **Tape > New recording** en commence une autre.

Débogueur :

- Il fait tourner la machine à la place de l'App (`Debugger.RunFrame`), instruction par
  instruction. Un point d'arrêt arrête avant l'instruction ; une surveillance (écriture mémoire,
  même en ROM ; accès à un port selon un masque) arrête après, en disant quelle instruction a
  fait l'accès. Pas par-dessus et sortie de routine ne bloquent pas : ils posent un arrêt
  temporaire et laissent tourner la machine, qui reste affichée.
- Les « crochets » de surveillance (mémoire, ports) ne coûtent qu'un test de référence nulle
  quand aucun débogueur n'est attaché. `Spectrum.Step` indique la fin d'une frame.
- Pendant une pause, l'image est redessinée depuis la mémoire courante (`Ula.DrawNow`), sans
  tenir compte du faisceau.
- Le désassemblage part de PC (ou d'une adresse choisie) : remonter en arrière dans du code
  machine est ambigu. Le code et les données ne se distinguent pas.
- Symboles : lignes `nom: EQU 0x…` (sjasmplus), `nom EQU …h`, `nom = $…` (z88dk) ; un symbole
  sans point l'emporte sur une étiquette locale pour la même adresse. Le fichier `.sym` voisin
  du fichier ouvert est chargé automatiquement.

Son et App :

- Mélange dans `Beeper` : haut-parleur (bit 4 du port `0xFE`, MIC ignoré), cassette à mi-volume,
  AY ; chaque échantillon (44 100 Hz, 16 bits mono) est le niveau moyen pendant sa durée, puis
  passe-haut à un pôle. Toutes les sources sont amenées au même T-state avant chaque événement.
- Cadence donnée par la carte son, qui puise elle-même dans un anneau de 1024 échantillons
  d'avance (callback Raylib, environ 30 ms de latence) ; affichage à la synchronisation
  verticale ; repli sur 50 images/s sans audio.
- Clavier du Mac traduit par caractère (Maj+2 = `"` sur un clavier suisse) ; Maj seule = Caps
  Shift, Ctrl seul = Symbol Shift, Ctrl + touche = par position ; événements lus à chaque
  passage de la boucle, touche tenue jusqu'à ce qu'une frame l'ait vue.
- Snapshots : `.SNA` 48K avec PC sur la pile ; `.SNA` 128K avec PC à part et la banque paginée
  répétée si c'est la 2 ou la 5 ; `.Z80` versions 1 à 3, compteur de T-states ignoré. Un fichier
  dit lui-même le modèle qu'il lui faut (`Snapshot.IsSpectrum128`) ; il est chargé dans une
  machine neuve, qui ne remplace la machine en cours qu'en cas de succès.
- Cassette réelle : durées de la ROM (pilote 2168 T × 8063 ou 3223, synchronisation 667 + 735,
  bits 2 × 855 ou 2 × 1710, pause 1 s pour un `.TAP`). Le signal suit le modèle de libspectrum :
  chaque événement est une transition (bascule, aucune, bas, haut) puis une durée ; un bloc
  qui suit une pause commence au niveau bas ; la bande finit par un front, qui termine la
  dernière impulsion, et ce niveau tient 1 ms avant l'arrêt (sans lui, Speedlock 4 rate le
  dernier bit d'*Out Run*). Durées TZX et CSW en T-states à 3,5 MHz, jouées telles quelles sur
  le 128K, comme FUSE ; le bloc « stop si 48K » ne compte que sur le modèle 48K.
- Démarrage de la cassette : quand la ROM lit le signal (`LD-SAMPLE`, `0x05ED`), ou quand un
  programme lit le port `0xFE` comme un chargeur (FUSE : 10 lectures à moins de 500 T-states
  d'intervalle, B augmentant ou diminuant de 1). Elle ne s'arrête d'elle-même qu'aux blocs
  d'arrêt et à la fin : FUSE l'arrête aussi quand les lectures cessent, mais reconnaît pour cela
  les boucles de nombreux chargeurs, que nous n'avons pas.
- Chargement rapide : interception à `0x056B` et saut à `0x05DF` avec H = XOR des octets (FUSE),
  pour les blocs à la vitesse de la ROM (bloc TZX `0x10`, ou `0x11` aux durées de bits de la
  ROM), en passant les blocs sans son. Tout autre bloc est joué en temps réel.
  Turbo : frames sans image ni son pendant 12 ms, puis une frame dessinée.
- Performances : environ 24 fois la vitesse réelle en jeu avec image et son, 46 fois sans.

### Reste en suspens

- Tests visuels de l'ULA (`btime`, `stime`, `ulatest3`) : licence et références à trouver.
- TZX : bloc « select » (`0x28`) ignoré ; les blocs CSW et « generalized data » sont décodés
  en mémoire à l'ouverture.

Souhaitable un jour, sans échéance :

- les autres modèles : **+2A/+3** et **Pentagon 128**, entre autres ;
- la **Beta Disk** (disquettes TR-DOS `.TRD` et `.SCL`), le format de la plupart des démos de la
  scène. Il faudrait le timing du Pentagon (frame de 71 680 T-states, sans contention), le
  contrôleur WD1793, et la ROM TR-DOS, qu'on ne peut pas fournir faute de licence de
  redistribution claire : l'utilisateur la mettrait dans `local/` ;
- les formats de cassette **`.CSW`** (signal brut, déjà décodé dans le bloc TZX `0x18`) et
  **`.PZX`** ;
- l'**accélération des chargeurs** : FUSE raccourcit les boucles des chargeurs qu'il
  reconnaît ; ici, seul le turbo accélère ;
- le **`SAVE` sans la ROM** : capturer le signal MIC, pour enregistrer un programme qui
  sauvegarde avec sa propre routine (sans `SA-BYTES`) ;
- la **NMI** (saut en `0x0066`), qui n'a d'intérêt qu'avec une interface qui s'en sert, comme le
  Multiface.

**Prochaine étape** : le plan du §7 est terminé ; la suite reste à choisir, par exemple parmi
les points ci-dessus.

---

## 1. La machine à émuler (Spectrum 48K)

| Composant | Détails |
|---|---|
| CPU | Zilog Z80 à 3,5 MHz |
| ROM | 16 Ko, `0x0000–0x3FFF` |
| RAM | 48 Ko, `0x4000–0xFFFF` |
| Écran | Bitmap `0x4000–0x57FF` (6 144 octets) + attributs `0x5800–0x5AFF` (768 octets) |
| ULA | Vidéo, clavier, beeper, bordure, contention mémoire |
| Frame | 69 888 T-states, interruption à 50 Hz |

---

## 2. Architecture de la solution

```
iSpectrum/
├── AGENTS.md                 # Consignes pour les agents de code (CLAUDE.md y renvoie)
├── LICENSE                   # GNU GPL v2 (le projet est GPL-2.0-or-later)
├── OVERVIEW.md
├── README.md                 # Utilisation : menus, clavier, snapshots, cassettes, débogueur
├── Directory.Build.props     # net10.0, Nullable, TreatWarningsAsErrors pour tous les projets
├── iSpectrum.sln
├── src/
│   ├── iSpectrum.Z80/        # CPU Z80 pur (IMemory, IIo), désassembleur
│   ├── iSpectrum.Core/       # Machines 48K et 128K : mémoire, ULA, clavier, son, AY, frame
│   │   ├── Tape/             # .TAP, .TZX et lecteur de cassette
│   │   ├── Snapshots/        # .SNA et .Z80
│   │   └── Debugging/        # Débogueur, symboles, ligne de commande
│   └── iSpectrum.App/        # Front-end Raylib-cs : fenêtre, menus, clavier, son, débogueur
├── tests/
│   ├── iSpectrum.Z80.Tests/
│   │   ├── Fuse/             # Suite FUSE + parseur et runner (provenance dans README.md)
│   │   └── Zex/              # ZEXDOC/ZEXALL + harnais CP/M (provenance dans README.md)
│   └── iSpectrum.Core.Tests/
│       ├── Z80Test/          # z80test de Patrik Rak, MIT (provenance dans README.md)
│       ├── ThirdParty/       # Tests de timing de Richard Butler (fichiers dans local/)
│       ├── Tape/Libspectrum/ # Fichiers TZX de libspectrum, GPL (provenance dans README.md)
│       ├── Debugging/, Snapshots/, Tape/
│       └── *.cs              # Écran, ULA, contention, mémoire, clavier, son, AY, ROM…
├── examples/
│   └── hello.asm             # Premier programme pour sjasmplus
├── local/                    # Ignoré par Git : jeux, fichiers de test tiers, builds
└── roms/
    ├── README.md             # Provenance et copyright Amstrad
    ├── 48.rom                # ROM du 48K (Sinclair/Amstrad, voir §8)
    └── 128-0.rom, 128-1.rom  # ROM du 128K : éditeur et menu, BASIC 48K
```

Principe : **le cœur (Z80 + Core) ne dépend d'aucune bibliothèque graphique**. Le front-end
ne fait que lire un framebuffer, un buffer audio, et envoyer l'état du clavier. On pourra
donc changer de front-end (Avalonia, MonoGame…) sans toucher à l'émulation.

### Interfaces de base

```csharp
public interface IMemory
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
    int ContentionDelay(ushort address, long tStates) => 0;  // attente imposée par l'ULA
    bool IsContended(ushort address) => false;
}

public interface IIo
{
    byte In(ushort port);
    void Out(ushort port, byte value);
    int ContentionDelay(ushort port, long tStates) => 0;
}

public sealed partial class Z80Cpu
{
    public byte A, B, C, D, E, H, L;
    public byte F { get; set; }                   // propriété : note les écritures (registre Q)
    public byte A_, F_, B_, C_, D_, E_, H_, L_;   // jeu alternatif A', F'…
    public ushort IX, IY, SP, PC, WZ;             // WZ = MEMPTR
    public byte I, R;
    public bool IFF1, IFF2, Halted;
    public int IM;
    public long TStates;

    public Z80Cpu(IMemory memory, IIo io);
    public void Reset();      // état à la mise sous tension (AF = SP = 0xFFFF, comme FUSE)
    public void Step();       // une instruction, préfixes DD/FD compris
    public bool Interrupt();  // INT masquable ; false si refusée (DI, juste après EI)
}
```

### Boucle principale

`Spectrum` (base de `Spectrum48` et `Spectrum128`) exécute la machine instruction par
instruction ; `RunFrame()` répète `Step()` jusqu'à la fin d'une frame :

```csharp
public bool Step()                // true quand la frame se termine
{
    // Début de frame : échantillons du beeper, frappe automatique.
    // INT est maintenue pendant les 32 (48K) ou 36 (128K) premiers T-states :
    if (!interruptTaken && Cpu.TStates < Timings.InterruptLength)
        interruptTaken = Cpu.Interrupt();
    // Interceptions de LD-BYTES (cassette) si la ROM qui la contient est paginée.
    Cpu.Step();
    if (Cpu.TStates >= Timings.FrameTStates)
    {
        Ula.EndFrameSound(...);   // cassette et AY amenés au même T-state, fin du son
        Cpu.TStates -= Timings.FrameTStates;
        Ula.EndFrame(Memory.Screen);  // fin de l'image (dessinée au fil du faisceau)
        return true;
    }
    return false;
}
```

Le front-end exécute autant de frames que la carte son en demande, puis affiche la dernière
image ; avec le débogueur, c'est `Debugger.RunFrame()` qui fait tourner la machine.

---

## 3. Le CPU Z80 (le plus gros morceau, ~80 % du travail)

- Décodage par `switch` / tables d'opcodes, avec les préfixes `CB`, `ED`, `DD`, `FD`
  et les combinaisons `DDCB` / `FDCB`.
- S'appuyer sur *« Decoding Z80 Opcodes »* (Cristian Dinu) : décomposition des opcodes en
  champs x/y/z/p/q, ce qui évite d'écrire ~1 500 cas à la main.
- Flags : tables précalculées (parité, signe/zéro) ; émuler aussi les **flags non documentés**
  (bits 3 et 5, registre interne MEMPTR/WZ) — certains jeux et tous les tests en dépendent.
- Compter précisément les **T-states** de chaque instruction.
- Registre `R` (rafraîchissement), modes d'interruption IM 0/1/2, `HALT`, `EI` retardé.

Organisation du code (`src/iSpectrum.Z80`) :

| Fichier | Contenu |
|---|---|
| `Z80Cpu.cs` | Registres, `Reset`, `Interrupt`, accès au bus minutés, paires de registres |
| `Z80Cpu.Alu.cs` | Tables de drapeaux, opérations arithmétiques et logiques, `DAA` |
| `Z80Cpu.Opcodes.cs` | `Step`, gestion des préfixes `DD`/`FD`, opcodes sans préfixe |
| `Z80Cpu.Cb.cs` | Préfixe `CB`, et `DDCB`/`FDCB` |
| `Z80Cpu.Ed.cs` | Préfixe `ED`, dont les instructions de bloc |
| `Z80Disassembler.cs` | Désassembleur : texte et longueur de chaque instruction, symboles |

Timing : chaque accès au bus ajoute la durée de son cycle machine (lecture d'opcode 4 T-states,
lecture ou écriture mémoire 3, port 4) ; seuls les cycles internes sont ajoutés à la main
(`Internal(n)`). La durée d'une instruction est donc la somme de ses cycles.

Préfixes `DD`/`FD` : ils choisissent IX ou IY pour l'opcode suivant, et le décodeur sans
préfixe passe par `IndexRegister` au lieu de HL (H et L deviennent IXH/IXL, `(HL)` devient
`(IX+d)`), plutôt que de dupliquer le décodeur.

### Tests (dès le début)

- **Suite de tests FUSE** (`tests.in` / `tests.expected`) : chaque instruction, registres,
  MEMPTR, timings, mémoire et événements de bus (un test xUnit par cas FUSE).
- **ZEXDOC / ZEXALL** : exécutés dans un mini-harnais CP/M (interception de `CALL 5`).
  Chacun exécute environ 47 milliards de T-states, d'où la catégorie `Slow`.
- Intégrés dans un projet de tests xUnit. Commandes : voir `AGENTS.md`, section Tests.

---

## 4. L'ULA : vidéo, clavier, son, bordure

### Vidéo

- 256×192 pixels, entrelacement des lignes :

  ```csharp
  int addr = 0x4000 | ((y & 0xC0) << 5) | ((y & 0x07) << 8) | ((y & 0x38) << 2) | (x >> 3);
  ```

- Attributs : 1 octet par case 8×8 → ink (bits 0–2), paper (bits 3–5), bright (bit 6), flash (bit 7).
- Flash : inversion ink/paper toutes les 16 frames.
- Bordure autour de l'écran (framebuffer total ~320×256).
- Palette de 15 couleurs (8 normales + 7 « bright », le noir est identique).

### Clavier

- Port `0xFE` en lecture ; l'octet haut de l'adresse sélectionne une ou plusieurs
  demi-rangées (8 rangées × 5 touches), bits 0–4 = touches, **actives à 0**.
- Mapping clavier Mac → matrice Spectrum (Caps Shift, Symbol Shift, Entrée, Espace…).

### Beeper et bordure

- Port `0xFE` en écriture : bits 0–2 = couleur de bordure, bit 4 = haut-parleur (bit 3 = MIC).
- Beeper échantillonné selon les T-states → buffer audio (~44,1 kHz) par frame.

---

## 5. Chargement des programmes (par ordre de difficulté)

1. **`.SNA`** — snapshot brut (registres + RAM). Le plus simple, idéal pour tester des jeux vite.
2. **`.Z80`** — snapshot compressé (versions 1, 2 et 3), le format le plus répandu.
3. **`.TAP`** — signal de cassette reconstitué et lu par la ROM (temps réel, avec son et bandes), ou interception de la routine ROM `LD-BYTES` (`0x0556`) et injection directe des blocs (« flash loading »).
4. **`.TZX`** — émulation réelle du signal cassette, nécessaire pour les chargeurs protégés/turbo ; les blocs à la vitesse de la ROM restent interceptables.

---

## 6. Front-end et plateforme (macOS en priorité)

- **.NET 10** (`net10.0`), cible `osx-arm64` (et `osx-x64` si besoin).
- **Front-end v1 : Raylib-cs**
  - fournit les bibliothèques natives macOS / Linux / Windows via NuGet ;
  - une texture 320×256 mise à jour à chaque frame, mise à l'échelle ×2 / ×3 ;
  - flux audio intégré (`AudioStream`) pour le beeper et l'AY ;
  - gestion clavier et glisser-déposer de fichiers ;
  - barre de menu, débogueur et fenêtre « About » dessinés dans la fenêtre (police de Raylib
    pour les menus, en anglais faute d'accents ; police de la ROM du Spectrum pour le débogueur
    et la fenêtre « About »).
- Des menus natifs (Avalonia) ne sont plus prévus : la barre de menu dessinée convient.
- Packaging macOS : bundle `.app` (et plus tard signature/notarisation si distribution).
- Performances : éviter toute allocation dans la boucle chaude, mémoire en `byte[]`,
  `Span<byte>` pour le framebuffer. .NET est largement assez rapide.

---

## 7. Plan de travail (jalons)

| # | Jalon | Résultat attendu |
|---|---|---|
| 1 | CPU Z80 + harnais de tests | Tests FUSE et ZEXDOC/ZEXALL au vert — **terminé** (`jalon-1`) |
| 2 | Mémoire + ROM + affichage écran | Message « © 1982 Sinclair Research Ltd » à l'écran — **terminé** (`jalon-2`) |
| 3 | Clavier | On peut taper et exécuter du BASIC — **terminé** (`jalon-3`) |
| 4 | Chargement `.SNA` / `.Z80` | Les premiers jeux tournent — **terminé** (`jalon-4`) |
| 5 | Beeper | Le son fonctionne — **terminé** (`jalon-5`) |
| 6 | `.TAP` : signal réel et chargement rapide | Chargement des cassettes courantes, avec son et bandes — **terminé** (`jalon-6`) |
| 7 | Contention mémoire + rendu ligne par ligne | Démos et effets de bordure corrects — **terminé** (`jalon-7`), validation fine par des suites de test à venir |
| 8 | Modèle 128K | Pagination (port `0x7FFD`) + puce son AY-3-8912 — **terminé** (`jalon-8`) |
| 9 | Débogueur intégré | Désassembleur, points d'arrêt, vue mémoire/registres — **terminé** (`jalon-9`), avec symboles et rechargement |
| 10 | `.TZX` | Chargeurs protégés / turbo — **terminé** (`jalon-10`) : Speedlock 1, 2, 4, 7 et Alkatraz chargent |
| 11 | Finitions | Fenêtre « About » (licence, copyright Amstrad des ROM), caractères du mode étendu tapés au clavier du Mac — **terminé** (`jalon-11`) |

---

## 8. La ROM (licence)

Amstrad, détenteur des droits Sinclair depuis 1986, autorise la **redistribution** des ROM du
Spectrum (autorisation informelle, donnée sur Usenet), à condition que :

- le copyright reste celui d'Amstrad et soit mentionné (README / fenêtre « À propos ») ;
- les ROM ne soient ni vendues en tant que telles, ni intégrées dans du matériel. Un produit
  payant qui les contient reste permis si l'on fait payer le produit, pas les ROM.

La ROM n'est donc pas couverte par la licence du projet. Texte exact dans `roms/README.md`.

Cela convient pour un projet personnel ou open source gratuit.
Alternative entièrement libre : **OpenSE BASIC** (ROM de remplacement compatible, GPL).

---

## 9. Références

- *The Complete Spectrum ROM Disassembly* (Ian Logan & Frank O'Hara)
- *The Undocumented Z80 Documented* (Sean Young)
- *Decoding Z80 Opcodes* (Cristian Dinu)
- Sinclair Wiki / World of Spectrum : timings de l'ULA, contention, formats de fichiers
- Code source de **FUSE** (référence et suite de tests)
- ZEXDOC / ZEXALL (Frank D. Cringle)
