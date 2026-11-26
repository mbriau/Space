# Plan agile de développement

Version du 26 novembre 2026. Le document de travail vivant est le Claude Doc « Jeu Espace — Plan agile de développement » ; ce fichier en est une copie de référence.

## Vision et hypothèses

Le jeu se construit en 8 jalons de 4 à 10 semaines chacun, précédés d'une preuve de concept technique, soit environ 15 mois à 15 h par semaine. Seuls M0 et M1 sont détaillés ; les suivants seront précisés au moment de les commencer.

- **Temps réel avec pause** : pause active en solo, ordres donnés pendant la pause. Mode 2 à 4 joueurs plus tard, sans pause libre.
- **Caméra** : troisième personne centrée sur le vaisseau sélectionné, avec dézoom vers une vue tactique de la flotte.
- **Déplacements** : le joueur donne des destinations et des ordres, il ne pilote pas manuellement. Physique arcade.
- **Plateforme** : PC (Windows, Steam), Unity 6 LTS, URP, DOTS (Entities, Burst, Entities Graphics).
- **Équipe** : développeur solo, 15 h par semaine, sprints de 2 semaines (24 h planifiables).

## Structure d'une partie

| Phase | Le joueur | Gagne | Jalon |
| --- | --- | --- | --- |
| 1. Seul avec son cargo | Dirige son vaisseau et ses drones : livraisons, défense très limitée | Crédits, améliorations, drones | M1, M2 |
| 2. Les alliés arrivent | Des PNJ le rejoignent ; il peut contrôler leurs vaisseaux | Une première flotte | M3 |
| 3. L'appui des factions | Missions pour d'autres factions | Points de faction | M4 |
| 4. Vers le combat final | Combats de plus en plus épiques | Flotte agrandie, combat final | M5 |

Implications :

- Machine à états de partie dans `Core`, phases décrites en ScriptableObjects.
- Un seul pipeline d'ordres pour joueur, drones et PNJ ; l'IA émet les mêmes commandes quand le joueur ne dirige pas.
- Les drones de la phase 1 éprouvent la sélection et les ordres groupés avant la phase 2.
- Un seul système de combat qui monte en échelle, testé en premier par M0b.

## Feuille de route

| Jalon | Objectif | Critère de sortie | Durée |
| --- | --- | --- | --- |
| M0 Fondations | Projet, pipeline, architecture | Build PC automatique, scène de test, profil de référence | 2 sprints |
| M0b Preuve de concept | Des centaines de vaisseaux en combat | 500 vaisseaux à 60 fps (GPU milieu de gamme), tick < 4 ms, caméra 3e personne à tactique, DOTS validé, limite mesurée | 2 sprints |
| M1 Tranche verticale | Une livraison d'une station à une autre | Sélection, charger, voyager, décharger, crédits, pause | 3 sprints |
| M2 Phase 1 complète | Survivre et progresser seul avec cargo et drones | Économie, améliorations, drones, pirates, défense limitée, sauvegarde, passage en phase 2 | 4 sprints |
| M3 Phase 2 : alliés PNJ | Des PNJ rejoignent le joueur | Recrutement, multi-sélection, files d'ordres, prise de contrôle, IA, escorte, formations | 4 sprints |
| M4 Phase 3 : factions | Gagner l'appui des factions | 3 à 4 factions, 4 types de missions, points de faction, plusieurs secteurs | 4 sprints |
| M5 Phase 4 : combat final | Combats épiques jusqu'au combat final | Achat de vaisseaux de guerre, batailles de flottes, combat final | 5 sprints |
| M6 Contenu et équilibrage | Partie complète des 4 phases | Tutoriel, équilibrage, playtests externes | 3 sprints |
| M7 Finition et sortie | Qualité commerciale | Art final, VFX, audio, UI, passe de performance, page Steam | 4 sprints |
| M8 Multijoueur 2 à 4 | Un mode précis à plusieurs | Hôte autoritaire, partie stable 30 min, 150 ms tolérés | 5 à 6 sprints |

Total : 31 sprints (environ 62 semaines) pour le solo, plus 10 à 12 semaines pour M8.

## M0 Fondations

