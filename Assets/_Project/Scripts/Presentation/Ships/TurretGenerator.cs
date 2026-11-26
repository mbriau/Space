using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>Geometry of one turret: a fixed base and a head that turns around +Y.</summary>
    public sealed class TurretMeshes
    {
        public TurretKind Kind;
        public ShipMeshData Base;
        /// <summary>Head and barrels, in the head's own space (pivot at the origin, barrels along +Z).</summary>
        public ShipMeshData Head;
        /// <summary>Height of the head pivot above the hardpoint.</summary>
        public float PivotHeight;
        /// <summary>Muzzle positions in head space; shots leave from there, in turn.</summary>
        public readonly List<Vector3> Muzzles = new();
    }

    /// <summary>How a weapon item looks once fitted: the turret kind and its size.</summary>
    public static class TurretVisuals
    {
        public static TurretKind KindFor(ItemDefinition item)
        {
            var w = item.Weapon;
            switch (w.Type)
            {
                case DamageType.Missile: return TurretKind.Missile;
                case DamageType.Ion: return TurretKind.Ion;
                case DamageType.Laser: return TurretKind.Laser;
                default:
                    if (w.ProjectileSpeed >= 1000f) return TurretKind.Rail;
                    if (w.Damage >= 100f || w.Cooldown >= 8f) return TurretKind.Heavy;
                    return TurretKind.Twin;
            }
        }

        /// <summary>Bigger for rarer and more power-hungry weapons: 0.9 to about 1.3 of the hardpoint size.</summary>
        public static float SizeFactor(ItemDefinition item) =>
            Mathf.Clamp(0.9f + 0.1f * (item.Tier - 1) + 0.05f * (item.Weapon.PowerDraw - 2), 0.85f, 1.35f);
    }

    /// <summary>
    /// Turrets in the same chunky style as the hulls: a round pedestal, an armoured head with a sloped
    /// front and its weapon. Painted with the ship's palette so they belong to it.
    /// </summary>
    public static class TurretGenerator
    {
        static readonly Quaternion Up = Quaternion.Euler(-90f, 0f, 0f); // lathe axis (local Z) to +Y

        static Color Glowing(Color c, float a) => new(c.r, c.g, c.b, a);

        public static TurretMeshes Build(TurretKind kind, float size, in ShipPalette p)
        {
            var s = size;
            var t = new TurretMeshes { Kind = kind, Base = new ShipMeshData(), Head = new ShipMeshData() };

            // Pedestal: sunk a little into the deck so it sits on chamfered tops without a gap.
            var ped = new List<Vector2>
            {
                new(0f, -0.45f * s), new(0.36f * s, -0.45f * s), new(0.36f * s, 0f),
                new(0.46f * s, 0.04f * s), new(0.46f * s, 0.13f * s), new(0.38f * s, 0.18f * s), new(0f, 0.18f * s),
            };
            t.Base.AddLathe(Vector3.zero, Up, ped, new[] { p.Metal, p.Metal, p.Metal, p.Accent, p.Secondary, p.Secondary }, 16);
            t.PivotHeight = 0.18f * s;

            var h = t.Head;
            switch (kind)
            {
                case TurretKind.Twin:
                    Head(h, s, 0.4f, 0.19f, p);
                    foreach (var x in new[] { -0.14f, 0.14f })
                        t.Muzzles.Add(Barrel(h, new Vector3(x * s, 0.2f * s, 0.25f * s), 0.05f * s, 0.95f * s, p));
                    break;
                case TurretKind.Heavy:
                    Head(h, s, 0.46f, 0.25f, p);
                    t.Muzzles.Add(Barrel(h, new Vector3(0f, 0.24f * s, 0.3f * s), 0.09f * s, 1.25f * s, p));
                    break;
                case TurretKind.Rail:
                {
                    Head(h, s, 0.38f, 0.2f, p);
                    // Two flat rails with a glowing gap between them.
                    foreach (var x in new[] { -0.09f, 0.09f })
                        h.AddChamferBox(new Vector3(x * s, 0.22f * s, 0.85f * s), Quaternion.identity,
                            new Vector3(0.07f * s, 0.16f * s, 1.3f * s), 0.02f * s, p.Metal);
                    h.AddChamferBox(new Vector3(0f, 0.22f * s, 0.75f * s), Quaternion.identity,
                        new Vector3(0.1f * s, 0.06f * s, 1.05f * s), 0.01f * s, Glowing(p.Glow, 0.8f));
                    foreach (var z in new[] { 0.35f, 0.95f })
                        h.AddChamferBox(new Vector3(0f, 0.22f * s, z * s), Quaternion.identity,
                            new Vector3(0.3f * s, 0.22f * s, 0.08f * s), 0.02f * s, p.Accent);
                    t.Muzzles.Add(new Vector3(0f, 0.22f * s, 1.5f * s));
                    break;
                }
                case TurretKind.Laser:
                {
                    Head(h, s, 0.36f, 0.2f, p);
                    // Thick emitter ending in a glowing lens.
                    var prof = new List<Vector2>
                    {
                        new(0f, 0.2f * s), new(0.15f * s, 0.2f * s), new(0.15f * s, 0.6f * s),
                        new(0.2f * s, 0.62f * s), new(0.2f * s, 0.78f * s), new(0.13f * s, 0.8f * s), new(0f, 0.8f * s),
                    };
                    h.AddLathe(new Vector3(0f, 0.2f * s, 0f), Quaternion.identity, prof,
                        new[] { p.Metal, p.Metal, p.Accent, p.Accent, p.Accent, Glowing(new Color(1f, 0.35f, 0.5f), 1f) }, 14);
                    t.Muzzles.Add(new Vector3(0f, 0.2f * s, 0.82f * s));
                    break;
                }
                case TurretKind.Ion:
                {
                    Head(h, s, 0.38f, 0.2f, p);
                    var prof = new List<Vector2> { new(0f, 0.2f * s), new(0.09f * s, 0.2f * s), new(0.09f * s, 0.85f * s), new(0f, 0.85f * s) };
                    h.AddLathe(new Vector3(0f, 0.22f * s, 0f), Quaternion.identity, prof, new[] { p.Metal, p.Metal, Glowing(new Color(0.5f, 0.6f, 1f), 1f) }, 12);
                    // Glowing coils around the emitter.
                    foreach (var z in new[] { 0.4f, 0.55f, 0.7f })
                    {
                        var coil = new List<Vector2>
                        {
                            new(0.08f * s, z * s), new(0.17f * s, z * s), new(0.17f * s, (z + 0.06f) * s), new(0.08f * s, (z + 0.06f) * s),
                        };
                        h.AddLathe(new Vector3(0f, 0.22f * s, 0f), Quaternion.identity, coil,
                            new[] { p.Metal, Glowing(new Color(0.45f, 0.55f, 1f), 0.9f), p.Metal }, 12);
                    }
                    t.Muzzles.Add(new Vector3(0f, 0.22f * s, 0.88f * s));
                    break;
                }
                case TurretKind.Missile:
                {
                    // A box of tubes on a short arm instead of an armoured head.
                    h.AddChamferBox(new Vector3(0f, 0.12f * s, 0f), Quaternion.identity, new Vector3(0.3f * s, 0.24f * s, 0.4f * s), 0.05f * s, p.Metal);
                    h.AddChamferBox(new Vector3(0f, 0.36f * s, 0.05f * s), Quaternion.identity,
                        new Vector3(0.72f * s, 0.4f * s, 0.7f * s), 0.07f * s, p.Secondary, p.Accent);
                    for (var row = 0; row < 2; row++)
                    for (var col = 0; col < 3; col++)
                    {
                        var pos = new Vector3((col - 1) * 0.2f * s, (0.28f + row * 0.17f) * s, 0.41f * s);
                        h.AddChamferBox(pos, Quaternion.identity, new Vector3(0.12f * s, 0.12f * s, 0.04f * s), 0.02f * s, new Color(0.12f, 0.12f, 0.16f, 0f));
                        t.Muzzles.Add(pos + new Vector3(0f, 0f, 0.05f * s));
                    }
                    break;
                }
            }

            t.Base.ComputeOutlineNormals();
            t.Head.ComputeOutlineNormals();
            return t;
        }

        /// <summary>Armoured head: flat back, sloped front plate, accent band across the front.</summary>
        static void Head(ShipMeshData h, float s, float halfWidth, float halfHeight, in ShipPalette p)
        {
            var w = halfWidth * s;
            var hh = halfHeight * s;
            var c = hh * 0.4f;
            var y = hh;
            var rings = new List<Vector3[]>
            {
                ShipMeshData.Octagon(w * 0.9f, hh * 0.85f, c, c * 0.5f, -0.42f * s, y),
                ShipMeshData.Octagon(w, hh, c, c * 0.5f, -0.34f * s, y),
                ShipMeshData.Octagon(w, hh, c, c * 0.5f, 0.12f * s, y),
                ShipMeshData.Octagon(w * 0.85f, hh * 0.55f, c * 0.6f, c * 0.3f, 0.36f * s, y - hh * 0.4f),
            };
            h.AddLoft(rings, new[] { p.Secondary, p.Secondary, p.Accent });
        }

        /// <summary>Barrel along +Z with a sleeve at the root and a muzzle brake. Returns the muzzle.</summary>
        static Vector3 Barrel(ShipMeshData h, Vector3 root, float r, float length, in ShipPalette p)
        {
            var prof = new List<Vector2>
            {
                new(0f, 0f), new(r * 1.7f, 0f), new(r * 1.7f, length * 0.28f), new(r, length * 0.32f),
                new(r, length - r * 2.2f), new(r * 1.45f, length - r * 2.2f), new(r * 1.45f, length), new(r * 0.6f, length), new(0f, length),
            };
            h.AddLathe(root, Quaternion.identity, prof,
                new[] { p.Metal, p.Secondary, p.Secondary, p.Metal, p.Metal, p.Accent, p.Accent, new Color(0.1f, 0.1f, 0.12f, 0f) }, 10);
            return root + new Vector3(0f, 0f, length);
        }
    }
}
