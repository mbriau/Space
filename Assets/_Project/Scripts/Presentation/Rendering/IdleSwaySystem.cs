using Space.Core.Combat;
using Space.Core.Ships;
using Unity.Entities;

namespace Space.Presentation.Rendering
{
    /// <summary>Gives every enemy ship its own idle sway (<see cref="IdleSway"/>), once.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SimToRenderTransformSystem))]
    public partial class IdleSwaySystem : SystemBase
    {
        EntityQuery _unswayed;

        protected override void OnCreate()
        {
            _unswayed = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Ship>(), ComponentType.ReadOnly<Team>(), ComponentType.ReadOnly<SimPosition>() },
                None = new[] { ComponentType.ReadOnly<IdleSway>() },
            });
            RequireForUpdate(_unswayed);
        }

        protected override void OnUpdate()
        {
            using var entities = _unswayed.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                if (EntityManager.GetComponentData<Team>(entity).Value == Team.Player)
                {
                    // Our side holds still: a zero sway marks it as seen.
                    EntityManager.AddComponentData(entity, new IdleSway());
                    continue;
                }
                var radius = EntityManager.HasComponent<ShipRadius>(entity) ? EntityManager.GetComponentData<ShipRadius>(entity).Value : 10f;
                EntityManager.AddComponentData(entity, CourseMath.SwayFor(radius, (uint)entity.Index));
            }
        }
    }
}
