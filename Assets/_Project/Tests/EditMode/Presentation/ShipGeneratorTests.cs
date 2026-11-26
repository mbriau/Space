using System.Linq;
using NUnit.Framework;
using Space.Core.Equipment;
using Space.Presentation.Ships;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class ShipGeneratorTests
    {
        static ShipDesign[] Designs => ShipDesigns.All.ToArray();

        [Test]
        public void TenDistinctMediumShips()
        {
            Assert.GreaterOrEqual(ShipDesigns.All.Count, 10);
            Assert.AreEqual(ShipDesigns.All.Count, ShipDesigns.All.Select(d => d.Id).Distinct().Count(), "identifiants en double");
            // Every design differs from the others by at least two modules.
            for (var i = 0; i < Designs.Length; i++)
            for (var j = i + 1; j < Designs.Length; j++)
            {
                var a = Designs[i];
                var b = Designs[j];
                var same = (a.Hull == b.Hull ? 1 : 0) + (a.Cargo == b.Cargo ? 1 : 0) + (a.Bridge == b.Bridge ? 1 : 0)
                           + (a.Engines == b.Engines ? 1 : 0) + (a.Wings == b.Wings ? 1 : 0);
                Assert.LessOrEqual(same, 3, $"{a.Id} et {b.Id} se ressemblent trop");
            }
        }

        [Test]
        public void HardpointShiftsMoveTheRemorqueurTurrets()
        {
            var tug = ShipDesigns.Get("remorqueur").Value;
            Assert.IsNotNull(tug.HardpointShifts, "le Remorqueur a des emplacements ajustés à la main");
            var plain = new ShipDesign(tug.Id, tug.DisplayName, tug.Role, tug.Seed, tug.Length, tug.Hull, tug.Cargo,
                tug.Bridge, tug.Engines, tug.Wings, tug.DefaultWeapons, tug.Palette);

            var moved = ShipGenerator.Generate(tug).Hardpoints;
            var original = ShipGenerator.Generate(plain).Hardpoints;
            Assert.AreEqual(original[0].Position, moved[0].Position, "l'avant ne bouge pas");
            Assert.Greater(moved[1].Position.z, original[1].Position.z, "le milieu avance");
            Assert.Less(moved[2].Position.z, original[2].Position.z - tug.Length * 0.1f, "l'arrière recule");
            Assert.Greater(moved[0].Position.z, moved[1].Position.z, "toujours de l'avant vers l'arrière");
            Assert.Greater(moved[1].Position.z, moved[2].Position.z);
        }

        [Test]
        public void ExtraDesignsAreFoundAndSmaller()
        {
            var fighter = ShipDesigns.Get("heron-chasseur");
            Assert.IsNotNull(fighter);
            var ship = ShipGenerator.Generate(fighter.Value);
            Assert.That(ship.Bounds.size.z, Is.InRange(fighter.Value.Length * 0.95f, fighter.Value.Length * 1.05f));
            Assert.AreEqual(ShipLoadout.WeaponSlots, ship.Hardpoints.Count);
            Assert.IsNull(ShipDesigns.Get("inconnu"));
        }

        [TestCaseSource(nameof(Designs))]
        public void ShipHasTheSizeOfItsDesign(ShipDesign design)
        {
            var ship = ShipGenerator.Generate(design);
            var size = ship.Bounds.size;
            Assert.That(size.z, Is.InRange(design.Length * 0.95f, design.Length * 1.05f), "longueur");
            Assert.Less(size.x, size.z * 0.7f, "trop large");
            Assert.Less(size.y, size.z * 0.6f, "trop haut");
            Assert.AreEqual(0f, ship.Bounds.center.x, 0.05f, "pas symétrique");
            Assert.Less(ship.Hull.TriangleCount, 12000, "trop de triangles pour un vaisseau moyen");
            Assert.Greater(ship.CargoVolume, 100f, "pas de soute");
        }

        [TestCaseSource(nameof(Designs))]
        public void MeshIsValidAndFacesOutward(ShipDesign design)
        {
            var m = ShipGenerator.Generate(design).Hull;
            Assert.AreEqual(m.VertexCount, m.Normals.Count);
            Assert.AreEqual(m.VertexCount, m.Colors.Count);
            Assert.AreEqual(m.VertexCount, m.OutlineNormals.Count);
            Assert.AreEqual(0, m.Indices.Count % 3);
            Assert.IsTrue(m.Indices.All(i => i >= 0 && i < m.VertexCount));
            Assert.IsTrue(m.Positions.All(p => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)));
            Assert.IsTrue(m.OutlineNormals.All(n => Mathf.Abs(n.magnitude - 1f) < 0.01f), "normales de contour non unitaires");

            // Closed pieces wound outward give a positive signed volume (and every triangle agrees
            // with its vertex normals).
            var volume = 0f;
            var disagree = 0;
            for (var i = 0; i < m.Indices.Count; i += 3)
            {
                var a = m.Positions[m.Indices[i]];
                var b = m.Positions[m.Indices[i + 1]];
                var c = m.Positions[m.Indices[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                var face = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(face, m.Normals[m.Indices[i]]) < 0f)
                    disagree++;
            }
            Assert.Greater(volume, 0f, "faces retournées");
            Assert.AreEqual(0, disagree, "triangles opposés à leur normale");
        }

        [TestCaseSource(nameof(Designs))]
        public void HardpointsAndEngines(ShipDesign design)
        {
            var ship = ShipGenerator.Generate(design);
            Assert.AreEqual(ShipLoadout.WeaponSlots, ship.Hardpoints.Count, "un emplacement de tourelle par arme");
            Assert.GreaterOrEqual(ship.Engines.Count, 2);
            foreach (var h in ship.Hardpoints)
            {
                Assert.Greater(h.Position.y, ship.Bounds.center.y - 0.1f * ship.Bounds.size.y, "tourelle sous le vaisseau");
                Assert.That(h.Position.z, Is.InRange(ship.Bounds.min.z, ship.Bounds.max.z));
            }
            // Turrets do not overlap.
            for (var i = 0; i < ship.Hardpoints.Count; i++)
            for (var j = i + 1; j < ship.Hardpoints.Count; j++)
                Assert.Greater(Vector3.Distance(ship.Hardpoints[i].Position, ship.Hardpoints[j].Position), ship.Hardpoints[i].Size, "tourelles superposées");
            foreach (var e in ship.Engines)
                Assert.Less(e.Position.z, ship.Bounds.center.z, "tuyère à l'avant");
        }

        [Test]
        public void GenerationIsDeterministic()
        {
            foreach (var design in Designs)
            {
                var a = ShipGenerator.Generate(design).Hull;
                var b = ShipGenerator.Generate(design).Hull;
                Assert.AreEqual(a.VertexCount, b.VertexCount, design.Id);
                for (var i = 0; i < a.VertexCount; i += 37)
                    Assert.AreEqual(a.Positions[i], b.Positions[i], design.Id);
            }
        }

        [Test]
        public void DefaultWeaponsExistAndFit()
        {
            foreach (var design in Designs)
            {
                Assert.AreEqual(ShipLoadout.WeaponSlots, design.DefaultWeapons.Length, design.Id);
                foreach (var id in design.DefaultWeapons.Where(id => id != null))
                {
                    var item = ItemCatalog.Get(id);
                    Assert.IsNotNull(item, $"{design.Id} : arme inconnue {id}");
                    Assert.IsTrue(ItemCategories.IsWeapon(item.Category), $"{design.Id} : {id} n'est pas une arme");
                }
            }
        }

        [Test]
        public void EveryWeaponHasATurret()
        {
            var weapons = ItemCatalog.All.Where(i => ItemCategories.IsWeapon(i.Category)).ToList();
            Assert.Greater(weapons.Count, 0);
            foreach (var item in weapons)
            {
                var kind = TurretVisuals.KindFor(item);
                var turret = TurretGenerator.Build(kind, 4f * TurretVisuals.SizeFactor(item), ShipDesigns.Sunburst);
                Assert.Greater(turret.Muzzles.Count, 0, item.Id);
                Assert.Greater(turret.Head.TriangleCount, 20, item.Id);
                Assert.Greater(turret.Base.TriangleCount, 20, item.Id);
            }

            Assert.AreEqual(TurretKind.Rail, TurretVisuals.KindFor(ItemCatalog.Get("physical_railgun")));
            Assert.AreEqual(TurretKind.Heavy, TurretVisuals.KindFor(ItemCatalog.Get("physical_howitzer")));
            Assert.AreEqual(TurretKind.Twin, TurretVisuals.KindFor(ItemCatalog.Get(ItemCatalog.StarterCannon)));
            Assert.AreEqual(TurretKind.Laser, TurretVisuals.KindFor(ItemCatalog.Get(ItemCatalog.StarterLaser)));
            Assert.AreEqual(TurretKind.Ion, TurretVisuals.KindFor(ItemCatalog.Get("energy_ionizer")));
            Assert.AreEqual(TurretKind.Missile, TurretVisuals.KindFor(ItemCatalog.Get("missile_hornet")));
            // Rarer, hungrier weapons get bigger turrets.
            Assert.Greater(TurretVisuals.SizeFactor(ItemCatalog.Get("energy_plasma")), TurretVisuals.SizeFactor(ItemCatalog.Get(ItemCatalog.StarterLaser)));
        }
    }
}
