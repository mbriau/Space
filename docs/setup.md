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
  presque sûrs). Pour l'instant toutes les armes sont en mode normal. Les tirs sont résolus avec un
  tirage déterministe par tick.
- Panneau d'armement (en bas à gauche) : une carte par arme du vaisseau du joueur. Interrupteur de
  tension avec son coût en unités (« réacteur plein » quand il manque des unités), bouton de
  surcharge pour les armes qui en ont, contrôle **Arrêt** (ne tire pas,
  continue de se recharger), **Auto** (tire seule selon son mode) ou **Manuel** (bouton « FEU » : un tir
  par clic, quelles que soient les chances, si l'arme est chargée et a une cible à portée), et barre
  bleue de recharge. Barres de bouclier (bleu) et de coque au-dessus de chaque vaisseau.
- Puissance : le réacteur produit un nombre fixe d'unités. Le bouclier et les moteurs les reçoivent
  unité par unité et fonctionnent en proportion (panneau en bas à droite, boutons − / +, ou Q et R,
  Maj pour retirer). Chaque arme consomme tout son coût quand elle est sous tension, ou rien : sous
  tension elle se recharge à pleine vitesse, hors tension elle garde sa charge sans tirer. La mise sous
  tension est refusée si le réacteur n'a pas assez d'unités libres (`WeaponPower`). Certaines armes
  ont un mode « surchargé » : des unités en plus pour des tirs plus puissants (laser d'origine :
  +2 unités, dégâts ×1,6 ; laser lourd : +2, ×1,5). Au départ : bouclier 3, moteurs 2, puis les armes
  dans l'ordre tant qu'elles tiennent (le Cargo : laser allumé, canon éteint).
- Dégâts : chaque tir retire d'abord du bouclier (dégâts bouclier), puis touche la coque (dégâts coque)
  atténués selon la charge restante du bouclier (90 % à pleine charge). Un bouclier ramené à zéro est
  « brisé » quelques secondes avant de pouvoir se recharger. Laser (énergie) : bouclier + coque ;
  ionique : bouclier seulement ; canon (physique, munitions limitées) : coque ; missiles : bouclier puis
  gros dégâts, sur cible imposée. Chaque arme préfère les cibles qu'elle abîme le plus.
- Rythme « bataille navale » : peu de tirs, lourds ; les projectiles volent réellement (obus lents
  ~350 m/s, traits d'énergie ~1 500 m/s), le tir est décidé au départ et appliqué à l'arrivée
  (`ProjectileInFlight`, `ImpactSystem`). Chaque impact produit une explosion proportionnelle aux dégâts
  (orange sur la coque, bleue sur le bouclier) et secoue la caméra quand un vaisseau du joueur est touché.
- L'IA ennemie répartit aussi sa puissance : bouclier + énergie tant que le bouclier adverse tient,
  puis armes physiques et tout sur le bouclier quand il est brisé.
- Premier niveau « Premier contact » : le Cargo seul contre un Raider (un peu plus petit, presque aussi
  rapide) qui sort d'hyperespace à 650 m. Joueur passif : défaite en 2 min de combat environ ; avec la
  stratégie ci-dessus : victoire en 1 min à 1 min 30 environ.
  Règles dans `DamageModel`, testées en EditMode.
- Les drones tiennent leur place dans la formation du vaisseau principal quand ils n'ont pas d'ordre.

## Équipement, magasin et catalogue (M1, sprint 3)

- 64 objets en 8 catégories (réacteurs, boucliers, armes à énergie, armes physiques, missiles,
  moteurs, blindages, drones), définis dans `Core/Equipment/ItemCatalog.cs`. Ce sont des compromis
  plutôt qu'une progression : grosse frappe mais courte portée ou rechargement long, bouclier solide
  mais gourmand en puissance ou lent à redémarrer, réacteur puissant mais lourd ou fragile.
- Le vaisseau a 4 emplacements fixes (réacteur, bouclier, moteurs, blindage), 3 emplacements d'arme et
  2 de drone, plus une soute (`ShipLoadout`). `LoadoutMath` calcule ce que l'équipement fait du vaisseau
  (coque, bouclier, capacités de puissance, vitesse, armes) ; l'équipement d'origine reproduit le Cargo
  du duel. `LevelRunner` l'applique au vaisseau principal et aux drones achetés quand ils apparaissent.
- Magasin entre les niveaux (`ShopGenerator`) : 8 offres tirées au hasard selon l'avancée de la partie,
  2 par catégorie au plus, toujours au moins 2 objets trop chers. Revente : moitié du prix.
- Menus provisoires en IMGUI : **I** mon vaisseau, **M** magasin (entre les niveaux), **C** catalogue,
  **Échap** pour fermer ; aussi accessibles depuis l'écran de victoire. Ouvrir un menu en combat met
  la partie en pause ; l'équipement ne se change qu'entre les niveaux.
- Icônes dessinées par code (`ItemIcons`, `IconCanvas`) à partir des caractéristiques de chaque objet.
  Le test `ItemIconTests.WritePreviewSheet` écrit une planche de toutes les icônes dans `artifacts/icons`.

## Hangar : vaisseaux moyens et champ de tir

- Menu **Space > Hangar (visionneuse de vaisseaux)** : ouvre `Assets/_Project/Scenes/Hangar.unity` (créée au premier
  usage : une scène vide avec un `ShipViewer`, qui installe sa caméra et sa lumière), puis Play.
- Dix vaisseaux moyens de 44 à 58 m (`ShipDesigns`), générés par `ShipGenerator` à partir de modules : coque
  (bloc, coin, arrondie, double coque, quille), cargaison (conteneurs sous la coque, sur les flancs, autour de la
  quille, nacelles latérales, soute bombée), passerelle (avant, tour au milieu, tour arrière), moteurs (paire, trio,
  quatre, sur pylônes, bloc) et ailes (aucune, moignons, radiateurs, dérives). Même graine = même vaisseau.
- Style cartoon : shader `Space/ToonShip` (deux tons, bande claire, reflet net, liseré, contour en coque inversée
  avec des normales lissées en UV3) ; peinture et émission dans les couleurs de sommets ; halos de réacteurs
  `Space/EngineHalo`. Les shaders sont dans `Art/Shaders/Resources` pour être trouvés dans un build.
- Trois emplacements d'arme par vaisseau : cliquer sur un emplacement pour y installer une arme du catalogue.
  La tourelle dépend de l'arme (`TurretVisuals`) : canons jumeaux, canon lourd, rail, émetteur laser, projecteur
  ionique, lance-missiles ; plus grande pour les armes rares et gourmandes.
- Champ de tir : une cible invincible à 230 m sur le flanc, comme l'ennemi en combat ; les tourelles tirent avec
  les vrais chiffres de leur arme (rechargement, vitesse des projectiles, dispersion). Purement visuel.
- Caméra tactique du jeu par défaut (`TacticalFraming`, partagé avec `ShipCameraRig`) ; **V** : caméra libre
  (clic droit ou bouton du milieu + glisser, ou Alt + clic gauche ; molette). **F** : tir ; **← / →** : vaisseau.
- Le test Play mode `HangarCaptureTests` capture chaque vaisseau (vue tactique et vue libre) dans le journal de la CI.

## Scène de test

`Assets/_Project/Scenes/Sandbox.unity` contient une lumière, une caméra et une sous-scène (`Sandbox_Ships.unity`)
avec un exemplaire de chaque vaisseau temporaire. Play : les vaisseaux sont des entités rendues par Entities Graphics
(le renderer URP doit rester en Forward+).

## Tests

Window > General > Test Runner > EditMode > Run All.
