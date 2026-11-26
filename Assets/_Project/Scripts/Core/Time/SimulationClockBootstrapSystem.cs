using Unity.Entities;

namespace Space.Core.Time
{
    /// <summary>
    /// Creates the <see cref="SimulationTime"/> singleton and installs the scaled fixed-rate
    /// manager on the FixedStepSimulationSystemGroup. All gameplay simulation lives in that group.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimulationClockBootstrapSystem : SystemBase
    {
        public const float TickRate = 20f;

        protected override void OnCreate()
        {
            var step = 1f / TickRate;

            var entity = EntityManager.CreateEntity(typeof(SimulationTime));
            EntityManager.SetName(entity, "SimulationTime");
            EntityManager.SetComponentData(entity, new SimulationTime
            {
                TickDuration = step,
                Paused = false,
                TimeScale = 1f,
            });

        }

        protected override void OnUpdate()
        {
            // Done on the first update rather than in OnCreate: the fixed-step group may not
            // exist yet while the world is still creating its systems.
            var fixedGroup = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            fixedGroup.RateManager = new ScaledFixedRateManager(1f / TickRate);
            Enabled = false;
        }
    }
}
