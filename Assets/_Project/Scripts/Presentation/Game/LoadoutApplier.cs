using Space.Core.Combat;
using Space.Core.Equipment;
using Space.Core.Ships;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Game
{
    /// <summary>Fits a ship entity with the numbers of a loadout (flagship) or of a drone item.</summary>
    public static class LoadoutApplier
    {
        public static void ApplyToFlagship(EntityManager em, Entity ship, in LoadoutStats stats)
        {
            em.SetComponentData(ship, new Hull { Current = stats.Hull, Max = stats.Hull });

            var grid = stats.Power;
            em.SetComponentData(ship, grid);

            if (em.HasComponent<Shield>(ship))
            {
                em.SetComponentData(ship, new Shield
                {
                    Current = stats.ShieldMax,
                    Max = stats.ShieldMax,
                    RechargePerPower = stats.ShieldRechargePerPower,
                    RebootSeconds = stats.ShieldRebootSeconds,
                    Active = stats.ShieldMax > 0f && grid.Shield > 0,
                });
            }

            var movement = em.GetComponentData<ShipMovementStats>(ship);
            movement.MaxSpeed = stats.MaxSpeed;
            movement.Acceleration = stats.Acceleration;
            movement.TurnRateRadians = math.radians(stats.TurnRateDegrees);
            em.SetComponentData(ship, movement);

            var weapons = em.GetBuffer<Weapon>(ship);
            weapons.Clear();
            foreach (var w in stats.Weapons)
                weapons.Add(LoadoutMath.ToWeapon(w));
        }

        public static void ApplyToDrone(EntityManager em, Entity drone, ItemDefinition item)
        {
            em.SetComponentData(drone, new Hull { Current = item.DroneHull, Max = item.DroneHull });

            var movement = em.GetComponentData<ShipMovementStats>(drone);
            if (movement.MaxSpeed > 0f)
            {
                // Keep the drone's handling, scaled to its new top speed.
                var scale = item.DroneSpeed / movement.MaxSpeed;
                movement.Acceleration *= scale;
            }
            movement.MaxSpeed = item.DroneSpeed;
            em.SetComponentData(drone, movement);

            // Drones have a small reactor: everything on the weapon and the engines.
            var energy = item.Weapon.IsEnergy;
            var grid = new PowerGrid
            {
                Reactor = 3,
                EnergyWeaponsCapacity = (byte)(energy ? 1 : 0),
                PhysicalWeaponsCapacity = (byte)(energy ? 0 : 1),
                EnginesCapacity = 2,
            };
            PowerMath.Allocate(ref grid, 0, energy ? 1 : 0, energy ? 0 : 1, 2);
            em.SetComponentData(drone, grid);

            var weapons = em.GetBuffer<Weapon>(drone);
            weapons.Clear();
            weapons.Add(LoadoutMath.ToWeapon(item.Weapon));
        }
    }
}
