using System;

namespace Space.Core.Levels
{
    /// <summary>
    /// What makes a level an elite fight: every enemy comes with a twin from the other side, a
    /// little later and a little farther, and the reward is doubled.
    /// </summary>
    public static class EliteLevels
    {
        public const int RewardFactor = 2;
        public const float TwinDelaySeconds = 6f;
        public const float TwinExtraDistance = 40f;

        public static LevelDefinition.EnemySpawn[] Reinforce(LevelDefinition.EnemySpawn[] enemies)
        {
            if (enemies == null || enemies.Length == 0)
                return Array.Empty<LevelDefinition.EnemySpawn>();
            var result = new LevelDefinition.EnemySpawn[enemies.Length * 2];
            for (var i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                result[i] = enemy;
                result[enemies.Length + i] = new LevelDefinition.EnemySpawn
                {
                    Ship = enemy.Ship,
                    DelaySeconds = enemy.DelaySeconds + TwinDelaySeconds,
                    Distance = enemy.Distance + TwinExtraDistance,
                    AngleDegrees = -enemy.AngleDegrees,
                    Along = -enemy.Along,
                };
            }
            return result;
        }
    }
}
