using System.Collections.Generic;
using Space.Core.Equipment;
using static Space.Presentation.Ships.ShipPalette;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// The medium ships of the hangar: about the size of the Cargo (44 to 58 m), able to fight and to
    /// carry a good load. Each one combines different modules so the silhouettes stay distinct.
    /// </summary>
    public static class ShipDesigns
    {
        // Saturated, cartoon colours: main paint, contrast, stripes, machinery, panels, windows, engine glow.
        public static readonly ShipPalette Sunburst = new("Soleil", Hex(0xF6B828), Hex(0x2F7FC1), Hex(0xE9483B), Hex(0x3B4254), Hex(0xEFE7D3), Hex(0x9BE7FF), Hex(0x46D6FF));
        public static readonly ShipPalette Lagoon = new("Lagon", Hex(0x2EC4A6), Hex(0xF3EBDD), Hex(0xFF7A3D), Hex(0x2F3A4C), Hex(0xFFD166), Hex(0xB8F3FF), Hex(0x5FE1FF));
        public static readonly ShipPalette Ember = new("Braise", Hex(0xE0483E), Hex(0xF4C24F), Hex(0x2D3047), Hex(0x3A3E4F), Hex(0xF2EDE4), Hex(0xFFE7A8), Hex(0xFF9B3D));
        public static readonly ShipPalette Polar = new("Polaire", Hex(0xEEF1F5), Hex(0x2D63B5), Hex(0xF59E2B), Hex(0x343B49), Hex(0x9FB4CC), Hex(0x7FD6FF), Hex(0x59C3FF));
        public static readonly ShipPalette Lime = new("Citron vert", Hex(0x9BD13A), Hex(0x46506E), Hex(0xF2545B), Hex(0x2E3345), Hex(0xEDEFE0), Hex(0xD8FFB0), Hex(0x7DF9C8));
        public static readonly ShipPalette Grape = new("Raisin", Hex(0x7A5AD8), Hex(0xFFC857), Hex(0x2EC4B6), Hex(0x30304A), Hex(0xEAE6F7), Hex(0xFFC2F0), Hex(0xFF5CE1));
        public static readonly ShipPalette Tangerine = new("Mandarine", Hex(0xFF8A3D), Hex(0x1F9E89), Hex(0xF7F3E3), Hex(0x323848), Hex(0xFFD9A0), Hex(0x9EF0FF), Hex(0x4CE0FF));
        public static readonly ShipPalette Cobalt = new("Cobalt", Hex(0x2B6FD6), Hex(0xF2F4F7), Hex(0xFFCB2E), Hex(0x2C3142), Hex(0x8EC5FF), Hex(0xFFF2A8), Hex(0x66E3FF));
        public static readonly ShipPalette Bumble = new("Bourdon", Hex(0xFFCF2E), Hex(0x2B2D3A), Hex(0xFF5A36), Hex(0x3A3D4A), Hex(0xF4F1E8), Hex(0x9BE7FF), Hex(0x4FD8FF));
        public static readonly ShipPalette Coral = new("Corail", Hex(0xFF6B6B), Hex(0x2D3A5C), Hex(0xFFD93D), Hex(0x333849), Hex(0xF7F1E3), Hex(0x9EF0FF), Hex(0x4CE0FF));

        const string Laser = ItemCatalog.StarterLaser;
        const string MassDriver = ItemCatalog.StarterCannon;

        public static readonly IReadOnlyList<ShipDesign> All = new[]
        {
            new ShipDesign("mule", "Mule", "Cargo lourd, conteneurs sous la coque", 11, 48f,
                HullForm.Brick, CargoLayout.BellyContainers, BridgeLayout.Nose, EngineLayout.Pair, WingLayout.None,
                new[] { Laser, MassDriver, null }, Sunburst),
            new ShipDesign("bourdon", "Bourdon", "Transport rapide à nacelles latérales", 23, 46f,
                HullForm.Rounded, CargoLayout.SidePods, BridgeLayout.Nose, EngineLayout.Trio, WingLayout.Stubs,
                new[] { "physical_autocannon", Laser, "physical_autocannon" }, Bumble),
            new ShipDesign("caravelle", "Caravelle", "Porte-conteneurs, passerelle à l'arrière", 37, 56f,
                HullForm.Spine, CargoLayout.SpineRack, BridgeLayout.RearTower, EngineLayout.Quad, WingLayout.None,
                new[] { MassDriver, "physical_light", null }, Lagoon),
            new ShipDesign("brise-glace", "Brise-glace", "Escorteur cuirassé, soute centrale", 41, 50f,
                HullForm.Wedge, CargoLayout.Bay, BridgeLayout.Nose, EngineLayout.Pylons, WingLayout.Fins,
                new[] { "physical_howitzer", MassDriver, "energy_ionizer" }, Polar),
            new ShipDesign("catamaran", "Catamaran", "Double coque, conteneurs entre les deux", 53, 52f,
                HullForm.Twin, CargoLayout.BellyContainers, BridgeLayout.MidTower, EngineLayout.Pair, WingLayout.None,
                new[] { MassDriver, MassDriver, "missile_hornet" }, Tangerine),
            new ShipDesign("tortue", "Tortue", "Soute bombée, tourelle lourde", 67, 46f,
                HullForm.Rounded, CargoLayout.Bay, BridgeLayout.MidTower, EngineLayout.Block, WingLayout.None,
                new[] { "physical_railgun", "energy_heavy", null }, Lime),
            new ShipDesign("heron", "Héron", "Éclaireur armé, radiateurs déployés", 71, 44f,
                HullForm.Wedge, CargoLayout.SidePods, BridgeLayout.Nose, EngineLayout.Pair, WingLayout.Radiators,
                new[] { "energy_lance", "energy_pulse", null }, Cobalt),
            new ShipDesign("remorqueur", "Remorqueur", "Pousseur aux gros moteurs, conteneurs latéraux", 83, 50f,
                HullForm.Brick, CargoLayout.SideContainers, BridgeLayout.MidTower, EngineLayout.Block, WingLayout.Fins,
                new[] { "physical_mortar", MassDriver, null }, Ember,
                // The middle turret a little forward, the rear one in the middle of the rear deck.
                hardpointShifts: new[] { 0f, 0.05f, -0.15f }),
            new ShipDesign("baleine", "Baleine", "Gros porteur, soute ventrale", 97, 58f,
                HullForm.Rounded, CargoLayout.BellyContainers, BridgeLayout.RearTower, EngineLayout.Quad, WingLayout.Stubs,
                new[] { "missile_swarm", Laser, MassDriver }, Grape),
            new ShipDesign("colporteur", "Colporteur", "Marchand armé, conteneurs sur les flancs", 109, 47f,
                HullForm.Wedge, CargoLayout.SideContainers, BridgeLayout.Nose, EngineLayout.Pylons, WingLayout.Stubs,
                new[] { "energy_thermal", MassDriver, "missile_rockets" }, Coral),
        };

        /// <summary>Designs drawn in the game but not offered in the hangar (smaller pirate ships).</summary>
        public static readonly IReadOnlyList<ShipDesign> Extra = new[]
        {
            Find("heron").Resized("heron-chasseur", "Héron (chasseur)", 22f),
        };

        /// <summary>A design of the hangar or of <see cref="Extra"/> by id, or null.</summary>
        public static ShipDesign? Get(string id)
        {
            foreach (var design in All)
                if (design.Id == id)
                    return design;
            if (Extra != null)
                foreach (var design in Extra)
                    if (design.Id == id)
                        return design;
            return null;
        }

        static ShipDesign Find(string id)
        {
            foreach (var design in All)
                if (design.Id == id)
                    return design;
            throw new System.ArgumentException($"Design inconnu : {id}");
        }
    }
}
