using Space.Core.Combat;
using Space.Core.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Shield bubbles: faint when the shield is weak, brighter when charged, a short flash on each
    /// impact, invisible when the shield is down or switched off. One per-instance colour per bubble,
    /// so thousands of ships stay a single batch.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct ShieldVisualSystem : ISystem
    {
        /// <summary>Opacity of a bubble with an empty but active shield, and added at full charge.</summary>
        const float MinAlpha = 0.08f;
        const float ChargeAlpha = 0.24f;

        /// <summary>Extra opacity just after an impact, fading over <see cref="FlashTicks"/>.</summary>
        const float FlashAlpha = 0.6f;
        const float FlashTicks = 6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<SimulationTime>();
            new BubbleJob
            {
                // Fractional tick so the flash fades smoothly between simulation ticks.
                Now = time.Tick + time.InterpolationAlpha,
                Shields = SystemAPI.GetComponentLookup<Shield>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct BubbleJob : IJobEntity
        {
            public float Now;
            [ReadOnly] public ComponentLookup<Shield> Shields;

            void Execute(in ShieldVisual visual, ref URPMaterialPropertyBaseColor color)
            {
                var alpha = 0f;
                if (Shields.HasComponent(visual.Ship))
                {
                    var shield = Shields[visual.Ship];
                    if (shield.IsUp && shield.Max > 0f)
                    {
                        alpha = MinAlpha + ChargeAlpha * math.saturate(shield.Current / shield.Max);
                        var sinceHit = Now - shield.LastHitTick;
                        if (shield.LastHitTick > 0 && sinceHit >= 0f && sinceHit < FlashTicks)
                            alpha += FlashAlpha * (1f - sinceHit / FlashTicks);
                    }
                }

                color.Value = new float4(visual.Color.xyz, math.saturate(alpha));
            }
        }
    }
}
