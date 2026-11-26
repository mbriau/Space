using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>Overall shape of the main hull.</summary>
    public enum HullForm
    {
        /// <summary>Boxy and blunt: a flying container.</summary>
        Brick,
        /// <summary>Tapers to a low, sloped nose.</summary>
        Wedge,
        /// <summary>Heavy chamfers and a bulging nose.</summary>
        Rounded,
        /// <summary>Two slim hulls joined by the cargo section.</summary>
        Twin,
        /// <summary>A narrow keel that carries everything else.</summary>
        Spine,
    }

    /// <summary>Where and how the cargo is carried. The cargo drives the silhouette as much as the hull.</summary>
    public enum CargoLayout
    {
        /// <summary>Grid of containers slung under the hull.</summary>
        BellyContainers,
        /// <summary>Containers stacked along both flanks.</summary>
        SideContainers,
        /// <summary>Cylindrical pods on short pylons.</summary>
        SidePods,
        /// <summary>Containers clamped all around a narrow keel.</summary>
        SpineRack,
        /// <summary>A bulging hold with big doors on the flanks.</summary>
        Bay,
    }

    public enum BridgeLayout
    {
        Nose,
        MidTower,
        RearTower,
    }

    public enum EngineLayout
    {
        /// <summary>Two nacelles side by side.</summary>
        Pair,
        /// <summary>One big central nacelle and two smaller ones.</summary>
        Trio,
        /// <summary>Two by two.</summary>
        Quad,
        /// <summary>Two nacelles held away from the hull on pylons.</summary>
        Pylons,
        /// <summary>One wide engine block with a row of nozzles.</summary>
        Block,
    }

    public enum WingLayout
    {
        None,
        /// <summary>Short stubby wings with a slight dihedral.</summary>
        Stubs,
        /// <summary>Wide, thin radiator panels.</summary>
        Radiators,
        /// <summary>A dorsal fin and two ventral fins.</summary>
        Fins,
    }

    /// <summary>Look of a turret, chosen from the weapon fitted on the hardpoint (<see cref="TurretVisuals"/>).</summary>
    public enum TurretKind
    {
        /// <summary>Two medium barrels: light, automatic and mass-driver cannons.</summary>
        Twin,
        /// <summary>One long, thick barrel: howitzer, mortar.</summary>
        Heavy,
        /// <summary>Two long rails: the railgun.</summary>
        Rail,
        /// <summary>A stubby emitter with a glowing lens.</summary>
        Laser,
        /// <summary>A short emitter wrapped in glowing coils.</summary>
        Ion,
        /// <summary>A box of launch tubes.</summary>
        Missile,
    }

    /// <summary>
    /// Bright, saturated colours for the cartoon look: a main paint, a contrasting one, an accent for
    /// stripes, a dark metal for machinery, a light panel colour, the window glow and the engine glow.
    /// </summary>
    public readonly struct ShipPalette
    {
        public readonly string Name;
        public readonly Color Primary, Secondary, Accent, Metal, Panel, Window, Glow;

        public ShipPalette(string name, Color primary, Color secondary, Color accent, Color metal, Color panel, Color window, Color glow)
        {
            Name = name;
            Primary = primary; Secondary = secondary; Accent = accent;
            Metal = metal; Panel = panel; Window = window; Glow = glow;
        }

        public static Color Hex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 0f);
    }

    /// <summary>
    /// One ship of the hangar: modular choices plus a seed for the proportions. The same design always
    /// gives the same ship (<see cref="ShipGenerator"/> is deterministic).
    /// </summary>
    public readonly struct ShipDesign
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Role;
        public readonly int Seed;
        /// <summary>Overall length in metres, nose to nozzles.</summary>
        public readonly float Length;
        public readonly HullForm Hull;
        public readonly CargoLayout Cargo;
        public readonly BridgeLayout Bridge;
        public readonly EngineLayout Engines;
        public readonly WingLayout Wings;
        /// <summary>Weapon fitted on each hardpoint by default (item ids of the catalogue, null = empty).</summary>
        public readonly string[] DefaultWeapons;
        public readonly ShipPalette Palette;

        public ShipDesign(string id, string displayName, string role, int seed, float length,
            HullForm hull, CargoLayout cargo, BridgeLayout bridge, EngineLayout engines, WingLayout wings,
            string[] defaultWeapons, ShipPalette palette)
        {
            Id = id; DisplayName = displayName; Role = role; Seed = seed; Length = length;
            Hull = hull; Cargo = cargo; Bridge = bridge; Engines = engines; Wings = wings;
            DefaultWeapons = defaultWeapons; Palette = palette;
        }
    }
}
