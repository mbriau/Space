# Space — notes pour Claude

Jeu de stratégie spatiale en 3D (Unity 6.6, `6000.6.10f1`, URP, DOTS) développé par Mathieu, seul,
de façon agile. Écrire au propriétaire **en français**. Le code, les noms et les commentaires sont en
anglais ; les messages de commit et la documentation sont en français.

- Vision, jalons et user stories : `docs/plan.md` (copie du Claude Doc du projet, qui fait foi).
- Mise en place, architecture et contrôles : `docs/setup.md`.

## Le jeu en bref

Roguelite en « faux 3D » : tout se passe sur un même plan horizontal. Combat entièrement par ordres
(aucun pilotage), au rythme d'une bataille navale : peu de tirs, lourds, avec temps de vol. Le joueur
répartit la puissance du réacteur entre bouclier, armes à énergie, armes physiques et moteurs.
Premier niveau : « Premier contact », le Cargo seul contre un Raider pirate.

## Architecture (Assets/_Project/Scripts)

| Assembly | Rôle |
| --- | --- |
| `Space.Core` | Simulation ECS pure, systèmes Burst dans `FixedStepSimulationSystemGroup` à 20 Hz. Aucune référence à des GameObjects, aux entrées ni au rendu. |
| `Space.Presentation` | Rendu interpolé, origine flottante, caméra, contrôles du joueur (MonoBehaviours), HUD provisoire en IMGUI, effets. |
| `Space.Authoring` | Bakers (prefab → entité). |
| `Space.Editor` | Outils d'éditeur, dont le générateur de vaisseaux temporaires. |
| `Space.Core.Tests` | Tests EditMode de la logique pure (`Tests/EditMode`). |

Règles à respecter :

- Positions de simulation en `double3` (`SimPosition`) ; le rendu est relatif à `FloatingOrigin`.
- Tout passe par des ordres : file `DynamicBuffer<ShipCommand>`, `AttackTarget`, `PowerGrid`. Le
  joueur et l'IA utilisent les mêmes. Pas de `Time.deltaTime` ni de `UnityEngine.Random` dans `Core`
  (tirages déterministes : `WeaponMath.ShotRandom`).
- La logique testable vit dans des fonctions pures (`ShipSteering`, `WeaponMath`, `DamageModel`,
  `PowerMath`) couvertes par des tests EditMode ; ajouter un test pour toute règle nouvelle.
- Un MonoBehaviour par fichier, nommé comme sa classe.
- Les chiffres d'équilibrage des vaisseaux sont dans `PlaceholderShipGenerator.ConfigureCombat` ;
  après modification, régénérer les prefabs (menu **Space > Generate Placeholder Ships**, ou
  `Space.Editor.Placeholders.PlaceholderShipGenerator.GenerateAll()`).
- Niveaux : assets `LevelDefinition` dans `Assets/_Project/Data/Levels` ; scène principale
  `Assets/_Project/Scenes/Level.unity` (sous-scène `Level_Content.unity` = bibliothèque de vaisseaux).
- Ne jamais éditer à la main les `.unity`, `.prefab`, `.asset` : passer par l'Editor (Unity CLI,
  `unity command script eval`) ou par un script d'éditeur.
- Git LFS pour les binaires et les maillages générés (`.gitattributes`) ; ne jamais commiter `Library/`.

## Travailler avec Unity

### Sur la machine de Mathieu (Windows, Editor ouvert)

Le Unity CLI pilote l'Editor ouvert (package `com.unity.pipeline`) :

```
unity status --project-path <projet>
unity recompile --project-path <projet>
unity command test run --mode editor --filter Space.Core.Tests --filter-type Assembly --project-path <projet>
unity command script eval --file <script.cs> --project-path <projet>
unity command editor.playmode start|stop --project-path <projet>
unity command capture game_view --output-path Temp/capture.png --project-path <projet>
```

La machine actuelle est peu puissante : imports lents, aucune mesure de performance à en tirer.

### Dans une session cloud (Linux, sans carte graphique)

1. `bash tools/cloud/setup-unity.sh` : installe le Unity CLI et l'Editor Linux `6000.6.10f1`
   (plusieurs Go, plusieurs minutes), puis active une licence si une est fournie (voir le script).
2. `bash tools/cloud/run-tests.sh` : compile le projet et lance les tests EditMode sans interface ;
   le rapport est écrit dans `Temp/test-results.xml`.

Sans licence Unity activée, la session cloud peut quand même écrire le code et les tests, mais ne
peut pas les exécuter : le dire clairement au propriétaire et laisser la vérification à sa machine.
Pas de Play mode visuel ni de captures dans le cloud.
