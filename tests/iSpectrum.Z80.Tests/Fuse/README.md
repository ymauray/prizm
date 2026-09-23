# Tests Z80 de FUSE

`tests.in` et `tests.expected` proviennent de l'émulateur FUSE (Free Unix Spectrum Emulator),
dossier `z80/tests/` du dépôt <https://git.code.sf.net/p/fuse-emulator/fuse>, commit
`6bcf10d9fe07a104ec9e8cc62c9d194cac3f7c14`. `FORMAT.txt` est le `README` d'origine de ce
dossier, qui décrit le format des deux fichiers.

Ces fichiers sont distribués sous licence **GNU GPL version 2 ou ultérieure**, comme FUSE.
Ils ne sont pas modifiés.

Le runner (`FuseTests.cs`) compare les registres, le compte de T-states final et la mémoire.
Les événements de bus (`MR`, `MW`, `MC`, `PR`, `PW`, `PC`) sont lus mais pas encore comparés :
il faudra pour cela que le CPU horodate chaque accès, ce qui viendra avec la contention (jalon 7).
