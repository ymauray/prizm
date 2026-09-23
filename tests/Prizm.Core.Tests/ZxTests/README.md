# zxtests (btime, stime, ulatest3)

Tests de synchronisation ULA, contention mémoire et bus flottant de Jan Bobrowski (auteur de
l'émulateur Qaop/JS) :

| Programme | Ce qu'il vérifie |
|---|---|
| `btime` | Synchronisation de la bordure ULA (`OUT (0xFE)`) au T-state près |
| `stime` | Synchronisation de l'affichage vidéo (écriture en mémoire d'écran) au T-state près |
| `ulatest3` | Contention mémoire et bus flottant (ports `0xFFFF` et `0xFFFE`) sur 48K et 128K |

## Provenance et licence

- Auteur : **Jan Bobrowski** (Copyright (C) Jan Bobrowski)
- Site web : <https://torinak.com/~jb/zx/> (archive des sources : `zxtests-3.tar.gz`)
- Page originale archivée : <http://web.archive.org/web/20181010082338/http://wizard.ae.krakow.pl/~jb/qaop/tests.html>
- Licence : **GNU General Public License (GPL)**, déclarée dans les en-têtes des programmes BASIC
  respectifs (`0 REM license GPL`).

## Fichiers

SHA-256 :

- `btime.tap` : `235f3ed166f819b4b743daf968dd06719aeb1b5cfbaea4635ed75790ddf959cd`
- `stime.tap` : `5c136a2adee5064aad3397223bb1fc51bea5aaa9e4b7a5c099b06647f03864c2`
- `ulatest3.tap` : `9445d3bd1661c2d5a62e2b3762ebd1ab00af9b319435ae7397bf6eb51462c6c9`

## Utilisation

Ces cassettes peuvent être chargées directement dans l'émulateur (`dotnet run --project src/Prizm.App -- tests/Prizm.Core.Tests/ZxTests/btime.tap`) :
- Dans `btime` et `stime`, les touches `Q` et `A` permettent de décaler le T-state de test pour observer le moment exact où la bordure ou le pixel change de couleur.
- Dans `ulatest3`, le test trace une matrice de résultats de contention et de bus flottant.
