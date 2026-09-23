# iSpectrum — Émulateur ZX Spectrum en C#

Projet personnel / open source d'émulateur ZX Spectrum écrit en C# (.NET), sous licence
GPL-2.0-or-later (voir `LICENSE`), sauf la ROM (voir §8).

- **Plateforme cible prioritaire** : macOS (Apple Silicon, `osx-arm64`)
- **Plateformes secondaires** : Linux et Windows, si ça fonctionne « gratuitement » grâce à .NET et aux bibliothèques choisies
- **Machine émulée en priorité** : ZX Spectrum 48K, puis 128K

---

## 0. État d'avancement

**Jalons 1 à 5 terminés** (tags Git `jalon-1` à `jalon-5`) : le Spectrum 48K démarre la ROM
d'origine dans une fenêtre, on y tape du BASIC au clavier du Mac, il charge et sauvegarde des
snapshots, et le beeper se fait entendre. Un vrai jeu de 1983 (*Miner*, de David Hembrow,
librement redistribuable) se joue avec le son. Il n'y a pas encore de chargement de cassettes.

Ce qui existe :

- `src/iSpectrum.Z80` : CPU complet — toutes les instructions, préfixes `CB`, `ED`, `DD`, `FD`,
  `DDCB`, `FDCB`, opcodes non documentés, bits 3 et 5 des drapeaux, MEMPTR ; interruptions
  IM 0/1/2, `HALT`, retard après `EI`.
