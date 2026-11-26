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

    /// <summary>Ship subsystems that draw power from the reactor.</summary>
    public enum PowerSystem : byte
    {
        Shield = 0,
        EnergyWeapons = 1,
        PhysicalWeapons = 2,
        Engines = 3,
    }

    /// <summary>
    /// The reactor produces a fixed number of power units that the player (or the AI) distributes
    /// between subsystems. Each subsystem works in proportion to the power it receives, up to its
    /// capacity: power is the main strategic lever of a fight.
    /// </summary>
    public struct PowerGrid : IComponentData
    {
        public const int SystemCount = 4;

        /// <summary>Total power units produced by the reactor.</summary>
        public byte Reactor;

        public byte Shield;
        public byte EnergyWeapons;
        public byte PhysicalWeapons;
        public byte Engines;

        public byte ShieldCapacity;
        public byte EnergyWeaponsCapacity;
        public byte PhysicalWeaponsCapacity;
        public byte EnginesCapacity;

        public int Allocated => Shield + EnergyWeapons + PhysicalWeapons + Engines;
        public int Free => Reactor - Allocated;

        public int Get(PowerSystem system) => system switch
        {
            PowerSystem.Shield => Shield,
            PowerSystem.EnergyWeapons => EnergyWeapons,
            PowerSystem.PhysicalWeapons => PhysicalWeapons,
            _ => Engines,
        };

        public int Capacity(PowerSystem system) => system switch
        {
            PowerSystem.Shield => ShieldCapacity,
            PowerSystem.EnergyWeapons => EnergyWeaponsCapacity,
            PowerSystem.PhysicalWeapons => PhysicalWeaponsCapacity,
            _ => EnginesCapacity,
        };

        public void Set(PowerSystem system, int value)
        {
            var clamped = (byte)math.clamp(value, 0, Capacity(system));
            switch (system)
            {
                case PowerSystem.Shield: Shield = clamped; break;
                case PowerSystem.EnergyWeapons: EnergyWeapons = clamped; break;
                case PowerSystem.PhysicalWeapons: PhysicalWeapons = clamped; break;
                default: Engines = clamped; break;
            }
        }

        /// <summary>How well a subsystem works, from 0 (unpowered) to 1 (full capacity).</summary>
        public float Factor(PowerSystem system)
        {
            var capacity = Capacity(system);
            return capacity > 0 ? (float)Get(system) / capacity : 0f;
        }
    }

    /// <summary>
    /// Energy shield. Powered from the <see cref="PowerGrid"/>: the more power, the faster it
    /// recharges. While up it mitigates physical damage according to its charge. When broken (charge
    /// reaches zero) it stays down for a few seconds before it can recharge: the window to strike.
    /// </summary>
    public struct Shield : IComponentData
    {
        public float Current;
        public float Max;

        /// <summary>Shield points recharged per second for each power unit.</summary>
        public float RechargePerPower;

        /// <summary>Seconds a broken shield stays down before it starts recharging.</summary>
        public float RebootSeconds;

        /// <summary>Runtime: the shield receives power this tick.</summary>
        public bool Active;

        /// <summary>Runtime: the shield cannot recharge before this tick (after being broken).</summary>
        public uint BrokenUntilTick;

        /// <summary>Runtime: tick of the last hit taken while the shield was up (presentation flash).</summary>
        public uint LastHitTick;

        /// <summary>The shield protects the ship right now.</summary>
        public bool IsUp => Active && Current > 0f;
    }

    public enum DamageType : byte
    {
        /// <summary>Energy weapon that only drains shields.</summary>
        Ion = 0,

        /// <summary>Energy weapon: drains shields and also damages the hull (mitigated by the shield).</summary>
        Laser = 1,

        /// <summary>Physical weapon: hull damage mitigated by the shield, limited ammunition.</summary>
        Kinetic = 2,

        /// <summary>Physical: hits the shield first, then heavy hull damage. Very limited ammunition.</summary>
        Missile = 3,
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
    /// ship has an <see cref="AttackTarget"/> order. It charges in proportion to the power of its
    /// subsystem (energy or physical weapons) and does not fire without power.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct Weapon : IBufferElementData
    {
        public DamageType Type;
        public float Range;

        /// <summary>Damage to the hull, mitigated by the target's shield.</summary>
        public float Damage;

        /// <summary>Damage to the shield, applied first.</summary>
        public float ShieldDamage;

        /// <summary>Shots left; negative means unlimited.</summary>
        public int Ammo;

        /// <summary>Fires only at the target the player ordered.</summary>
        public bool OrderedTargetOnly;

        /// <summary>Seconds between two shots at full power in <see cref="FireMode.Normal"/>.</summary>
        public float Cooldown;

        /// <summary>Projectile speed in m/s: slow projectiles miss moving targets more often.</summary>
        public float ProjectileSpeed;

        /// <summary>Angular dispersion of the weapon, in radians.</summary>
        public float Spread;

        public FireMode Mode;
        public bool Enabled;

        /// <summary>Runtime: seconds of full-power charging left before the next shot.</summary>
        public float CooldownLeft;

        /// <summary>Runtime: what the weapon is aiming at (for presentation).</summary>
        public Entity CurrentTarget;

        public PowerSystem PowerSource =>
            Type == DamageType.Ion || Type == DamageType.Laser ? PowerSystem.EnergyWeapons : PowerSystem.PhysicalWeapons;
    }

    /// <summary>Player or AI order: every weapon of the ship focuses on this target when it can.</summary>
    public struct AttackTarget : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// One shot fired during the last simulation ticks, kept in a singleton buffer for presentation
    /// (projectiles in flight). Presentation clears the buffer once it has read the shots.
    /// </summary>
    public struct ShotEvent : IBufferElementData
    {
        /// <summary>The ship that fired and which of its weapons (presentation: the turret's muzzle).</summary>
        public Entity Shooter;
        public byte WeaponIndex;

        public double3 From;

        /// <summary>Where the projectile ends its flight (the target's predicted position, or beside it for a miss).</summary>
        public double3 To;

        /// <summary>Simulated seconds of flight.</summary>
        public float FlightSeconds;
        public bool Hit;
        public byte Team;
        public DamageType Type;
    }

    /// <summary>
    /// A projectile on its way. The hit is decided when it is fired (deterministic draw) and applied
    /// when it arrives, at <see cref="ImpactTick"/>.
    /// </summary>
    public struct ProjectileInFlight : IBufferElementData
    {
        public Entity Target;
        public uint ImpactTick;
        public bool Hit;
        public DamageType Type;
        public float Damage;
        public float ShieldDamage;
    }

    /// <summary>An impact this tick, for presentation (explosion size, shield flash, camera shake).</summary>
    public struct ImpactEvent : IBufferElementData
    {
        public double3 Position;
        public DamageType Type;
        public float HullDamage;
        public float ShieldDamage;
        public bool ShieldWasUp;
        public byte TargetTeam;
        public float TargetRadius;
    }

    /// <summary>Tag on the singleton entity holding the <see cref="ShotEvent"/> buffer.</summary>
    public struct ShotEventQueue : IComponentData { }

    /// <summary>The player's main ship: losing it ends the run, other ships form up on it.</summary>
    public struct PlayerFlagship : IComponentData { }

    /// <summary>Ship driven by the pirate AI (manoeuvres and manages its power).</summary>
    public struct PirateAI : IComponentData { }

    /// <summary>The ship has just arrived from hyperspace (presentation effect).</summary>
    public struct HyperspaceArrival : IComponentData
    {
        public uint Tick;
    }
}
