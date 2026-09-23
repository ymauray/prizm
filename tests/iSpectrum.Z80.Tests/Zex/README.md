# ZEXDOC et ZEXALL

`zexdoc.com` et `zexall.com` sont les exerciseurs d'instructions Z80 de Frank D. Cringle (1994).
Ce sont des programmes CP/M : ils exécutent des milliers de combinaisons d'opérandes pour chaque
groupe d'instructions, puis comparent un CRC des registres et de la mémoire aux valeurs mesurées
sur un vrai Z80. ZEXDOC ne vérifie que les drapeaux documentés ; ZEXALL vérifie aussi les
bits 3 et 5.

Provenance : dossier `testfiles/` du dépôt <https://github.com/anotherlin/z80emu>, commit
`1c418fa0d719abab9273131113defbe276101d95`. Les sources assembleur (`zexdoc.z80`, `zexall.z80`)
viennent du même endroit. Les fichiers ne sont pas modifiés.

SHA-256 :

- `zexdoc.com` : `34923a7ed82285d3038b2d54bd64899e12173eebb61f9d07b4fc72e78af2ae8f`
- `zexall.com` : `6e2da55147a04f28d303d5da6a1e6b771557ac244653590a0f24a2d39c8537e8`

Licence : **GNU GPL version 2 ou ultérieure** (voir l'en-tête des sources).

Le harnais (`CpmHarness`, dans `ZexTests.cs`) charge le programme en `0x0100`, émule les deux appels BDOS utilisés
(`C = 2` et `C = 9`) en interceptant `CALL 5`, et s'arrête au retour en `0x0000`. Un test échoue
si la sortie contient `ERROR` ou ne se termine pas par `Tests complete`.

Chaque programme exécute environ 47 milliards de T-states : 40 s en Release, 4 min en Debug.
Les deux tests portent la catégorie `Slow` :

- run rapide : `dotnet test --filter "Category!=Slow"` ;
- run complet (commande de référence) : `dotnet test -c Release` ; en cas d'échec, relancer le test en Debug pour le déboguer.
