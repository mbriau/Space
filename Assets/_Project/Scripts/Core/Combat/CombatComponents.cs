using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>Side a ship fights for. Ships only shoot at ships of another team.</summary>
    public struct Team : IComponentData
    {
        public const byte Player = 0;
        public const byte Pirates = 1;

        public byte Value;
    }

    /// <summary>Structural integrity. The ship is destroyed when it reaches zero.</summary>
    public struct Hull : IComponentData
    {
        public float Current;
        public float Max;
    }

    /// <summary>How eagerly automatic weapons fire.</summary>
    public enum FireMode : byte
    {
        /// <summary>Fires whenever a hit is likely.</summary>
        Normal = 0,

        /// <summary>Fires faster and at doubtful shots: more damage up close, many misses.</summary>
        TriggerHappy = 1,

        /// <summary>Fires only when a hit is almost certain.</summary>
        Conservative = 2,
    }

    /// <summary>
    /// One weapon mounted on a ship. Each weapon picks its own target automatically, unless the
    /// ship has an <see cref="AttackTarget"/> order or the weapon is disabled.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct Weapon : IBufferElementData
    {
        public float Range;
        public float Damage;

        /// <summary>Seconds between two shots in <see cref="FireMode.Normal"/>.</summary>
        public float Cooldown;

        /// <summary>Projectile speed in m/s: slow projectiles miss moving targets more often.</summary>
        public float ProjectileSpeed;

        /// <summary>Angular dispersion of the weapon, in radians.</summary>
        public float Spread;

        public FireMode Mode;
        public bool Enabled;

        /// <summary>Runtime: seconds before the weapon can fire again.</summary>
        public float CooldownLeft;

        /// <summary>Runtime: what the weapon is aiming at (for presentation).</summary>
        public Entity CurrentTarget;
    }

    /// <summary>Player or AI order: every weapon of the ship focuses on this target when it can.</summary>
    public struct AttackTarget : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// One shot fired during the last simulation ticks, kept in a singleton buffer for presentation
    /// (tracers). Presentation clears the buffer once it has drawn the shots.
    /// </summary>
    public struct ShotEvent : IBufferElementData
    {
        public double3 From;
        public double3 To;
        public bool Hit;
        public byte Team;
    }

    /// <summary>Tag on the singleton entity holding the <see cref="ShotEvent"/> buffer.</summary>
    public struct ShotEventQueue : IComponentData { }

    /// <summary>The player's main ship: losing it ends the run, other ships form up on it.</summary>
    public struct PlayerFlagship : IComponentData { }

    /// <summary>Ship driven by the simple pirate AI (closes in and circles its target).</summary>
    public struct PirateAI : IComponentData { }
}
