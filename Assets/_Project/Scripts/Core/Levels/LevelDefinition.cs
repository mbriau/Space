using System;
using UnityEngine;

namespace Space.Core.Levels
{
    /// <summary>
    /// Describes one level without code: environment, ships, objective, reward and the destinations
    /// offered afterwards. Create with Assets > Create > Space > Level Definition.
    /// </summary>
    [CreateAssetMenu(menuName = "Space/Level Definition", fileName = "Level")]
    public class LevelDefinition : ScriptableObject
    {
        public string DisplayName = "Niveau";

        [TextArea] public string Description;

        [Tooltip("Kind of place (asteroid field, station, gas giant...). Decor comes later (US10).")]
        public string Environment = "Espace";

        [Header("Objectif : survivre")]
        [Min(1f)] public float SurviveSeconds = 60f;
        public bool WinWhenEnemiesDestroyed = true;

        [Header("Combat tactique")]
        [Tooltip("Speed of the fixed course the flagship keeps (m/s). Enemies sail alongside at the same speed.")]
        [Min(1f)] public float CruiseSpeed = 15f;

        [Header("Vaisseaux du joueur")]
        public string Flagship = "Cargo";
        public EscortSpawn[] Escorts = Array.Empty<EscortSpawn>();

        [Header("Ennemis")]
        public EnemySpawn[] Enemies = Array.Empty<EnemySpawn>();

        [Header("Suite")]
        [Min(0)] public int RewardCredits = 100;

        [Tooltip("Two or three destinations offered once the level is won.")]
        public LevelDefinition[] NextLevels = Array.Empty<LevelDefinition>();

        [Serializable]
        public struct EscortSpawn
        {
            public string Ship;

            [Tooltip("Formation slot relative to the flagship, in metres (X right, Z forward).")]
            public Vector3 Offset;
        }

        [Serializable]
        public struct EnemySpawn
        {
            public string Ship;
            [Min(0f)] public float DelaySeconds;

            [Tooltip("Distance abeam of the flagship at which the enemy sails, in metres.")]
            [Min(0f)] public float Distance;

            [Tooltip("Side it comes from: positive = starboard (right), negative = port (left).")]
            public float AngleDegrees;

            [Tooltip("Station along the course, in metres: + ahead of the flagship, - astern (0 = automatic spread).")]
            public float Along;
        }
    }
}
