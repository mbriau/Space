using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Equipment;
using Space.Core.Levels;
using Space.Core.Sectors;
using Space.Core.Ships;
using Space.Core.Time;
using Space.Presentation.Controls;
using Space.Presentation.Rendering;
using Space.Presentation.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Game
{
    /// <summary>
    /// Game flow of a run: sets up a level from its <see cref="LevelDefinition"/>, watches the
    /// objective, pays the reward, offers one reward out of three (<see cref="RewardScreen"/>) then
    /// the sector map (<see cref="SectorMapScreen"/>), where the player picks the next beacon. Phase 1
    /// is <see cref="RunState.SectorsInPhase"/> sectors, each a delivery to bring to its exit; on
    /// defeat, offers a new run.
    /// The level itself (spawns, objective, combat) runs in the simulation. The player's ship is
    /// fitted with the run's equipment (<see cref="RunState.Loadout"/>) when it spawns, and the
    /// drones of the loadout join it as escorts.
    /// </summary>
    public class LevelRunner : MonoBehaviour
    {
        public LevelDefinition FirstLevel;

        [Tooltip("Scripted opening of a level (camera, lines, arrivals); found on this object when empty.")]
        public LevelIntro Intro;

        [Tooltip("Seed of the first run; each run and level derives its own from it.")]
        public uint BaseSeed = 20261126;

        readonly RunState _run = new();
        readonly RewardScreen _rewards = new();
        readonly SectorMapScreen _mapScreen = new();
        LevelPool _pool;

        /// <summary>Delivery bonus paid by the last exit won, or -1.</summary>
        int _deliveryBonus = -1;
        LevelDefinition _current;
        LevelStatus _shownStatus;
        bool _started;

        World _world;
        EntityQuery _levelQuery;
        EntityQuery _timeQuery;
        EntityQuery _shipsQuery;
        EntityQuery _libraryQuery;
        EntityQuery _shotsQuery;

        /// <summary>Formation slots of the loadout's drones, on the flagship's flanks.</summary>
        static readonly float3[] DroneOffsets = { new(-45f, 0f, -40f), new(45f, 0f, -40f) };

        readonly List<(float3 Offset, ItemDefinition Item)> _drones = new();
        EquipmentMenus _menus;
        EntityQuery _unfittedFlagships;
        EntityQuery _unfittedEscorts;
        EntityQuery _flagshipQuery;

        GUIStyle _title;
        GUIStyle _text;
        GUIStyle _button;

        public RunState Run => _run;
        public LevelDefinition CurrentLevel => _current;
        public bool HasStarted => _started;

        public bool IsLevelRunning =>
            _started && _world != null && _world.IsCreated && !_levelQuery.IsEmpty &&
            _levelQuery.GetSingleton<LevelState>().Status == LevelStatus.Running;

        public bool IsPaused =>
            _world != null && _world.IsCreated && !_timeQuery.IsEmpty && _timeQuery.GetSingleton<SimulationTime>().Paused;

        public bool TryGetFlagship(out Entity ship)
        {
            ship = Entity.Null;
            if (_world == null || !_world.IsCreated || _flagshipQuery.IsEmpty)
                return false;
            using var ships = _flagshipQuery.ToEntityArray(Allocator.Temp);
            ship = ships[0];
            return true;
        }

        void Awake()
        {
            _menus = GetComponent<EquipmentMenus>();
            if (_menus == null)
                _menus = gameObject.AddComponent<EquipmentMenus>();
            _menus.Runner = this;
            if (Intro == null)
                Intro = GetComponent<LevelIntro>();

            // Tactical presentation: turrets on the decks, dust drifting past the camera.
            if (FindAnyObjectByType<TurretPresenter>() == null)
                gameObject.AddComponent<TurretPresenter>();
            var rig = FindAnyObjectByType<ShipCameraRig>();
            var cameraObject = rig != null ? rig.gameObject : Camera.main != null ? Camera.main.gameObject : null;
            // Dust only for scenes without a space backdrop (the backdrop's star fields show the speed).
            if (cameraObject != null && cameraObject.GetComponent<SpaceDust>() == null && cameraObject.GetComponent<SpaceBackdrop>() == null)
                cameraObject.AddComponent<SpaceDust>();
        }

        bool EnsureWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            if (world != _world)
            {
                _world = world;
                var em = world.EntityManager;
                _levelQuery = em.CreateEntityQuery(typeof(LevelState));
                _timeQuery = em.CreateEntityQuery(typeof(SimulationTime));
                _shipsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Ship>());
                _libraryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ShipLibraryEntry>());
                _shotsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ShotEventQueue>());
                _flagshipQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagship>());
                _unfittedFlagships = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<PlayerFlagship>() },
                    None = new[] { ComponentType.ReadOnly<EquipmentFitted>() },
                });
                _unfittedEscorts = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<FormationSlot>(), ComponentType.ReadOnly<Ship>() },
                    None = new[] { ComponentType.ReadOnly<EquipmentFitted>(), ComponentType.ReadOnly<PlayerFlagship>() },
                });
                _started = false;
            }
            return true;
        }

        void Update()
        {
            if (!EnsureWorld() || _timeQuery.IsEmpty)
                return;

            if (!_started)
            {
                // Wait for the level sub scene (ship library) to finish streaming in.
                if (_libraryQuery.IsEmpty || FirstLevel == null)
                    return;
                BeginRun();
                _started = true;
            }

            FitSpawnedShips();

            var status = _levelQuery.GetSingleton<LevelState>().Status;
            if (status == _shownStatus)
                return;
            _shownStatus = status;

            if (status == LevelStatus.Won)
            {
                var beacon = _run.Sector.CurrentBeacon;
                _run.Credits += _current.RewardCredits;
                _run.LevelsCompleted++;
                _run.BetweenLevels = true;
                _deliveryBonus = -1;
                if (_run.Sector.AtExit)
                {
                    _deliveryBonus = _run.Sector.CurrentBonus;
                    _run.Credits += _deliveryBonus;
                    _run.SectorDone = true;
                }
                _run.RollRewards(math.hash(new uint4(BaseSeed, (uint)_run.RunIndex, (uint)_run.LevelsCompleted, 0x5109u)),
                    beacon.Kind == BeaconKind.Elite);
                _rewards.Reset();
                SetPaused(true);
            }
            else if (status == LevelStatus.Lost)
            {
                SetPaused(true);
            }
        }

        /// <summary>Fits the flagship and the loadout's drones once the simulation has spawned them.</summary>
        void FitSpawnedShips()
        {
            var em = _world.EntityManager;
            if (!_unfittedFlagships.IsEmpty)
            {
                var stats = LoadoutMath.Compute(_run.Loadout);
                using var flagships = _unfittedFlagships.ToEntityArray(Allocator.Temp);
                foreach (var ship in flagships)
                {
                    LoadoutApplier.ApplyToFlagship(em, ship, stats);
                    em.AddComponent<EquipmentFitted>(ship);
                }
            }

            if (!_unfittedEscorts.IsEmpty)
            {
                using var escorts = _unfittedEscorts.ToEntityArray(Allocator.Temp);
                foreach (var escort in escorts)
                {
                    var offset = em.GetComponentData<FormationSlot>(escort).Offset;
                    foreach (var (droneOffset, item) in _drones)
                        if (math.distancesq(offset, droneOffset) < 0.01f)
                            LoadoutApplier.ApplyToDrone(em, escort, item);
                    em.AddComponent<EquipmentFitted>(escort);
                }
            }
        }

        /// <summary>A new run: the first sector's map, its start being the first level (with its intro).</summary>
        void BeginRun()
        {
            _run.StartNewRun();
            _pool = LevelPool.Collect(FirstLevel);
            _run.StartSector(SectorSeed(0), _pool.Count);
            _deliveryBonus = -1;
            StartLevel(FirstLevel);
        }

        uint SectorSeed(int sector) => math.hash(new uint4(BaseSeed, (uint)_run.RunIndex, (uint)sector, 0x5EC7u));

        /// <summary>Jumps to a beacon of the map: a fight starts there, or a shop opens.</summary>
        void JumpTo(int beaconIndex)
        {
            if (!_run.Sector.Jump(beaconIndex))
                return;
            _run.ShopOpen = false;
            var beacon = _run.Sector.CurrentBeacon;
            if (beacon.Kind == BeaconKind.Shop)
            {
                _run.ShopOpen = true;
                _run.RollShop(math.hash(new uint4(BaseSeed, (uint)_run.RunIndex, (uint)_run.SectorIndex * 64u + (uint)beaconIndex, 0x5109u)));
                return;
            }
            var level = _pool.ForBeacon(beacon);
            if (level != null)
                StartLevel(level);
        }

        void StartLevel(LevelDefinition level)
        {
            var em = _world.EntityManager;
            _current = level;
            _run.BetweenLevels = false;
            _run.ShopOpen = false;
            if (_menus != null && _menus.IsOpen)
                _menus.Close();

            // Clear the previous level: ships (with their child entities) and pending shots.
            using (var ships = _shipsQuery.ToEntityArray(Allocator.Temp))
                foreach (var ship in ships)
                    if (em.Exists(ship))
                        em.DestroyEntity(ship);
            if (!_shotsQuery.IsEmpty)
            {
                var queue = _shotsQuery.GetSingletonEntity();
                em.GetBuffer<ShotEvent>(queue).Clear();
                em.GetBuffer<ProjectileInFlight>(queue).Clear();
                em.GetBuffer<ImpactEvent>(queue).Clear();
            }

            var levelEntity = _levelQuery.IsEmpty ? CreateLevelEntity(em) : _levelQuery.GetSingletonEntity();
            var tick = _timeQuery.GetSingleton<SimulationTime>().Tick;
            var startTick = tick + 1;
            const float ticksPerSecond = SimulationClockBootstrapSystem.TickRate;

            em.SetComponentData(levelEntity, new LevelState
            {
                Status = LevelStatus.Running,
                StartTick = startTick,
                EndTick = startTick + (uint)math.round(level.SurviveSeconds * ticksPerSecond),
                WinWhenEnemiesDestroyed = level.WinWhenEnemiesDestroyed,
                RunSeed = math.hash(new uint3(BaseSeed, (uint)_run.RunIndex, (uint)_run.LevelsCompleted)),
                CruiseSpeed = level.CruiseSpeed > 0f ? level.CruiseSpeed : Engagement.DefaultCruiseSpeed,
            });

            var spawns = em.GetBuffer<LevelSpawn>(levelEntity);
            spawns.Clear();
            spawns.Add(new LevelSpawn { Ship = level.Flagship, Role = SpawnRole.Flagship, Position = double3.zero });
            foreach (var escort in level.Escorts)
                spawns.Add(new LevelSpawn { Ship = escort.Ship, Role = SpawnRole.Escort, Offset = escort.Offset });

            // The drones of the player's loadout fly with the flagship in every level.
            _drones.Clear();
            var drones = LoadoutMath.Compute(_run.Loadout).Drones;
            for (var i = 0; i < drones.Count && i < DroneOffsets.Length; i++)
            {
                _drones.Add((DroneOffsets[i], drones[i]));
                spawns.Add(new LevelSpawn { Ship = drones[i].DroneShip, Role = SpawnRole.Escort, Offset = DroneOffsets[i] });
            }
            foreach (var enemy in level.Enemies)
            {
                spawns.Add(new LevelSpawn
                {
                    Ship = enemy.Ship,
                    Role = SpawnRole.Enemy,
                    DelayTicks = (uint)math.round(enemy.DelaySeconds * ticksPerSecond),
                    Distance = enemy.Distance,
                    AngleDegrees = enemy.AngleDegrees,
                    Along = enemy.Along,
                });
            }

            // A level with an intro starts held (no enemy, no shot, timer stopped) until the intro lets go.
            var intro = Intro != null && Intro.Plays(level);
            em.SetComponentData(levelEntity, new LevelHold { Clock = intro, CeaseFire = intro });

            _shownStatus = LevelStatus.Running;
            SetPaused(false);
            if (intro)
                Intro.Play(this);
        }

        /// <summary>Holds or releases the level clock and the weapons (used by the intro).</summary>
        public void SetHold(bool clock, bool ceaseFire)
        {
            if (!EnsureWorld() || _levelQuery.IsEmpty)
                return;
            _world.EntityManager.SetComponentData(_levelQuery.GetSingletonEntity(), new LevelHold { Clock = clock, CeaseFire = ceaseFire });
        }

        /// <summary>Number of enemies of the current level already in space.</summary>
        public int ArrivedEnemies()
        {
            if (!EnsureWorld() || _levelQuery.IsEmpty)
                return 0;
            var count = 0;
            foreach (var spawn in _world.EntityManager.GetBuffer<LevelSpawn>(_levelQuery.GetSingletonEntity(), true))
                if (spawn.Role == SpawnRole.Enemy && spawn.Done)
                    count++;
            return count;
        }

        static Entity CreateLevelEntity(EntityManager em)
        {
            var entity = em.CreateEntity(typeof(LevelState), typeof(LevelHold));
            em.AddBuffer<LevelSpawn>(entity);
            em.SetName(entity, "Level");
            return entity;
        }

        public void SetPaused(bool paused)
        {
            var timeEntity = _timeQuery.GetSingletonEntity();
            var time = _world.EntityManager.GetComponentData<SimulationTime>(timeEntity);
            time.Paused = paused;
            _world.EntityManager.SetComponentData(timeEntity, time);
        }

        void OnGUI()
        {
            if (!_started || _current == null || _world == null || !_world.IsCreated || _levelQuery.IsEmpty)
                return;
            if (UiFocus.Cinematic)
                return;

            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            _text ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 8, 8) };

            var level = _levelQuery.GetSingleton<LevelState>();
            var tick = _timeQuery.GetSingleton<SimulationTime>().Tick;

            // Objective banner.
            var remaining = level.EndTick > tick ? (level.EndTick - tick) / SimulationClockBootstrapSystem.TickRate : 0f;
            var banner = $"{_current.DisplayName} · {_current.Environment}";
            var objective = $"Survivre : {Mathf.FloorToInt(remaining / 60f)}:{Mathf.FloorToInt(remaining % 60f):00}" +
                            $"    Crédits : {_run.Credits}    Niveaux réussis : {_run.LevelsCompleted}";
            GUI.Label(new Rect(0, 8, Screen.width, 28), banner, _title);
            GUI.Label(new Rect(0, 36, Screen.width, 22), objective, _text);

            if (_menus != null && _menus.IsOpen)
                return;
            if (level.Status == LevelStatus.Won)
                DrawVictory();
            else if (level.Status == LevelStatus.Lost)
                DrawDefeat();
        }

        void DrawVictory()
        {
            if (!_run.RewardClaimed)
                DrawRewards();
            else if (_run.SectorDone)
                DrawDelivery();
            else
                DrawMap();
        }

        /// <summary>The sector map: where to jump next, the delivery deadline, the ship and the shop.</summary>
        void DrawMap()
        {
            var sector = _run.Sector;
            var width = Mathf.Min(SectorMapScreen.Width, Screen.width - 20f);
            var height = Mathf.Min(SectorMapScreen.Height + 90f, Screen.height - 70f);
            var area = new Rect((Screen.width - width) * 0.5f, Mathf.Max(64f, (Screen.height - height) * 0.5f), width, height);
            DrawPanel(area);

            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 10, area.width - 32, 80));
            var where = sector.CurrentBeacon.Kind == BeaconKind.Shop ? " · Station marchande : magasin ouvert" : "";
            GUILayout.Label($"Secteur {_run.SectorIndex + 1} / {RunState.SectorsInPhase}{where}", _title);
            var late = sector.Jumps > sector.Deadline ? " (en retard)" : "";
            GUILayout.Label($"Livraison : {sector.Jumps} / {sector.Deadline} sauts{late} · prime actuelle {sector.CurrentBonus} cr, " +
                            $"−{SectorRun.BonusLostPerLateJump} cr par saut de retard · crédits {_run.Credits}", _text);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Mon vaisseau (I)", GUILayout.Height(26)))
                _menus.Open(EquipmentMenus.Page.Ship);
            if (_run.ShopOpen && GUILayout.Button("Magasin (M)", GUILayout.Height(26)))
                _menus.Open(EquipmentMenus.Page.Shop);
            if (GUILayout.Button("Catalogue (C)", GUILayout.Height(26)))
                _menus.Open(EquipmentMenus.Page.Catalog);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            var mapArea = new Rect(area.x, area.y + 90f, area.width, area.height - 90f);
            var clicked = _mapScreen.Draw(mapArea, _run, _pool);
            if (clicked >= 0)
                JumpTo(clicked);
        }

        /// <summary>The cargo is delivered: on to the next sector, or the end of phase 1.</summary>
        void DrawDelivery()
        {
            var area = new Rect((Screen.width - 460) * 0.5f, (Screen.height - 220) * 0.5f, 460, 220);
            DrawPanel(area);
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            GUILayout.Label("Livraison effectuée", _title);
            var sector = _run.Sector;
            GUILayout.Label($"{sector.Jumps} sauts pour un délai de {sector.Deadline} : prime de {Mathf.Max(0, _deliveryBonus)} crédits " +
                            $"(total {_run.Credits}).", _text);
            GUILayout.Space(8);
            if (_run.PhaseComplete)
            {
                GUILayout.Label("Fin de la phase 1 pour l'instant : le niveau de transition viendra plus tard.", _text);
                if (GUILayout.Button("Nouvelle partie", _button, GUILayout.Height(40)))
                    BeginRun();
            }
            else if (GUILayout.Button($"Secteur suivant ({_run.SectorIndex + 2} / {RunState.SectorsInPhase})", _button, GUILayout.Height(40)))
            {
                _run.NextSector(SectorSeed(_run.SectorIndex + 1), _pool.Count);
            }
            GUILayout.EndArea();
        }

        /// <summary>The choice of one reward out of three, before the next destination.</summary>
        void DrawRewards()
        {
            const float width = 560f;
            var height = 100f + _rewards.ContentHeight(_run);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(area);
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            GUILayout.Label("Niveau réussi", _title);
            var delivery = _deliveryBonus >= 0 ? $" Livraison : +{_deliveryBonus} crédits de prime." : "";
            GUILayout.Label($"+{_current.RewardCredits} crédits (total {_run.Credits}).{delivery}", _text);
            _rewards.Draw(_run);
            GUILayout.EndArea();
        }

        static void DrawPanel(Rect area)
        {
            GUI.color = new Color(0.03f, 0.04f, 0.08f, 0.94f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawDefeat()
        {
            var area = new Rect((Screen.width - 420) * 0.5f, (Screen.height - 170) * 0.5f, 420, 170);
            DrawPanel(area);
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            GUILayout.Label("Partie terminée", _title);
            GUILayout.Label($"Le {_current.Flagship} a été détruit après {_run.LevelsCompleted} niveau(x) réussi(s) " +
                            $"et {_run.Credits} crédits.", _text);
            GUILayout.Space(8);
            if (GUILayout.Button("Nouvelle partie", _button, GUILayout.Height(40)))
                BeginRun();
            GUILayout.EndArea();
        }
    }
}
