# z80test

Programmes de test du Z80 de Patrik Rak (Raxoft), version **1.2a** : chacun exécute chaque
instruction sur un grand nombre de valeurs et compare une somme de contrôle aux valeurs relevées
sur un vrai 48K équipé d'un Z80 Zilog.

| Programme | Ce qu'il vérifie |
|---|---|
| `z80doc` | registres, drapeaux documentés |
| `z80docflags` | drapeaux documentés seuls |
| `z80full` | registres et tous les drapeaux |
| `z80flags` | tous les drapeaux |
| `z80ccf` | les drapeaux vus par `CCF` après chaque instruction (registre « Q ») |
| `z80memptr` | MEMPTR, vu par `BIT n,(HL)` après chaque instruction |

Provenance : `z80test-1.2a.zip`, release `v1.2a` de <https://github.com/raxoft/z80test> (commit
`c490c0ca94f1021509f8bd656b413861bd7cfb41`). Fichiers non modifiés ; `z80ccfscr` (test visuel)
n'est pas repris. Licence : **MIT**, voir `license.txt` (Copyright (c) 2012-2023 Patrik Rak).

SHA-256 :

- `z80ccf.tap` : `a35b3b269b58371015cf033f28a08ad1ac92cce6dd27b428caf67b7f9ce8f5b7`
- `z80doc.tap` : `4b06ce9fd517fd5f5a86d4ff2ef05a3f0ab7ed20eb075b87b0623d42fbd3e4bd`
- `z80docflags.tap` : `cf64a8d66f248bc054c5255f3a51ba50a511b166b29054b7192ca91e306a528d`
- `z80flags.tap` : `4dbe34a28ed564ac782b538736e1a1c57c562876165a3294bd93af25ea3df653`
- `z80full.tap` : `cdfebeff306e5c0756ecb3295a1aebb8e71c336b2de35ecc3c0397146d73813c`
- `z80memptr.tap` : `444582ddfa4d05711b6235e743ddf68295231e97ba92cc838c5d09a106c9a10f`

Le harnais (`Z80TestTests.cs`) charge la cassette en mode rapide, laisse la machine taper
`LOAD ""`, puis relit ce que le programme affiche en interceptant `RST 0x10`, la routine
d'affichage de la ROM. Il remet la variable système `SCR-CT` (23692) à 255 avant chaque
caractère, pour que la ROM ne demande jamais `scroll?`. Le test échoue si la dernière ligne
n'est pas `Result: all tests passed.` ; les lignes `FAILED` et leurs détails apparaissent dans
la sortie du test.

Chaque programme représente 20 à 45 minutes de temps Spectrum, soit quelques secondes de calcul ; les
tests sont dans la catégorie `Slow`, et xUnit les lance en parallèle.
