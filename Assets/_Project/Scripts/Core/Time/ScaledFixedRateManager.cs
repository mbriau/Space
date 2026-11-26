using Unity.Core;
using Unity.Entities;

namespace Space.Core.Time
{
    /// <summary>
    /// Rate manager for the FixedStepSimulationSystemGroup that honours pause and time scale
    /// read from the <see cref="SimulationTime"/> singleton.
    /// </summary>
    public sealed class ScaledFixedRateManager : IRateManager
    {
        FixedStepAccumulator _accumulator;
        EntityQuery _timeQuery;
        bool _queryCreated;
        bool _didPushTime;
        bool _frameStarted;
        int _stepsLeft;

        public ScaledFixedRateManager(float step, int maxStepsPerFrame = 8)
        {
            _accumulator = new FixedStepAccumulator(step, maxStepsPerFrame);
        }

        public float Timestep
        {
            get => _accumulator.Step;
            set => _accumulator = new FixedStepAccumulator(value, _accumulator.MaxStepsPerFrame);
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            var world = group.World;
            if (_didPushTime)
            {
                world.PopTime();
                _didPushTime = false;
            }

            if (!_queryCreated)
            {
                _timeQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SimulationTime>());
                _queryCreated = true;
            }

            if (!_timeQuery.TryGetSingletonEntity<SimulationTime>(out var timeEntity))
                return false;

            var em = world.EntityManager;
            var time = em.GetComponentData<SimulationTime>(timeEntity);

            if (!_frameStarted)
            {
                _frameStarted = true;
                _stepsLeft = _accumulator.Advance(world.Time.DeltaTime * (double)time.EffectiveScale);
            }

            if (_stepsLeft > 0)
            {
                _stepsLeft--;
                time.Tick++;
                time.ElapsedTime += _accumulator.Step;
                em.SetComponentData(timeEntity, time);

                world.PushTime(new TimeData(time.ElapsedTime, _accumulator.Step));
                _didPushTime = true;
                return true;
            }

            // End of this frame's ticks: publish the interpolation factor for presentation.
            time.InterpolationAlpha = _accumulator.Alpha;
            em.SetComponentData(timeEntity, time);
            _frameStarted = false;
            return false;
        }
    }
}
