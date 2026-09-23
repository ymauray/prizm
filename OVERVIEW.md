# iSpectrum — Émulateur ZX Spectrum en C#

Projet personnel / open source d'émulateur ZX Spectrum écrit en C# (.NET).

- **Plateforme cible prioritaire** : macOS (Apple Silicon, `osx-arm64`)
- **Plateformes secondaires** : Linux et Windows, si ça fonctionne « gratuitement » grâce à .NET et aux bibliothèques choisies
- **Machine émulée en priorité** : ZX Spectrum 48K, puis 128K

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
├── OVERVIEW.md
├── iSpectrum.sln
├── src/
│   ├── iSpectrum.Z80/        # CPU Z80 pur, sans dépendance (IMemory, IIo)
│   ├── iSpectrum.Core/       # Machine : mémoire, ULA, clavier, beeper, formats de fichiers
│   └── iSpectrum.App/        # Front-end : fenêtre, rendu, audio, entrées (Raylib-cs)
├── tests/
│   ├── iSpectrum.Z80.Tests/  # Tests FUSE + ZEXDOC/ZEXALL (harnais CP/M)
│   └── iSpectrum.Core.Tests/ # Adressage écran, clavier, chargement SNA/Z80/TAP
└── roms/
    └── 48.rom                # ROM Sinclair/Amstrad (voir §8)
```

Principe : **le cœur (Z80 + Core) ne dépend d'aucune bibliothèque graphique**. Le front-end
ne fait que lire un framebuffer, un buffer audio, et envoyer l'état du clavier. On pourra
donc changer de front-end (Avalonia, MonoGame…) sans toucher à l'émulation.

### Interfaces de base

```csharp
public interface IMemory { byte Read(ushort addr); void Write(ushort addr, byte value); }
public interface IIo     { byte In(ushort port);   void Out(ushort port, byte value); }

public sealed class Z80Cpu
{
    public byte A, F, B, C, D, E, H, L;   // + jeu alternatif A', F', B', C', D', E', H', L'
    public ushort IX, IY, SP, PC;
    public byte I, R;
    public bool IFF1, IFF2;
    public int IM;
    public long TStates;

    public void Step();       // fetch – decode – execute d'une instruction
    public void Interrupt();  // INT masquable déclenchée par l'ULA
}
```

### Boucle principale

```csharp
public void RunFrame()
{
    while (cpu.TStates < 69888) cpu.Step();
    cpu.TStates -= 69888;
    cpu.Interrupt();          // 50 Hz
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

### Tests (dès le début)

- **Suite de tests FUSE** (`tests.in` / `tests.expected`) : chaque instruction, registres et timings.
- **ZEXDOC / ZEXALL** : exécutés dans un mini-harnais CP/M (interception de `CALL 5`).
- Intégrés dans un projet de tests xUnit.

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
| 1 | CPU Z80 + harnais de tests | Tests FUSE et ZEXDOC/ZEXALL au vert |
| 2 | Mémoire + ROM + affichage écran | Message « © 1982 Sinclair Research Ltd » à l'écran |
| 3 | Clavier | On peut taper et exécuter du BASIC |
| 4 | Chargement `.SNA` / `.Z80` | Les premiers jeux tournent |
| 5 | Beeper | Le son fonctionne |
| 6 | `.TAP` (flash loading) | Chargement des cassettes courantes |
| 7 | Contention mémoire + rendu ligne par ligne | Démos et effets de bordure corrects |
| 8 | Modèle 128K | Pagination (port `0x7FFD`) + puce son AY-3-8912 |
| 9 | Débogueur intégré | Désassembleur, points d'arrêt, vue mémoire/registres |
| 10 | `.TZX` | Chargeurs protégés / turbo |

---

## 8. La ROM (licence)

Amstrad, détenteur des droits Sinclair depuis 1986, autorise la distribution des ROM du
Spectrum **avec les émulateurs**, à condition que :

- le copyright reste celui d'Amstrad et soit mentionné (README / fenêtre « À propos ») ;
- les ROM ne soient pas vendues en tant que telles.

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
