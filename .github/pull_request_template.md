## Description

Description concise des changements introduits par cette pull request.

## Type de changement

- [ ] Correction de bug (changement non cassant résolvant un problème)
- [ ] Nouvelle fonctionnalité (changement non cassant ajoutant une fonctionnalité)
- [ ] Refactoring / Optimisation (sans modification de comportement)
- [ ] Documentation / CI

## Checklist

- [ ] Mon code respecte les règles d'architecture du projet (`AGENTS.md`) :
  - `Prizm.Z80` reste pur sans dépendance externe
  - `Prizm.Core` n'a aucune dépendance graphique ou audio
  - `Prizm.App` est le seul projet référençant Raylib-cs
- [ ] Chaque nouveau fichier `.cs` commence par l'en-tête SPDX GPL-2.0-or-later
- [ ] Boucle chaude : zéro allocation par instruction ou par frame (Span, tableaux préalloués)
- [ ] Code, identifiants, commentaires et commits en anglais ; documentation en français
- [ ] Des tests unitaires ont été ajoutés ou mis à jour si nécessaire
- [ ] La suite de tests passe intégralement en local (`dotnet test -c Release`)