- [ ] Unity 6 LTS, URP, Input System, Cinemachine 3, Git LFS
- [ ] Packages DOTS (Entities, Entities Graphics, Burst, Collections, Mathematics, Unity Physics), sous-scènes et bakers
- [ ] Assemblies : `Core` (ECS), `Presentation`, `UI`, `Tests`
- [ ] Simulation à pas fixe, échelle de temps pour pause et x2 / x4
- [ ] Échelle du monde et origine flottante
- [ ] Build PC automatisé (GitHub Actions + GameCI ou Unity Build Automation)
- [ ] Capture Profiler de base

## M1 Tranche verticale : une livraison

Terminé quand un joueur novice réussit une livraison complète sans explication en moins de 5 minutes. Total 72 h.

| ID | User story | Critères d'acceptation | Estim. |
| --- | --- | --- | --- |
| US1 | Caméra 3e personne autour de mon vaisseau | Orbite souris, zoom 20 m à 2 km, pas de tremblement loin de l'origine | 6 h |
| US2 | Sélectionner mon vaisseau | Clic gauche, marqueur visible, panneau d'infos | 5 h |
| US3 | Ordonner un déplacement | Clic droit ; accélération, rotation, freinage crédibles ; contourne les astéroïdes | 12 h |
| US4 | S'amarrer à une station | Approche auto, amarrage et désamarrage, état visible | 8 h |
| US5 | Charger une marchandise | Cale à capacité limitée, durée de chargement, refus si pleine | 8 h |
| US6 | Livrer et être payé | Contrat « livrer X à B », paiement, crédits au HUD | 8 h |
| US7 | Pause et vitesse | Espace = pause, ordres en pause, x1 / x2 / x4 | 5 h |
| US8 | Menu d'ordres sur une station | S'amarrer, Charger, Décharger ; grisé si impossible | 8 h |
| US9 | Petit système stellaire | 3 stations, planète, astéroïdes, skybox ; trajet de 60 à 90 s | 6 h |
| US10 | Voir trajet et arrivée | Ligne de trajet, ETA, sons provisoires | 6 h |

Sprints : S1 = US1-3 (23 h) ; S2 = US4, US5, US7, US9 (27 h) ; S3 = US6, US8, US10 + playtest (22 h).

## Architecture et performance

- **Simulation (`Core`)** : composants ECS, systèmes Burst à pas fixe 10 à 20 Hz, aucune référence à des GameObjects, tests EditMode.
- **Ordres** : commandes dans un `DynamicBuffer` par vaisseau (`MoveTo`, `Dock`, `Load`, `Unload`).
- **Présentation** : Entities Graphics, interpolation entre ticks ; GameObjects seulement pour caméra, UI, certains VFX.
- **Données** : ScriptableObjects convertis par bakers en blob assets ; sauvegarde JSON versionnée.
- **Échelle** : positions `double` dans la simulation, `float` relatives à la caméra pour le rendu.
- **Navigation** : steering en `IJobEntity` Burst, grille spatiale ; Unity Physics pour raycasts.
- **Multijoueur** : commandes horodatées par tick = messages réseau ; hôte autoritaire (Netcode for Entities) ; ID stables, RNG seedé, pas de `Time.deltaTime` ni `UnityEngine.Random` dans `Core`.
- **Performance** : zéro allocation GC par frame ; M0b valide DOTS et cherche la limite (viser 2 000 vaisseaux) ; LOD et impostors pour la vue tactique.

## Fonctionnement agile

- Jalon en cours = user stories estimées ; jalon suivant = épopées ; jalons lointains = objectif et critère de sortie.
- Début de sprint : 24 h de stories, 20 % de marge. Fin de sprint : build jouable, démo, rétro, vélocité.
- Définition de terminé : marche dans le build, logique `Core` testée, aucune allocation GC ajoutée, sur `main`.

## Questions ouvertes

- [ ] Combat direct ou entièrement par ordres ?
- [ ] Un système, plusieurs secteurs, ou carte procédurale ?
- [ ] Perte définitive de vaisseaux ou de la partie ?
- [ ] Sortie commerciale et échéance ?
- [ ] Multijoueur coopératif ou compétitif ?
- [ ] Déclencheur du passage de phase ?
- [ ] Rôle des drones ?
- [ ] Durée et rejouabilité d'une partie ?
- [ ] Mondes Netcode client/serveur dès M0b, ou réseau seulement en M8 ?
