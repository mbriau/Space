using Space.Core.Combat;
using Space.Core.Levels;
using Space.Core.Ships;
using Space.Core.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Game
{
    /// <summary>
    /// Game flow of a run (M1): sets up a level from its <see cref="LevelDefinition"/>, watches the
    /// objective, pays the reward and offers the next destinations; on defeat, offers a new run.
    /// The level itself (spawns, objective, combat) runs in the simulation.
    /// </summary>
    public class LevelRunner : MonoBehaviour
    {
        public LevelDefinition FirstLevel;

        [Tooltip("Seed of the first run; each run and level derives its own from it.")]
        public uint BaseSeed = 20261126;

        readonly RunState _run = new();
        LevelDefinition _current;
        LevelStatus _shownStatus;
        bool _started;

        World _world;
        EntityQuery _levelQuery;
        EntityQuery _timeQuery;
        EntityQuery _shipsQuery;
        EntityQuery _libraryQuery;
        EntityQuery _shotsQuery;

        GUIStyle _title;
        GUIStyle _text;
        GUIStyle _button;

        public RunState Run => _run;
        public LevelDefinition CurrentLevel => _current;

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
                _run.StartNewRun();
                StartLevel(FirstLevel);
                _started = true;
            }

            var status = _levelQuery.GetSingleton<LevelState>().Status;
            if (status == _shownStatus)
                return;
            _shownStatus = status;

            if (status == LevelStatus.Won)
            {
                _run.Credits += _current.RewardCredits;
                _run.LevelsCompleted++;
                SetPaused(true);
            }
            else if (status == LevelStatus.Lost)
            {
                SetPaused(true);
            }
        }

        void StartLevel(LevelDefinition level)
        {
            var em = _world.EntityManager;
            _current = level;

            // Clear the previous level: ships (with their child entities) and pending shots.
            using (var ships = _shipsQuery.ToEntityArray(Allocator.Temp))
                foreach (var ship in ships)
                    if (em.Exists(ship))
                        em.DestroyEntity(ship);
            if (!_shotsQuery.IsEmpty)
                em.GetBuffer<ShotEvent>(_shotsQuery.GetSingletonEntity()).Clear();

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
            });

            var spawns = em.GetBuffer<LevelSpawn>(levelEntity);
            spawns.Clear();
            spawns.Add(new LevelSpawn { Ship = level.Flagship, Role = SpawnRole.Flagship, Position = double3.zero });
            foreach (var escort in level.Escorts)
                spawns.Add(new LevelSpawn { Ship = escort.Ship, Role = SpawnRole.Escort, Offset = escort.Offset });
            foreach (var enemy in level.Enemies)
            {
                var angle = math.radians(enemy.AngleDegrees);
                spawns.Add(new LevelSpawn
                {
                    Ship = enemy.Ship,
                    Role = SpawnRole.Enemy,
                    DelayTicks = (uint)math.round(enemy.DelaySeconds * ticksPerSecond),
                    Position = new double3(math.sin(angle) * enemy.Distance, 0.0, math.cos(angle) * enemy.Distance),
                });
            }

            _shownStatus = LevelStatus.Running;
            SetPaused(false);
        }

        static Entity CreateLevelEntity(EntityManager em)
        {
            var entity = em.CreateEntity(typeof(LevelState));
            em.AddBuffer<LevelSpawn>(entity);
            em.SetName(entity, "Level");
            return entity;
        }

        void SetPaused(bool paused)
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

            if (level.Status == LevelStatus.Won)
                DrawVictory();
            else if (level.Status == LevelStatus.Lost)
                DrawDefeat();
        }

        void DrawVictory()
        {
            var next = _current.NextLevels;
            var height = 120 + next.Length * 70;
            var area = new Rect((Screen.width - 460) * 0.5f, (Screen.height - height) * 0.5f, 460, height);
            DrawPanel(area);
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            GUILayout.Label("Niveau réussi", _title);
            GUILayout.Label($"+{_current.RewardCredits} crédits. Où aller ensuite ?", _text);
            GUILayout.Space(8);
            foreach (var destination in next)
            {
                if (destination == null)
                    continue;
                var label = $"{destination.DisplayName} ({destination.Environment})\n{destination.Description}";
                if (GUILayout.Button(label, _button, GUILayout.Height(60)))
                    StartLevel(destination);
            }
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
            {
                _run.StartNewRun();
                StartLevel(FirstLevel);
            }
            GUILayout.EndArea();
        }
    }
}
