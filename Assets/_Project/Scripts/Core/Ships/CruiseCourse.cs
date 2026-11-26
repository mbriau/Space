using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>
    /// The ship keeps a fixed course at a fixed speed, forever: the player does not steer (tactical
    /// combat, decided on 26 November). It holds a straight lane through <see cref="LaneOrigin"/>.
    /// Other ships (escorts, enemies alongside) keep their place relative to it with a <see cref="FormationSlot"/>.
    /// </summary>
    public struct CruiseCourse : IComponentData
    {
        /// <summary>Unit direction of travel, on the horizontal plane.</summary>
        public float3 Heading;

        /// <summary>Cruise speed, m/s.</summary>
        public float Speed;

        /// <summary>A point of the lane the ship follows.</summary>
        public double3 LaneOrigin;
    }

    /// <summary>Pure rules of the tactical engagement (cruise lane, enemy stations alongside). Unit tested.</summary>
    public static class Engagement
    {
        /// <summary>Default cruise speed: slow and heavy, like ships of the line.</summary>
        public const float DefaultCruiseSpeed = 15f;

        /// <summary>Distance between two enemies stationed alongside, along the course.</summary>
        public const float StationSpacing = 110f;

        /// <summary>Extra distance for each further rank of enemies.</summary>
        public const float RankSpacing = 140f;

        /// <summary>
        /// Where a ship on a cruise lane steers to: its own position brought back onto the lane, so the
        /// steering only corrects drift and the ship keeps the cruise velocity.
        /// </summary>
        public static double3 LanePoint(double3 position, double3 laneOrigin, float3 heading)
        {
            // Normalised in double precision: the lane stays exact thousands of kilometres along.
            var direction = math.normalize((double3)heading);
            var along = math.dot(position - laneOrigin, direction);
            return laneOrigin + direction * along;
        }

        /// <summary>Share of its longest weapon range at which an enemy keeps station, so the fight starts.</summary>
        public const float StationRangeShare = 0.85f;

        /// <summary>
        /// Distance abeam an enemy keeps: the authored distance, brought within reach of its weapons
        /// (until the range mechanic lets the faster ship choose the distance).
        /// </summary>
        public static float StationDistance(float authored, float longestRange)
        {
            if (longestRange <= 0f)
                return authored;
            return math.min(authored > 0f ? authored : longestRange, longestRange * StationRangeShare);
        }

        /// <summary>
        /// Station of the <paramref name="index"/>-th enemy alongside the flagship, in the flagship's
        /// frame (X right, Z forward): abeam at <paramref name="distance"/>, on the right unless the
        /// authored angle is negative. Enemies spread along the course in threes, then in further ranks.
        /// </summary>
        public static float3 EnemyStation(int index, float distance, float angleDegrees)
        {
            var side = angleDegrees < 0f ? -1f : 1f;
            var rank = index / 3;
            var slot = index % 3;
            var along = slot == 0 ? 0f : (slot == 1 ? StationSpacing : -StationSpacing);
            return new float3(side * (distance + rank * RankSpacing), 0f, along);
        }
    }
}
