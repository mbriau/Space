using Space.Core.Ships;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Combat
{
    /// <summary>Reactor regeneration, shield upkeep and recharge, once per tick before weapons fire.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ShipMovementSystem))]
    [UpdateBefore(typeof(WeaponSystem))]
    [BurstCompile]
    public partial struct EnergySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnergyJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct EnergyJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref Energy energy, ref Shield shield)
            {
                DamageModel.UpdateEnergy(ref energy, ref shield, DeltaTime);
            }
        }
    }
}
