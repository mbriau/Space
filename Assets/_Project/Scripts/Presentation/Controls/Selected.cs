using Unity.Entities;

namespace Space.Presentation.Controls
{
    /// <summary>Tag on the ship(s) the local player has selected. Presentation only: the simulation never reads it.</summary>
    public struct Selected : IComponentData { }
}
