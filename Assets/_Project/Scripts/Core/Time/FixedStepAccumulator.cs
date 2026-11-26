using Unity.Mathematics;

namespace Space.Core.Time
{
    /// <summary>
    /// Pure fixed-step accumulator: converts scaled frame time into a number of ticks to run.
    /// Kept free of any ECS dependency so it can be unit tested.
    /// </summary>
    public struct FixedStepAccumulator
    {
        public readonly float Step;
        public readonly int MaxStepsPerFrame;

        double _accumulated;

        public FixedStepAccumulator(float step, int maxStepsPerFrame)
        {
            Step = step;
            MaxStepsPerFrame = maxStepsPerFrame;
            _accumulated = 0;
        }

        /// <summary>Fraction of a tick accumulated but not yet simulated, in [0, 1).</summary>
        public float Alpha => (float)math.saturate(_accumulated / Step);

        /// <summary>
        /// Adds scaled frame time and returns how many ticks to run this frame.
        /// When the frame is too long (hitch or high speed), extra backlog is dropped
        /// rather than causing a spiral of death.
        /// </summary>
        public int Advance(double scaledDeltaTime)
        {
            if (scaledDeltaTime <= 0)
                return 0;

            _accumulated += scaledDeltaTime;
            var steps = (int)math.floor(_accumulated / Step);
            if (steps > MaxStepsPerFrame)
            {
                steps = MaxStepsPerFrame;
                _accumulated = 0;
            }
            else
            {
                _accumulated -= steps * (double)Step;
            }
            return steps;
        }
    }
}
