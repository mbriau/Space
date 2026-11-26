using Space.Core.Time;
using Space.Presentation.UI;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace Space.Presentation.Input
{
    /// <summary>
    /// Temporary keyboard controls for the simulation clock (M0):
    /// Space = pause, 1 / 2 / 3 = speed x1 / x2 / x4.
    /// Will move to an Input Actions asset with the rest of the controls in M1.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TimeControlsSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SimulationTime>();
        }

        protected override void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || UiFocus.MenuOpen)
                return;

            var time = SystemAPI.GetSingletonRW<SimulationTime>();

            if (keyboard.spaceKey.wasPressedThisFrame)
                time.ValueRW.Paused = !time.ValueRO.Paused;
            if (keyboard.digit1Key.wasPressedThisFrame)
                time.ValueRW.TimeScale = 1f;
            if (keyboard.digit2Key.wasPressedThisFrame)
                time.ValueRW.TimeScale = 2f;
            if (keyboard.digit3Key.wasPressedThisFrame)
                time.ValueRW.TimeScale = 4f;
        }
    }
}
