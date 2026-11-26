# Plan agile de développement

Mis à jour le 25 novembre 2026. Le document de travail vivant est le Claude Doc « Jeu Espace — Plan agile de développement » ; ce fichier en est une copie de référence.

## Vision et hypothèses

Le jeu se construit en 8 jalons de 4 à 10 semaines chacun, précédés d'une preuve de concept technique, soit environ 15 mois à 15 h par semaine. Seuls M0 et M1 sont détaillés ; les suivants seront précisés au moment de les commencer.

- **Temps réel avec pause** : pause active en solo, ordres donnés pendant la pause.
- **Approche roguelite** : perdre et recommencer est normal. Une partie est plus longue qu'un roguelite classique puisqu'elle traverse les 4 phases. Une partie en cours peut être sauvegardée.
- **Progression permanente** : seule la phase 1 est accessible au départ ; la progression débloque les phases suivantes, ainsi que de nouveaux équipements, drones, PNJ et factions.
- **Caméra** : troisième personne centrée sur le vaisseau sélectionné, avec dézoom vers une vue tactique de la flotte.
- **Déplacements** : le joueur donne des destinations et des ordres, il ne pilote pas manuellement. Physique arcade.
- **Plateforme** : PC (Windows, Steam), Unity 6.6 (6000.6.10f1), URP, DOTS (Entities, Burst, Entities Graphics).
- **Équipe** : développeur solo, 15 h par semaine, sprints de 2 semaines (24 h planifiables).
- **Multijoueur** : escarmouches compétitives plus tard (M8), en 1 contre 1, ou à 4 joueurs en chacun pour soi ou 2 contre 2.

## Structure d'une partie

| Phase | Le joueur | Gagne | Jalon |
| --- | --- | --- | --- |
| 1. Seul avec son cargo | Dirige son vaisseau et ses drones ; défense très limitée | Crédits, améliorations, drones | M1, M2 |
| 2. Les alliés arrivent | Des PNJ le rejoignent ; il peut contrôler leurs vaisseaux | Une première flotte | M3 |
| 3. L'appui des factions | Missions pour d'autres factions | Points de faction | M4 |
| 4. Vers le combat final | Combats de plus en plus épiques | Flotte agrandie, combat final | M5 |

