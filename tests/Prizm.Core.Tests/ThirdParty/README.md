# Tests de timing (Richard Butler)

Ces 72 tests (`TimingTests.cs`) vérifient la durée d'instructions en mémoire normale et contendue
ainsi que le comportement du bus flottant par rapport à des mesures sur machine réelle.

## Provenance et licence

- Auteur : **Richard Butler**
- Source originale : <https://www.zxspectrum4.net/op_timing.php>
- Harnais .NET : <https://github.com/MrKWatkins/EmulatorTestSuites> (dossier `src/MrKWatkins.EmulatorTestSuites.ZXSpectrum/Timing`)
- Licence : non formellement déclarée en open source (GPL/MIT). Par précaution, ces fichiers ne sont
  pas committés dans le dépôt Git et sont placés dans `local/timing-tests/`.

Si ces fichiers sont absents, les tests correspondants sont automatiquement ignorés (`Skip`) par le
harnais `LocalFileTheory` sans mettre la suite de tests en échec.

## Installation dans `local/`

Pour exécuter ces tests :

1. Téléchargez `timing_tests_48k_v1.0.z80` et les quatre fichiers `.scr` associés depuis
   <https://github.com/MrKWatkins/EmulatorTestSuites/tree/main/src/MrKWatkins.EmulatorTestSuites.ZXSpectrum/Timing>
   ou <https://www.zxspectrum4.net/op_timing.php>.
2. Placez-les dans `local/timing-tests/` :

```sh
mkdir -p local/timing-tests/screens
# Copiez timing_tests_48k_v1.0.z80 dans local/timing-tests/
# Copiez les 4 fichiers d'écran dans local/timing-tests/screens/ :
#   35-contended.scr
#   35-uncontended.scr
#   36-contended.scr
#   37-contended.scr
```
