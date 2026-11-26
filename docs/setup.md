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
- Souris (M1) : clic gauche = sélectionner un vaisseau ; clic droit = ordre de déplacement sur le plan
  horizontal du vaisseau (Maj + clic droit = ajouter à la file) ; molette = zoom de 20 m à 2 km ;
  bouton du milieu ou Alt + clic gauche = orbite.
- `ShipMovementSystem` exécute la tête de la file `ShipCommand` à chaque tick ; la logique de pilotage
  (`ShipSteering`) est pure et testée en EditMode.
- Sur la caméra de la scène : `PlayerControls` (sélection, ordres), `ShipCameraRig` (caméra 3e personne)
  et `DebugHud` (HUD provisoire en IMGUI, à remplacer par UI Toolkit).
- Les positions de simulation sont en `double3` (`SimPosition`). `SimToRenderTransformSystem` interpole
  entre les deux derniers ticks et écrit `LocalTransform` relatif à `FloatingOrigin`.
- Mettre `FloatingOriginAnchor` sur le vaisseau suivi par la caméra.

## Vaisseaux temporaires

Menu **Space > Generate Placeholder Ships** : génère dans `Assets/_Project/Art/Placeholder/` six classes
(Drone, Cargo, PirateFighter, Corvette, Frigate, Capital) avec 3 LOD, de 1 500 à 80 000 triangles en LOD0,
un matériau URP Lit par classe et un `ShipAuthoring` sur chaque prefab. Le nombre de triangles par LOD
est affiché dans la console. Les budgets se règlent dans `PlaceholderShipGenerator.Classes`.

## Scène de test

`Assets/_Project/Scenes/Sandbox.unity` contient une lumière, une caméra et une sous-scène (`Sandbox_Ships.unity`)
avec un exemplaire de chaque vaisseau temporaire. Play : les vaisseaux sont des entités rendues par Entities Graphics
(le renderer URP doit rester en Forward+).

## Tests

Window > General > Test Runner > EditMode > Run All.
