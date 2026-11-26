# Space

Jeu de stratégie spatiale en 3D, en temps réel avec pause, réalisé avec Unity 6 et DOTS.

Structure roguelite : le joueur commence avec un seul cargo quasi sans défense et enchaîne des niveaux, en choisissant à chaque fois parmi 2 ou 3 destinations. Recommencer fait partie du jeu, et de nouveaux équipements, drones, PNJ et factions se débloquent d'une partie à l'autre. Une partie traverse 4 phases d'une douzaine de niveaux :

1. **Seul avec son cargo** : survivre ou escorter, améliorer son vaisseau et acheter des drones dans les bases.
2. **Les alliés arrivent** : des PNJ le rejoignent, il peut contrôler leurs vaisseaux.
3. **L'appui des factions** : missions pour obtenir des points de faction.
4. **Vers le combat final** : la flotte grandit avec les points de faction jusqu'au combat final.

Un mode multijoueur compétitif en escarmouche (2 joueurs, ou 4 en chacun pour soi ou 2 contre 2) est prévu plus tard.

## Pile technique

- Unity 6 LTS, URP
- DOTS : Entities, Burst, Entities Graphics, Unity Physics
- Input System, Cinemachine 3, UI Toolkit
- Git LFS pour les assets binaires (`git lfs install` avant le premier commit d'assets)

## Documentation

- [Plan agile de développement](docs/plan.md)
- [Mise en place du projet](docs/setup.md)
