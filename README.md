# Space

Jeu de stratégie spatiale en 3D, en temps réel avec pause, réalisé avec Unity 6 et DOTS.

Le joueur commence avec un seul cargo quasi sans défense qui livre des marchandises. Une partie traverse 4 phases :

1. **Seul avec son cargo** : le joueur dirige son vaisseau et ses drones.
2. **Les alliés arrivent** : des PNJ le rejoignent, il peut contrôler leurs vaisseaux.
3. **L'appui des factions** : missions pour obtenir des points de faction.
4. **Vers le combat final** : la flotte grandit avec les points de faction jusqu'au combat final.

Un mode multijoueur à 2 à 4 joueurs est prévu plus tard.

## Pile technique

- Unity 6 LTS, URP
- DOTS : Entities, Burst, Entities Graphics, Unity Physics
- Input System, Cinemachine 3, UI Toolkit
- Git LFS pour les assets binaires (`git lfs install` avant le premier commit d'assets)

## Documentation

- [Plan agile de développement](docs/plan.md)
