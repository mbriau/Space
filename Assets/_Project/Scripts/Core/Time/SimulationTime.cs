using Unity.Entities;

namespace Space.Core.Time
{
    /// <summary>
    /// Singleton describing the state of the simulation clock.
    /// The simulation advances in fixed ticks; presentation interpolates between them.
    /// </summary>
    public struct SimulationTime : IComponentData
    {
        /// <summary>Fixed duration of one simulation tick, in seconds.</summary>
        public float TickDuration;

        /// <summary>When true, no simulation tick runs. Orders can still be queued.</summary>
        public bool Paused;

        /// <summary>Speed multiplier (1, 2, 4...). Ignored while paused.</summary>
        public float TimeScale;

        /// <summary>Number of ticks simulated since the start of the game.</summary>
        public uint Tick;

        /// <summary>Simulated time elapsed since the start of the game, in seconds.</summary>
        public double ElapsedTime;

        /// <summary>Position between the last tick and the next one, in [0, 1). Used to interpolate rendering.</summary>
        public float InterpolationAlpha;

        public float EffectiveScale => Paused ? 0f : TimeScale;
    }
}
