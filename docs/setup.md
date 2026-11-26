# Mise en place du projet

## Ouvrir le projet

1. `git lfs install` (une seule fois par machine), puis `git pull`.
2. Unity Hub > Add > Add project from disk > dossier du dépôt.
3. Au premier import, Unity génère `Library/`, `ProjectSettings/` et `Packages/packages-lock.json`.
   Unity crée aussi les fichiers `.meta` des scripts : les commiter avec `ProjectSettings/` et `Packages/` ; `Library/` est ignoré.

## Arborescence

```
Assets/_Project/
  Scripts/
    Core/          Space.Core          simulation ECS pure (aucune dépendance au rendu ni aux entrées)
      Time/        horloge à pas fixe, pause, vitesse x1/x2/x4
      Ships/       composants de vaisseau (positions en double)
      Orders/      file d'ordres (DynamicBuffer<ShipCommand>)
    Presentation/  Space.Presentation  rendu interpolé, origine flottante, entrées
    Authoring/     Space.Authoring     bakers (prefab -> entité)
    Editor/        Space.Editor        outils d'éditeur (générateur de vaisseaux temporaires)
  Tests/EditMode/  Space.Core.Tests
  Art/ Scenes/ Settings/
```

## Simulation

- La simulation tourne dans `FixedStepSimulationSystemGroup` à 20 Hz, pilotée par `ScaledFixedRateManager`.
- Le singleton `SimulationTime` porte la pause, la vitesse, le numéro de tick et le facteur d'interpolation.
- Clavier : Espace = pause, 1 / 2 / 3 = vitesse x1 / x2 / x4.
- Le jeu est en « faux 3D » : tout se passe sur un même plan horizontal.
- Souris (M1) : clic gauche = sélectionner un vaisseau (un clic dans le vide garde la sélection) ;
  Tab / Maj + Tab = vaisseau suivant / précédent ; clic droit = ordre de déplacement sur le plan
  (Maj + clic droit = ajouter à la file) ; molette = zoom de 20 m à 2 km ; bouton du milieu ou
  Alt + clic gauche = orbite. Changer de vaisseau garde l'angle de caméra et presque le même zoom.
- Cercles de distance (100 m, 250 m, 500 m, 1 km, 2 km) centrés sur le vaisseau principal
  (`PlayerControls.MainShipName`, le Cargo par défaut).
- `ShipMovementSystem` exécute la tête de la file `ShipCommand` à chaque tick ; la logique de pilotage
  (`ShipSteering`) est pure et testée en EditMode.
- Sur la caméra de la scène : `PlayerControls` (sélection, ordres), `ShipCameraRig` (caméra 3e personne),
  `RangeRings` (cercles de distance) et `DebugHud` (HUD provisoire en IMGUI, à remplacer par UI Toolkit).
- Les positions de simulation sont en `double3` (`SimPosition`). `SimToRenderTransformSystem` interpole
  entre les deux derniers ticks et écrit `LocalTransform` relatif à `FloatingOrigin`.
- Mettre `FloatingOriginAnchor` sur le vaisseau suivi par la caméra.

## Vaisseaux temporaires

Menu **Space > Generate Placeholder Ships** : génère dans `Assets/_Project/Art/Placeholder/` six classes
(Drone, Cargo, PirateFighter, Corvette, Frigate, Capital) de 6 à 150 m de long, avec 3 LOD, de 1 500 à 80 000 triangles en LOD0,
un matériau URP Lit par classe et un `ShipAuthoring` sur chaque prefab. Le nombre de triangles par LOD
est affiché dans la console. Les budgets se règlent dans `PlaceholderShipGenerator.Classes`.

## Niveaux (M1, sprint 2)

- Scène `Assets/_Project/Scenes/Level.unity` (première du build) : caméra avec les contrôles, `Level Runner`
  et une sous-scène `Level_Content.unity` qui contient la bibliothèque de vaisseaux (`ShipLibraryAuthoring`).
- Un niveau est un asset `LevelDefinition` (Assets > Create > Space > Level Definition), dans
  `Assets/_Project/Data/Levels` : environnement, durée de survie, drones en formation autour du vaisseau
  principal, pirates (délai, distance, angle), récompense et 2 ou 3 destinations suivantes.
- La simulation fait apparaître les vaisseaux au bon tick (`LevelSpawnSystem`) et décide de la victoire
  ou de la défaite (`LevelObjectiveSystem`). `LevelRunner` affiche l'objectif, verse la récompense et
  propose la suite ; la défaite propose une nouvelle partie.
- Combat entièrement par ordres : chaque arme choisit sa cible (l'ennemi le plus proche à portée qu'elle
  peut abîmer, ou la cible imposée par clic droit) et tire selon la probabilité de toucher et son mode :
  normal, nerveux (tire plus vite et plus souvent, rate davantage) ou prudent (ne tire que les coups
  presque sûrs). Touche F pour changer de mode. Les tirs sont résolus avec un tirage déterministe par tick.
- Énergie et boucliers : le réacteur se recharge en continu ; le bouclier consomme de l'énergie pour rester
  en ligne et pour se recharger (touche B pour le couper et libérer l'énergie). Types d'armes :
  ionique (énergie, ne vide que les boucliers), laser (énergie, dégâts à la coque), canon (sans énergie,
  dégâts à la coque) — laser et canon sont entièrement bloqués par un bouclier actif — et missiles
  (munitions limitées, gros dégâts, perturbent le bouclier ; tirés seulement sur une cible imposée).
  Règles dans `DamageModel`, testées en EditMode.
- Les drones tiennent leur place dans la formation du vaisseau principal quand ils n'ont pas d'ordre.

## Scène de test

`Assets/_Project/Scenes/Sandbox.unity` contient une lumière, une caméra et une sous-scène (`Sandbox_Ships.unity`)
avec un exemplaire de chaque vaisseau temporaire. Play : les vaisseaux sont des entités rendues par Entities Graphics
(le renderer URP doit rester en Forward+).

## Tests

Window > General > Test Runner > EditMode > Run All.
