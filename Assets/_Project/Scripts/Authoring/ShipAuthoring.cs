using System;
using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Orders;
using Space.Core.Ships;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Authoring
{
    /// <summary>Turns a ship prefab into a simulated ship entity.</summary>
    [DisallowMultipleComponent]
    public class ShipAuthoring : MonoBehaviour
    {
        public enum TeamId : byte
        {
            Player = Space.Core.Combat.Team.Player,
            Pirates = Space.Core.Combat.Team.Pirates,
        }

        [Serializable]
        public class WeaponSpec
        {
            [Tooltip("Ion and laser: energy weapons, unlimited. Cannon and missile: physical, with ammunition.")]
            public DamageType Type = DamageType.Kinetic;

            [Tooltip("Power units drawn from the reactor while the weapon is powered.")]
            public byte PowerDraw = 1;

            [Tooltip("Extra units drawn when supercharged; 0 = no supercharge mode.")]
            public byte SuperchargeDraw;

            [Tooltip("Damage multiplier of a supercharged shot.")]
            [Min(1f)] public float SuperchargeDamageFactor = 1f;
            [Min(0f)] public float Range = 300f;

            [Tooltip("Damage to the hull, mitigated by the target's shield.")]
            [Min(0f)] public float Damage = 5f;

            [Tooltip("Damage to the shield, applied first.")]
            [Min(0f)] public float ShieldDamage;

            [Tooltip("Shots available; -1 = unlimited.")]
            public int Ammo = -1;

            [Tooltip("Fires only at the target ordered by the player.")]
            public bool OrderedTargetOnly;

            [Tooltip("Seconds between shots at full power.")]
            [Min(0.05f)] public float CooldownSeconds = 1f;
            [Min(1f)] public float ProjectileSpeed = 1000f;
            [Min(0f)] public float SpreadDegrees = 0.8f;
            public FireMode Mode = FireMode.Normal;
        }

        [Serializable]
        public struct PowerSpec
        {
            [Tooltip("Total power units produced by the reactor.")]
            public byte Reactor;

            [Header("Capacité du bouclier et des moteurs")]
            public byte ShieldCapacity;
            public byte EnginesCapacity;

            [Header("Répartition au départ (les armes s'allument ensuite dans l'ordre si elles tiennent)")]
            public byte Shield;
            public byte Engines;
        }

        [Header("Déplacement")]
        [Min(0f)] public float MaxSpeed = 50f;
        [Min(0f)] public float Acceleration = 10f;
        [Min(0f)] public float TurnRateDegrees = 45f;

        [Header("Combat")]
        public TeamId Team = TeamId.Player;
        [Min(1f)] public float Hull = 100f;

        [Header("Réacteur")]
        public PowerSpec Power = new()
        {
            Reactor = 4, ShieldCapacity = 0, EnginesCapacity = 2, Engines = 2,
        };

        [Header("Bouclier")]
        [Tooltip("0 = no shield.")]
        [Min(0f)] public float ShieldMax;
        [Tooltip("Shield points per second for each power unit.")]
        [Min(0f)] public float ShieldRechargePerPower = 1.5f;
        [Tooltip("Seconds a broken shield stays down.")]
        [Min(0f)] public float ShieldRebootSeconds = 6f;

        [Header("Armes")]
        public List<WeaponSpec> Weapons = new();

        class Baker : Baker<ShipAuthoring>
        {
            public override void Bake(ShipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var t = authoring.transform;
                var position = new double3(t.position.x, t.position.y, t.position.z);
                quaternion rotation = t.rotation;

                // LODGroup.size is the largest dimension of the ship's bounds.
                var lodGroup = GetComponent<LODGroup>();
                var radius = lodGroup != null ? lodGroup.size * 0.5f : 1f;

                AddComponent<Ship>(entity);
                AddComponent(entity, new ShipName { Value = new FixedString64Bytes(authoring.name) });
                AddComponent(entity, new ShipRadius { Value = radius });
                AddComponent(entity, new SimPosition { Value = position });
                AddComponent(entity, new SimPreviousPosition { Value = position });
                AddComponent(entity, new SimRotation { Value = rotation });
                AddComponent(entity, new SimPreviousRotation { Value = rotation });
                AddComponent(entity, new ShipVelocity());
                AddComponent(entity, new ShipMovementStats
                {
                    MaxSpeed = authoring.MaxSpeed,
                    Acceleration = authoring.Acceleration,
                    TurnRateRadians = math.radians(authoring.TurnRateDegrees),
                });
                AddComponent(entity, new FollowTarget());
                AddBuffer<ShipCommand>(entity);

                AddComponent(entity, new Space.Core.Combat.Team { Value = (byte)authoring.Team });
                AddComponent(entity, new Hull { Current = authoring.Hull, Max = authoring.Hull });
                AddComponent(entity, new AttackTarget());

                var p = authoring.Power;
                var grid = new PowerGrid
                {
                    Reactor = p.Reactor,
                    ShieldCapacity = authoring.ShieldMax > 0f ? p.ShieldCapacity : (byte)0,
                    EnginesCapacity = p.EnginesCapacity,
                };
                PowerMath.Allocate(ref grid, p.Shield, p.Engines);

                AddComponent(entity, new Shield
                {
                    Current = authoring.ShieldMax,
                    Max = authoring.ShieldMax,
                    RechargePerPower = authoring.ShieldRechargePerPower,
                    RebootSeconds = authoring.ShieldRebootSeconds,
                    Active = authoring.ShieldMax > 0f && grid.Shield > 0,
                });

                var weapons = AddBuffer<Weapon>(entity);
                foreach (var spec in authoring.Weapons)
                {
                    weapons.Add(new Weapon
                    {
                        Type = spec.Type,
                        ShieldDamage = spec.ShieldDamage,
                        Ammo = spec.Ammo,
                        OrderedTargetOnly = spec.OrderedTargetOnly,
                        Range = spec.Range,
                        Damage = spec.Damage,
                        Cooldown = spec.CooldownSeconds,
                        ProjectileSpeed = spec.ProjectileSpeed,
                        Spread = math.radians(spec.SpreadDegrees),
                        Mode = spec.Mode,
                        Control = WeaponControl.Auto,
                        PowerDraw = spec.PowerDraw,
                        SuperchargeDraw = spec.SuperchargeDraw,
                        SuperchargeDamageFactor = spec.SuperchargeDamageFactor,
                    });
                }

                // The weapons draw what is left after the shield and the engines, in order.
                WeaponPower.PowerUpInOrder(ref grid, weapons.AsNativeArray());
                AddComponent(entity, grid);
            }
        }
    }
}
