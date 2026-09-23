# Tests Z80 de FUSE

`tests.in` et `tests.expected` proviennent de l'émulateur FUSE (Free Unix Spectrum Emulator),
dossier `z80/tests/` du dépôt <https://git.code.sf.net/p/fuse-emulator/fuse>, commit
`6bcf10d9fe07a104ec9e8cc62c9d194cac3f7c14`. `FORMAT.txt` est le `README` d'origine de ce
dossier, qui décrit le format des deux fichiers.

Ces fichiers sont distribués sous licence **GNU GPL version 2 ou ultérieure**, comme FUSE.
Ils ne sont pas modifiés.

Le runner (`FuseTests.cs`) compare les registres, le compte de T-states final, la mémoire et
tous les événements de bus, dans l'ordre et à leur T-state : lectures et écritures mémoire
(`MR`, `MW`), accès aux ports (`PR`, `PW`) et points de contention (`MC` pour la mémoire et les
cycles internes, `PC` pour les entrées-sorties). Comme le harnais de FUSE, les doublures notent
les points de contention sans ajouter de délai, et tiennent `0x4000`-`0x7FFF` pour contendu.

Cinq cas (`edb2_1`, `edb3_1`, `edb9_2`, `edba_1`, `edbb_1`) arrêtent une instruction de bloc
juste après une répétition. FUSE est antérieur aux découvertes de David Banks (2018) sur ce cas :
pendant la répétition, les bits 5 et 3 de F viennent de PC, les instructions d'entrée-sortie
recalculent H et P/V, et MEMPTR vaut PC + 1. z80test, mesuré sur un vrai 48K, vérifie ce
comportement : pour ces cinq cas, F (sur ces seuls bits) et MEMPTR suivent la machine réelle,
et tout le reste doit toujours correspondre exactement à FUSE.
