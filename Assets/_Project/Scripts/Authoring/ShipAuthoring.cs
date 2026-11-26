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
            public DamageType Type = DamageType.Kinetic;
            [Min(0f)] public float Range = 300f;

            [Tooltip("Damage to the hull (laser, cannon, missile).")]
            [Min(0f)] public float Damage = 5f;

            [Tooltip("Damage to the shield (ion, missile).")]
            [Min(0f)] public float ShieldDamage;

            [Tooltip("Energy used per shot (ion, laser).")]
            [Min(0f)] public float EnergyCost;

            [Tooltip("Shots available; -1 = unlimited.")]
            public int Ammo = -1;

            [Tooltip("Fires only at the target ordered by the player (missiles).")]
            public bool OrderedTargetOnly;

            [Min(0.05f)] public float CooldownSeconds = 1f;
            [Min(1f)] public float ProjectileSpeed = 1000f;
            [Min(0f)] public float SpreadDegrees = 0.8f;
            public FireMode Mode = FireMode.Normal;
        }

        [Header("Déplacement")]
        [Min(0f)] public float MaxSpeed = 50f;
        [Min(0f)] public float Acceleration = 10f;
        [Min(0f)] public float TurnRateDegrees = 45f;

        [Header("Combat")]
        public TeamId Team = TeamId.Player;
        [Min(1f)] public float Hull = 100f;

        [Header("Réacteur et bouclier")]
        [Min(0f)] public float EnergyMax = 50f;
        [Min(0f)] public float EnergyRegenPerSecond = 5f;
        [Tooltip("0 = no shield.")]
        [Min(0f)] public float ShieldMax;
        [Min(0f)] public float ShieldRechargePerSecond = 5f;
        [Min(0f)] public float ShieldUpkeepPerSecond = 2f;
        [Min(0f)] public float ShieldRechargeCostPerPoint = 1f;

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
                AddComponent(entity, new Energy
                {
                    Current = authoring.EnergyMax,
                    Max = authoring.EnergyMax,
                    RegenPerSecond = authoring.EnergyRegenPerSecond,
                });
                AddComponent(entity, new Shield
                {
                    Current = authoring.ShieldMax,
                    Max = authoring.ShieldMax,
                    RechargePerSecond = authoring.ShieldRechargePerSecond,
                    UpkeepPerSecond = authoring.ShieldUpkeepPerSecond,
                    RechargeCostPerPoint = authoring.ShieldRechargeCostPerPoint,
                    Online = authoring.ShieldMax > 0f,
                    Active = authoring.ShieldMax > 0f,
                });

                var weapons = AddBuffer<Weapon>(entity);
                foreach (var spec in authoring.Weapons)
                {
                    weapons.Add(new Weapon
                    {
                        Type = spec.Type,
                        ShieldDamage = spec.ShieldDamage,
                        EnergyCost = spec.EnergyCost,
                        Ammo = spec.Ammo,
                        OrderedTargetOnly = spec.OrderedTargetOnly,
                        Range = spec.Range,
                        Damage = spec.Damage,
                        Cooldown = spec.CooldownSeconds,
                        ProjectileSpeed = spec.ProjectileSpeed,
                        Spread = math.radians(spec.SpreadDegrees),
                        Mode = spec.Mode,
                        Enabled = true,
                    });
                }
            }
        }
    }
}