- `src/iSpectrum.Core` : `Memory48K` (ROM protégée en écriture), `Ula` (port `0xFE` : clavier
  en lecture, bordure et haut-parleur en écriture ; rendu de l'écran avec BRIGHT et FLASH), `Keyboard` et
  `SpectrumKey` (matrice 8 demi-rangées × 5 touches), `Beeper` (échantillons audio de chaque
  frame), `Spectrum48` (frame de 69 888 T-states,
  interruption tant que INT est maintenue), `ScreenLayout`, `Palette`.
- `src/iSpectrum.App` : fenêtre Raylib-cs 960×768 (image ×3, sans filtrage) ; `KeyboardInput`
  traduit le clavier du Mac en matrice Spectrum ; `AudioOutput` joue le beeper et donne la
  cadence de l'émulation.
- `SpectrumCharacters` (Core) : quelles touches Spectrum tapent un caractère donné.
- `src/iSpectrum.Core/Snapshots` : `SnaFormat` (chargement et sauvegarde `.SNA` 48K),
  `Z80Format` (chargement `.Z80` versions 1 à 3, 48K), `Snapshot` (choix d'après l'extension).
- App : chargement en argument, par glisser-déposer ou Cmd+O (sélecteur natif via `osascript`,
  `zenity` sous Linux) ; Cmd+S sauvegarde un `.SNA` dans `~/Documents/iSpectrum`, Cmd+F ouvre
  ce dossier. Messages dans le titre de la fenêtre (pas encore de texte à l'écran).
- Tests : suite FUSE (1356 tests), ZEXDOC et ZEXALL (catégorie `Slow`), interruptions,
  adressage écran, attributs, bordure, protection de la ROM, matrice clavier, et deux tests sur
  la vraie ROM qui relisent l'écran en comparant chaque case à la police de la ROM : le message
  de copyright au démarrage, puis `PRINT 2+2` et `PRINT "A+B=C"` tapés au clavier.
- Snapshots : fichiers fabriqués dans le code (aucun jeu dans le dépôt) — en-têtes écrits à la
  main d'après la description des formats, cas limites de la compression, et un programme BASIC
  sauvegardé puis rechargé dans une machine neuve pour chaque format.
- Beeper : silence, nombre d'échantillons sur 50 frames, moyenne dans un échantillon, et
  `BEEP 1,0` joué par la vraie ROM, dont la hauteur (do, 261,6 Hz) est vérifiée.

Choix de comportement déjà faits (détaillés en commentaire dans le code) :

- `SCF` / `CCF` : bits 3 et 5 pris dans `A | F`, comme FUSE (l'effet du registre interne « Q »
  n'est pas émulé).
- Instructions de bloc répétées (`LDIR`, `INIR`…) : drapeaux comme FUSE, sans les effets liés
  à PC décrits en 2018.
- `OUT (C),0` envoie 0 (Z80 NMOS du Spectrum).
- Pendant l'acquittement d'une interruption, le bus de données lit `0xFF` : IM 0 se comporte
  comme IM 1 (`RST 38h`), IM 2 lit son vecteur en `I × 256 + 0xFF`.
- `Interrupt()` renvoie `false` si l'interruption est refusée ; `Spectrum48.RunFrame` la
  redemande tant que INT est maintenue (32 premiers T-states de la frame).
- Image de 320×256 : écran 256×192 et bordure de 32 pixels de chaque côté. La bordure réelle
  est plus haute et asymétrique ; ce sera à revoir avec le rendu ligne par ligne.
- L'image entière est rendue en fin de frame à partir de la mémoire (pas encore ligne par ligne).
- Palette : 0xD7 par composante pour les couleurs normales, 0xFF pour les couleurs vives.
- Port `0xFE` en lecture : bits 0-4 = demi-rangées choisies par les lignes A8-A15 à 0 ; bits
  5 et 7 à 1 ; bit 6 (EAR) à 1 tant que l'entrée cassette n'existe pas.
- Clavier du Mac **traduit par caractère**, quelle que soit la disposition : le système dit
  quel caractère une touche a tapé (`"` = Maj+2 sur un clavier suisse), et l'App tient
  enfoncées les touches Spectrum correspondantes (Symbol Shift + P) tant que la touche du Mac
  l'est. Maj seule = Caps Shift ; Ctrl seul = Symbol Shift ; avec Ctrl enfoncé, les touches
  sont lues par position (Raylib nomme les touches d'après le QWERTY américain, qui est aussi
  la disposition du Spectrum). Option est laissée au système (elle tape `@`, `#`…). Touches
  spéciales : voir `README.md`.
- La RAM démarre à zéro.
- `.SNA` : PC est sur la pile (chargement = RETN) ; la sauvegarde l'y pousse dans la copie de la
  RAM sans modifier la machine, et échoue si SP ne laisse pas de place en RAM.
- `.Z80` : le compteur de T-states des fichiers v3 est ignoré, la frame repart à 0 au chargement
  (comme pour `.SNA`) ; les snapshots 128K et les autres machines sont refusés avec un message.
- Son : seul le bit 4 du port `0xFE` pilote le haut-parleur (le bit 3, MIC, est ignoré).
  Échantillons 16 bits mono à 44 100 Hz, chacun égal au niveau moyen pendant sa durée (environ
  79 T-states), puis filtre passe-haut à un pôle contre la composante continue.
- Cadence : c'est la carte son qui rythme l'émulation. La boucle se redessine avec la
  synchronisation verticale (plafonnée à 120 Hz) et exécute des frames tant que moins de 2048
  échantillons attendent dans le tampon circulaire (plus 2 × 1024 dans le flux Raylib, soit
  environ 90 ms de latence). Sans périphérique audio, repli sur 50 images/s.
- Clavier : les événements sont lus à chaque passage de la boucle (Raylib les efface au
  suivant), et une touche traduite ou spéciale reste enfoncée jusqu'à ce qu'une frame l'ait vue.
- L'App charge un snapshot dans une machine neuve et ne remplace la machine en cours qu'en cas
  de succès : un fichier invalide ne laisse jamais une machine à moitié chargée.

Reste en suspens :

- Les événements de bus de FUSE (`MR`, `MW`, `MC`, `PR`, `PW`, `PC`) sont lus mais pas comparés.
  Il faudra horodater chaque accès, ce qui ira avec la contention (jalon 7).
- NMI non implémentée (inutile sur un Spectrum sans interface).
- Latence audio d'environ 90 ms : à réduire si elle gêne.
- Les caractères du mode étendu (`[ ] { } ~ | \ ©`) ne sont pas traduits : il faudrait
  enchaîner deux combinaisons. Les touches mortes (`^`, `¨`) et la frappe très rapide n'ont été
  essayées qu'à la main.
- Pas encore de fenêtre « À propos » : le copyright Amstrad n'est mentionné que dans
  `README.md` et `roms/README.md`.

**Prochaine étape : jalon 6** — cassettes `.TAP` en chargement rapide : interception de la
routine ROM `LD-BYTES` (`0x0556`) pour injecter les blocs directement, tests du chargeur ;
ouverture des `.tap` dans l'App (glisser-déposer, Cmd+O) suivie de `LOAD ""`.

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
├── Directory.Build.props     # net10.0, Nullable, TreatWarningsAsErrors pour tous les projets
├── iSpectrum.sln
├── src/
│   ├── iSpectrum.Z80/        # CPU Z80 pur, sans dépendance (IMemory, IIo)
│   ├── iSpectrum.Core/       # Machine : mémoire, ULA, clavier, son, frame, snapshots (cassettes à venir)
│   └── iSpectrum.App/        # Front-end : fenêtre, rendu, clavier, son, fichiers (Raylib-cs)
├── tests/
│   ├── iSpectrum.Z80.Tests/
│   │   ├── Fuse/             # Suite FUSE + parseur et runner (provenance dans README.md)
│   │   └── Zex/              # ZEXDOC/ZEXALL + harnais CP/M (provenance dans README.md)
│   └── iSpectrum.Core.Tests/ # Écran, attributs, mémoire, clavier, son, ROM, SNA/Z80 (TAP à venir)
├── local/                    # Ignoré par Git : fichiers de test personnels (jeux…)
├── README.md
└── roms/
    ├── README.md             # Provenance et copyright Amstrad
    └── 48.rom                # ROM Sinclair/Amstrad (voir §8)
```

Principe : **le cœur (Z80 + Core) ne dépend d'aucune bibliothèque graphique**. Le front-end
ne fait que lire un framebuffer, un buffer audio, et envoyer l'état du clavier. On pourra
donc changer de front-end (Avalonia, MonoGame…) sans toucher à l'émulation.

### Interfaces de base

```csharp
public interface IMemory { byte Read(ushort addr); void Write(ushort addr, byte value); }
public interface IIo     { byte In(ushort port);   void Out(ushort port, byte value); }

public sealed partial class Z80Cpu
{
    public byte A, F, B, C, D, E, H, L;
    public byte A_, F_, B_, C_, D_, E_, H_, L_;   // jeu alternatif A', F'…
    public ushort IX, IY, SP, PC;
    public ushort WZ;                             // MEMPTR (registre interne)
    public byte I, R;
    public bool IFF1, IFF2;
    public int IM;
    public bool Halted;
    public long TStates;

    public Z80Cpu(IMemory memory, IIo io);
    public void Reset();      // état à la mise sous tension (AF = SP = 0xFFFF, comme FUSE)
    public void Step();       // fetch – decode – execute d'une instruction
    public bool Interrupt();  // INT masquable ; false si refusée (DI, juste après EI)
}
```

### Boucle principale

```csharp
public void RunFrame()
{
    while (cpu.TStates < 69888) cpu.Step();
    cpu.TStates -= 69888;
    cpu.Interrupt();          // 50 Hz ; à réessayer tant que INT est maintenue si elle est refusée
    ula.RenderFrame(memory);  // framebuffer 320×256 (écran + bordure)
    beeper.EndFrame();        // buffer audio de la frame
}
```

Le front-end appelle `RunFrame()` puis se synchronise à 50 Hz (idéalement sur l'audio).

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

Timing : chaque accès au bus ajoute la durée de son cycle machine (lecture d'opcode 4 T-states,
lecture ou écriture mémoire 3, port 4) ; seuls les cycles internes sont ajoutés à la main
(`Internal(n)`). La durée d'une instruction est donc la somme de ses cycles.

Préfixes `DD`/`FD` : ils choisissent IX ou IY pour l'opcode suivant, et le décodeur sans
préfixe passe par `IndexRegister` au lieu de HL (H et L deviennent IXH/IXL, `(HL)` devient
`(IX+d)`), plutôt que de dupliquer le décodeur.

### Tests (dès le début)

- **Suite de tests FUSE** (`tests.in` / `tests.expected`) : chaque instruction, registres,
  MEMPTR, timings et mémoire (un test xUnit par cas FUSE).
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
3. **`.TAP`** — interception de la routine ROM `LD-BYTES` (`0x0556`) et injection directe des blocs (« flash loading »).
4. **`.TZX`** — émulation réelle du signal cassette, nécessaire pour les chargeurs protégés/turbo.

---

## 6. Front-end et plateforme (macOS en priorité)

- **.NET 10** (`net10.0`), cible `osx-arm64` (et `osx-x64` si besoin).
- **Front-end v1 : Raylib-cs**
  - fournit les bibliothèques natives macOS / Linux / Windows via NuGet ;
  - une texture 320×256 mise à jour à chaque frame, mise à l'échelle ×2 / ×3 ;
  - flux audio intégré (`AudioStream`) pour le beeper ;
  - gestion clavier et glisser-déposer de fichiers.
- **Option ultérieure : Avalonia** (`WriteableBitmap`) si l'on veut une vraie application Mac
  avec menus natifs, dialogues d'ouverture, préférences. Audio alors via OpenAL (Silk.NET).
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
| 6 | `.TAP` (flash loading) | Chargement des cassettes courantes |
| 7 | Contention mémoire + rendu ligne par ligne | Démos et effets de bordure corrects |
| 8 | Modèle 128K | Pagination (port `0x7FFD`) + puce son AY-3-8912 |
| 9 | Débogueur intégré | Désassembleur, points d'arrêt, vue mémoire/registres |
| 10 | `.TZX` | Chargeurs protégés / turbo |

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
