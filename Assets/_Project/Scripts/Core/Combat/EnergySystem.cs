using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Combat
{
    /// <summary>Shields run on the power their subsystem receives, once per tick before weapons fire.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ShipMovementSystem))]
    [UpdateBefore(typeof(WeaponSystem))]
    [BurstCompile]
    public partial struct EnergySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ShieldJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Tick = SystemAPI.GetSingleton<SimulationTime>().Tick,
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ShieldJob : IJobEntity
        {
            public float DeltaTime;
            public uint Tick;

            void Execute(in PowerGrid grid, ref Shield shield)
            {
                DamageModel.UpdateShield(ref shield, grid.Shield, Tick, DeltaTime);
            }
        }
    }
}
