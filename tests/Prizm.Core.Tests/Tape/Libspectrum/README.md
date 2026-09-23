# Fichiers de test TZX de libspectrum

Ces fichiers `.tzx` et `complete-tzx.pl` proviennent de libspectrum, la bibliothèque de
l'émulateur FUSE : dossier `test/` du dépôt <https://git.code.sf.net/p/fuse-emulator/libspectrum>,
commit `ebac4e5f55ba434549cd7970d257fb9a0f1e2f63` (20 septembre 2026). Ils sont distribués sous
licence **GNU GPL version 2 ou ultérieure**, comme libspectrum (Copyright (c) 2001-2026 Philip
Kendall, Darren Salt, Fredrick Meunier). Ils ne sont pas modifiés.

`complete-tzx.tzx` n'est pas dans le dépôt de libspectrum : c'est la sortie de
`perl complete-tzx.pl > complete-tzx.tzx`, un fichier qui contient un bloc de presque chaque type.

Les tests (`TzxFileTests.cs`) reprennent les suites d'impulsions que libspectrum attend de
`complete-tzx.tzx`, `trailing-pause-block.tzx`, `raw-data-block.tzx` et `no-pilot-gdb.tzx`
(fichier `test/tape-edges.c`), dans sa notation : durée en T-states, nombre, drapeaux. Les autres
fichiers doivent être lus jusqu'au bout sans boucler (`loop*.tzx`, `jump.tzx`, blocs vides) ou
rejetés comme corrompus (`invalid*.tzx`), comme dans libspectrum.