- **Niveaux** : chaque phase a un nombre défini de niveaux, un peu moins pour la phase 4. La phase 1 compte 12 niveaux de survie ou d'escorte, puis un niveau final plus difficile qui sert de transition : seul, le joueur ne peut plus s'en sortir et doit chercher d'autres vaisseaux.
- **Départ** : le vaisseau commence chaque niveau déjà dans l'espace. Il n'y a ni chargement ni vente de marchandises.
- **Bases spatiales** : certains niveaux en contiennent une, où améliorer le vaisseau ou acheter des drones.
- **Choix de route** : après chaque niveau, le joueur choisit entre 2 ou 3 destinations (champ d'astéroïdes, station spatiale, géante gazeuse...). La rencontre dépend du lieu choisi.
- **Durée des combats** : environ 1 minute au début ; vers la fin, 10 à 30 minutes de temps réel avec les pauses, pour au plus 5 minutes de temps simulé. L'équilibrage se fait en temps simulé.
- **Trame (idée de travail)** : l'« Empire » attaque, et les contrebandiers se soutiennent entre eux.

Ce que la structure impose à l'architecture :

- Machine à états de partie dans `Core`, phases décrites en ScriptableObjects.
- Un seul pipeline d'ordres pour joueur, drones et PNJ ; l'IA émet les mêmes commandes quand le joueur ne dirige pas.
- Les drones de la phase 1 éprouvent la sélection et les ordres groupés avant la phase 2.
- Un seul système de combat qui monte en échelle, testé en premier par M0b.
- Niveaux pilotés par les données : une `LevelDefinition` (ScriptableObject) et une sous-scène par environnement ; les rencontres sont des données.
- Une graine par partie pilote tous les tirages, ce qui rend une partie reproductible.
- Carte de route générée par phase à partir de la graine : graphe à 2 ou 3 branches, rencontre tirée d'un pool filtré par environnement, phase et contenu débloqué.
- Deux sauvegardes distinctes : la partie en cours et la progression permanente. Chaque définition de contenu porte sa condition de déblocage.

## Feuille de route

| Jalon | Objectif | Critère de sortie | Durée |
| --- | --- | --- | --- |
| M0 Fondations | Projet, pipeline, architecture | Build PC automatique, scène de test, profil de référence | 2 sprints |
| M0b Preuve de concept | Des centaines de vaisseaux en combat | 500 vaisseaux à 60 fps (GPU milieu de gamme), tick < 4 ms, caméra 3e personne à tactique, DOTS validé, limite mesurée | 2 sprints |
| M1 Tranche verticale : un niveau | Le cargo survit à un niveau | Départ dans l'espace, ordres de déplacement, objectif de survie, récompense, choix de la destination suivante, une base spatiale, pause | 3 sprints |
| M2 Phase 1 complète | Survivre et progresser seul avec cargo et drones | Carte de route, 12 niveaux de survie et d'escorte plus le niveau de transition, bases spatiales, drones, pirates, défense limitée, sauvegarde de la partie, déblocage de la phase 2 | 4 sprints |
| M3 Phase 2 : alliés PNJ | Des PNJ rejoignent le joueur | Recrutement, multi-sélection, files d'ordres, prise de contrôle, IA, escorte, formations | 4 sprints |
| M4 Phase 3 : factions | Gagner l'appui des factions | 3 à 4 factions, 4 types de missions, points de faction, réputation | 4 sprints |
| M5 Phase 4 : combat final | Combats épiques jusqu'au combat final | Achat de vaisseaux de guerre, batailles de flottes, combat final | 5 sprints |
| M6 Contenu et équilibrage | Partie complète des 4 phases | Tutoriel, équilibrage, variété d'une partie à l'autre, playtests externes | 3 sprints |
| M7 Finition et sortie | Qualité commerciale | Art final, VFX, audio, UI, passe de performance, page Steam | 4 sprints |
| M8 Escarmouches compétitives | 1 contre 1, chacun pour soi ou 2 contre 2 | Hôte autoritaire (aucun client ne décide de l'état), partie stable 30 min, 150 ms tolérés | 5 à 6 sprints |

Total : 31 sprints (environ 62 semaines) pour le solo, plus 10 à 12 semaines pour M8. Une démo est envisageable dès que la phase 1 est complète.

## M0 Fondations (fait, sauf le build automatisé)

- [x] Unity 6.6, URP (Forward+), Input System, Cinemachine, Git LFS
- [x] Packages DOTS (Entities, Entities Graphics, Burst, Collections, Mathematics, Unity Physics), sous-scènes et bakers
- [x] Assemblies : `Core` (ECS), `Presentation`, `Authoring`, `Editor`, `Tests`
- [x] Simulation à pas fixe 20 Hz, pause et vitesse x1 / x2 / x4
- [x] Positions en double et origine flottante
- [x] Générateur de vaisseaux temporaires avec LOD, scène Sandbox
- [ ] Build PC automatisé (GitHub Actions + GameCI ou Unity Build Automation)
- [ ] Capture Profiler de référence (sur la machine de développement définitive)

## M1 Tranche verticale : un niveau

Terminé quand un joueur novice termine un niveau sans explication, en moins de 5 minutes, puis choisit la destination suivante. M1 couvre la survie ; l'escorte arrive en M2. Total 73 h.

| ID | User story | Critères d'acceptation | Estim. |
| --- | --- | --- | --- |
| US1 | Caméra 3e personne autour de mon vaisseau | Orbite souris, zoom 20 m à 2 km, pas de tremblement loin de l'origine | 6 h |
| US2 | Sélectionner mon vaisseau | Clic gauche, marqueur visible, panneau d'infos (nom, coque, vitesse) | 5 h |
| US3 | Ordonner un déplacement | Clic droit ; accélération, rotation, freinage crédibles ; contourne les astéroïdes | 12 h |
| US4 | Décrire un niveau sans code | `LevelDefinition` (départ, objectif, contenu), sous-scène chargée au lancement | 8 h |
| US5 | Survivre jusqu'à la fin du niveau | Compte à rebours (environ 60 s simulées), un pirate basique poursuit et endommage le cargo ; coque à 0 = défaite, fin du temps = réussite | 10 h |
| US6 | Être récompensé et choisir la suite | Crédits conservés d'un niveau à l'autre, écran de choix entre 2 ou 3 destinations | 8 h |
| US7 | Pause et vitesse | Espace = pause, ordres en pause, x1 / x2 / x4, indicateur à l'écran | 4 h |
| US8 | Base spatiale | Présente dans certains niveaux ; au moins une amélioration et un drone à acheter | 10 h |
| US9 | Quelques niveaux variés | 3 niveaux de test, dont un avec une base spatiale | 6 h |
| US10 | Voir trajet et arrivée | Ligne de trajet, temps d'arrivée estimé, sons provisoires | 4 h |

Sprints : S1 = US1, US2, US3, US7 (27 h) ; S2 = US4, US5, US6 (26 h) ; S3 = US8, US9, US10 + playtest (20 h).

## Architecture et performance

- **Simulation (`Core`)** : composants ECS, systèmes Burst à pas fixe 20 Hz, aucune référence à des GameObjects, tests EditMode.
- **Ordres** : commandes dans un `DynamicBuffer<ShipCommand>` par vaisseau.
- **Présentation** : Entities Graphics, interpolation entre ticks ; GameObjects seulement pour caméra, UI, certains VFX.
- **Données** : ScriptableObjects convertis par bakers ; sauvegarde JSON versionnée.
- **Échelle** : positions `double` dans la simulation, `float` relatives à l'origine flottante pour le rendu.
- **Navigation** : steering en `IJobEntity` Burst, grille spatiale ; Unity Physics pour les raycasts.
- **Multijoueur** : commandes horodatées par tick = messages réseau ; hôte autoritaire (Netcode for Entities) ; ID stables, RNG seedé, pas de `Time.deltaTime` ni `UnityEngine.Random` dans `Core`.
- **Performance** : zéro allocation GC par frame ; M0b valide DOTS et cherche la limite (viser 2 000 vaisseaux) ; LOD et impostors pour la vue tactique. Aucune mesure de performance sur la machine actuelle, trop modeste.

## Fonctionnement agile

- Jalon en cours = user stories estimées ; jalon suivant = épopées ; jalons lointains = objectif et critère de sortie.
- Début de sprint : 24 h de stories, 20 % de marge. Fin de sprint : build jouable, démo, rétro, vélocité.
- Définition de terminé : marche dans le build, logique `Core` testée, aucune allocation GC ajoutée, sur `main`.

## Questions ouvertes

- [ ] Combat direct (le joueur cible et tire) ou entièrement par ordres ?
- [ ] Sortie commerciale et échéance ?
- [ ] Rôle des drones de la phase 1 : défense, minage, éclaireurs ?
- [ ] Les niveaux sont-ils conçus à la main, générés, ou un mélange ?
- [ ] Mondes Netcode client/serveur dès M0b, ou réseau seulement en M8 ?
