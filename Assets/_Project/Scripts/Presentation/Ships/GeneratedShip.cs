using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// A weapon hardpoint on the deck, in ship space (+Z forward, +Y up). The turret built on it
    /// depends on the weapon fitted in the matching slot of the loadout.
    /// </summary>
    public readonly struct Hardpoint
    {
        public readonly Vector3 Position;
        /// <summary>Nominal turret size in metres (a standard turret base is about 0.9 of it across).</summary>
        public readonly float Size;

        public Hardpoint(Vector3 position, float size)
        {
            Position = position; Size = size;
        }
    }

    /// <summary>A nozzle: centre of its glowing throat, radius of the glow, exhaust towards -Z.</summary>
    public readonly struct EngineMount
    {
        public readonly Vector3 Position;
        public readonly float Radius;

        public EngineMount(Vector3 position, float radius)
        {
            Position = position; Radius = radius;
        }
    }

    /// <summary>Result of <see cref="ShipGenerator.Generate"/>: hull geometry plus the mounts the presentation dresses.</summary>
    public sealed class GeneratedShip
    {
        public ShipDesign Design;
        public ShipMeshData Hull;
        /// <summary>One per weapon slot, front to back.</summary>
        public readonly List<Hardpoint> Hardpoints = new();
        public readonly List<EngineMount> Engines = new();
        /// <summary>Hull bounds without the turrets.</summary>
        public Bounds Bounds;
        /// <summary>Rough volume of the holds, containers and pods, in cubic metres.</summary>
        public float CargoVolume;
    }
}
