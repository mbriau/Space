using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>Computes the world position of every formation slot from its leader, before ships move.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [UpdateBefore(typeof(ShipMovementSystem))]
    [BurstCompile]
    public partial struct FormationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SlotJob
            {
                Positions = SystemAPI.GetComponentLookup<SimPosition>(true),
                Rotations = SystemAPI.GetComponentLookup<SimRotation>(true),
                Velocities = SystemAPI.GetComponentLookup<ShipVelocity>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct SlotJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<SimPosition> Positions;
            [ReadOnly] public ComponentLookup<SimRotation> Rotations;
            [ReadOnly] public ComponentLookup<ShipVelocity> Velocities;

            void Execute(in FormationSlot slot, ref FollowTarget follow)
            {
                var leader = slot.Leader;
                if (leader == Entity.Null || !Positions.HasComponent(leader))
                {
                    follow.Valid = false;
                    return;
                }

                var rotation = Rotations.HasComponent(leader) ? Rotations[leader].Value : quaternion.identity;
                follow.Point = Positions[leader].Value + (double3)math.mul(rotation, slot.Offset);
                follow.Velocity = Velocities.HasComponent(leader) ? Velocities[leader].Linear : float3.zero;
                follow.Valid = true;
            }
        }
    }
}
