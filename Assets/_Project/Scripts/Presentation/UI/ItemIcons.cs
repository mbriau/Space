using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Equipment;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// Icons of the items, painted by code from their category and numbers: every icon is different
    /// and says something about the item (a longer barrel for a longer range, more power segments
    /// for a stronger reactor, a thicker shield crest for a stronger shield...). Placeholder art
    /// until the visual pass (M7), but readable at a glance.
    /// </summary>
    public static class ItemIcons
    {
        public const int Size = 96;

        static readonly Dictionary<string, Texture2D> Cache = new();
        static readonly Dictionary<ItemCategory, Texture2D> CategoryCache = new();

        public static Texture2D Get(ItemDefinition item)
        {
            if (item == null)
                return null;
            if (Cache.TryGetValue(item.Id, out var texture) && texture != null)
                return texture;
            texture = ToTexture(Paint(item), item.Id);
            Cache[item.Id] = texture;
            return texture;
        }

        /// <summary>Generic icon of a category (tabs, empty slots): its first item without the rarity frame.</summary>
        public static Texture2D Get(ItemCategory category)
        {
            if (CategoryCache.TryGetValue(category, out var texture) && texture != null)
                return texture;
            foreach (var item in ItemCatalog.InCategory(category))
            {
                texture = ToTexture(Paint(item, frame: false), category.ToString());
                break;
            }
            CategoryCache[category] = texture;
            return texture;
        }

        static Texture2D ToTexture(IconCanvas canvas, string name)
        {
            var texture = new Texture2D(canvas.Size, canvas.Size, TextureFormat.RGBA32, false)
            {
                name = $"Icon_{name}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(canvas.ToColor32());
            texture.Apply(false, false);
            return texture;
        }

        public static Color CategoryColor(ItemCategory category) => category switch
        {
            ItemCategory.Reactor => new Color(1f, 0.72f, 0.2f),
            ItemCategory.Shield => new Color(0.35f, 0.75f, 1f),
            ItemCategory.EnergyWeapon => new Color(1f, 0.35f, 0.55f),
            ItemCategory.PhysicalWeapon => new Color(0.78f, 0.8f, 0.85f),
            ItemCategory.Missile => new Color(1f, 0.45f, 0.2f),
            ItemCategory.Engine => new Color(0.45f, 0.6f, 1f),
            ItemCategory.Armor => new Color(0.6f, 0.78f, 0.55f),
            _ => new Color(0.4f, 0.95f, 0.85f),
        };

        public static Color TierColor(int tier) => tier switch
        {
            >= 3 => new Color(1f, 0.78f, 0.25f),
            2 => new Color(0.45f, 0.65f, 1f),
            _ => new Color(0.55f, 0.58f, 0.62f),
        };

        /// <summary>Paints an item's icon (public for the preview test).</summary>
        public static IconCanvas Paint(ItemDefinition item, bool frame = true)
        {
            var c = new IconCanvas(Size);
            const float s = Size;
            var color = CategoryColor(item.Category);

            // Background tile and rarity frame.
            var tile = IconCanvas.Box(new float2(s / 2f), new float2(s / 2f - 2f), 0f, 12f);
            c.FillGradient(tile, new Color(0.04f, 0.05f, 0.09f), new Color(0.11f, 0.13f, 0.2f), 0f, s);
            if (frame)
            {
                var tier = TierColor(item.Tier);
                c.Fill(IconCanvas.Outline(tile, item.Tier >= 3 ? 4f : 3f), tier);
                for (var i = 0; i < item.Tier; i++)
                    c.Fill(IconCanvas.Circle(new float2(s - 14f - i * 9f, s - 14f), 3f), tier);
            }

            switch (item.Category)
            {
                case ItemCategory.Reactor: PaintReactor(c, item, color); break;
                case ItemCategory.Shield: PaintShield(c, item, color); break;
                case ItemCategory.EnergyWeapon: PaintEnergyWeapon(c, item.Weapon, color, new float2(0.5f, 0.5f), 1f); break;
                case ItemCategory.PhysicalWeapon: PaintCannon(c, item.Weapon, color, new float2(0.5f, 0.5f), 1f); break;
                case ItemCategory.Missile: PaintMissiles(c, item.Weapon, color); break;
                case ItemCategory.Engine: PaintEngine(c, item, color); break;
                case ItemCategory.Armor: PaintArmor(c, item, color); break;
                default: PaintDrone(c, item, color); break;
            }
            return c;
        }

        static float2 P(float x, float y) => new float2(x, y) * Size;

        static Color Dim(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        // Reactor: a glowing core with one segment per power unit around it.
        static void PaintReactor(IconCanvas c, ItemDefinition item, Color color)
        {
            var center = P(0.5f, 0.47f);
            var unstable = item.HullBonus < 0f;
            var heavy = item.SpeedFactor < 1f;
            var core = Size * (0.11f + 0.006f * item.Power);

            c.Fill(IconCanvas.Circle(center, Size * 0.36f), new Color(0.13f, 0.12f, 0.1f));
            c.Fill(IconCanvas.Ring(center, Size * 0.36f, heavy ? 5f : 2.5f), Dim(color, 0.55f));

            var count = math.max(1, item.Power);
            for (var i = 0; i < count; i++)
            {
                var angle = math.PI * 2f * i / count + math.PI / 2f;
                var dir = new float2(math.cos(angle), math.sin(angle));
                c.Fill(IconCanvas.Box(center + dir * Size * 0.285f, new float2(Size * 0.045f, Size * 0.018f + 6f / count),
                    angle, 1.5f), color);
            }

            var coreColor = unstable ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.93f, 0.6f);
            c.Glow(IconCanvas.Circle(center, core), WithAlpha(coreColor, 0.9f), 6f);
            c.Fill(IconCanvas.Circle(center, core), coreColor);
            c.Fill(IconCanvas.Circle(center, core * 0.5f), Color.white);
            if (unstable)
            {
                // Cracks: the reactor weakens the hull.
                c.Fill(IconCanvas.Segment(center + P(-0.05f, 0.16f), center + P(0.04f, 0.27f), 2f), coreColor);
                c.Fill(IconCanvas.Segment(center + P(0.12f, -0.12f), center + P(0.24f, -0.15f), 2f), coreColor);
            }
        }

        // Shield: a crest whose border thickness follows strength, with one pip per power unit needed,
        // and an arc whose length follows the recharge speed.
        static void PaintShield(IconCanvas c, ItemDefinition item, Color color)
        {
            var w = 0.26f;
            var crest = IconCanvas.Polygon(P(0.5f - w, 0.76f), P(0.5f + w, 0.76f), P(0.5f + w, 0.46f), P(0.5f, 0.17f), P(0.5f - w, 0.46f));
            var strength = math.saturate((item.ShieldMax - 80f) / 280f);
            c.Glow(crest, WithAlpha(color, 0.35f), 5f);
            c.FillGradient(crest, Dim(color, 0.25f), Dim(color, 0.55f), Size * 0.17f, Size * 0.76f);
            c.Fill(IconCanvas.Outline(crest, 2f + strength * 7f), color);

            // Recharge: an arc across the crest; faster recharge = more chevrons.
            var chevrons = math.clamp((int)math.round(item.ShieldRechargePerPower * 1.5f), 1, 5);
            for (var i = 0; i < chevrons; i++)
            {
                var y = 0.62f - i * 0.075f;
                c.Fill(IconCanvas.Segment(P(0.4f, y), P(0.5f, y - 0.05f), 3f), Color.white);
                c.Fill(IconCanvas.Segment(P(0.5f, y - 0.05f), P(0.6f, y), 3f), Color.white);
            }

            // Power needed.
            for (var i = 0; i < item.PowerCapacity; i++)
                c.Fill(IconCanvas.Box(P(0.2f + i * 0.1f, 0.09f), new float2(Size * 0.035f, Size * 0.02f), 0f, 1f),
                    new Color(1f, 0.75f, 0.25f));
        }

        // Energy weapon: an emitter and its beam. Ion: rings of energy; laser: a straight beam
        // whose length follows range and thickness follows damage.
        static void PaintEnergyWeapon(IconCanvas c, WeaponStats w, Color color, float2 centre, float scale)
        {
            var ion = w.Type == DamageType.Ion;
            var beamColor = ion ? new Color(0.5f, 0.65f, 1f) : color;
            var reach = math.saturate((w.Range - 250f) / 550f);
            var power = math.saturate((w.ShieldDamage + w.Damage * 2f) / 220f);

            var origin = (centre + new float2(-0.27f, -0.2f) * scale) * Size;
            var dir = math.normalize(new float2(1f, 0.75f));
            var end = origin + dir * Size * scale * (0.32f + 0.32f * reach);
            var angle = math.atan2(dir.y, dir.x);

            // Emitter body.
            c.Fill(IconCanvas.Box(origin - dir * 6f * scale, new float2(14f, 9f + w.PowerDraw) * scale, angle, 3f), new Color(0.35f, 0.37f, 0.45f));
            c.Fill(IconCanvas.Box(origin + dir * 7f * scale, new float2(6f, 5f) * scale, angle, 1f), new Color(0.55f, 0.57f, 0.65f));

            if (ion)
            {
                var rings = 2 + (int)(power * 3f);
                for (var i = 0; i < rings; i++)
                {
                    var t = (i + 1f) / rings;
                    var p = math.lerp(origin, end, t);
                    var r = (4f + 10f * power) * scale * (0.6f + 0.4f * t);
                    c.Glow(IconCanvas.EllipseRing(p, new float2(r * 0.45f, r), angle, 1f), WithAlpha(beamColor, 0.6f), 2.5f);
                    c.Fill(IconCanvas.EllipseRing(p, new float2(r * 0.45f, r), angle, 2.5f), beamColor);
                }
            }
            else
            {
                var thickness = (2f + 7f * power) * scale;
                var beam = IconCanvas.Segment(origin + dir * 12f * scale, end, thickness);
                c.Glow(beam, WithAlpha(beamColor, 0.8f), 3f + thickness);
                c.Fill(beam, beamColor);
                c.Fill(IconCanvas.Segment(origin + dir * 12f * scale, end, thickness * 0.4f), Color.white);
                if (w.Damage >= 25f)
                    c.Glow(IconCanvas.Circle(end, thickness), WithAlpha(new Color(1f, 0.7f, 0.3f), 0.9f), 5f);
            }

            // Fast weapons show several short pulses behind the main one.
            if (w.Cooldown < 3f)
                for (var i = 1; i <= 2; i++)
                    c.Fill(IconCanvas.Circle(math.lerp(origin, end, 0.25f * i) + new float2(dir.y, -dir.x) * 9f * scale, 2f * scale), beamColor);
        }

        // Cannon: barrel length follows range, bore follows damage, shells show the ammunition.
        static void PaintCannon(IconCanvas c, WeaponStats w, Color color, float2 centre, float scale)
        {
            var reach = math.saturate((w.Range - 200f) / 650f);
            var calibre = math.saturate(w.Damage / 220f);
            var length = Size * scale * (0.3f + 0.3f * reach);
            var bore = Size * scale * (0.035f + 0.05f * calibre);
            var breech = (centre + new float2(-0.2f, 0.05f) * scale) * Size;
            var muzzle = breech + new float2(length, 0f);

            c.Fill(IconCanvas.Box(breech, new float2(13f, 11f + bore * 0.4f) * scale, 0f, 3f), new Color(0.3f, 0.32f, 0.36f));
            c.Fill(IconCanvas.Box((breech + muzzle) / 2f, new float2(length / 2f, bore), 0f, 1.5f), color);
            c.Fill(IconCanvas.Box(muzzle, new float2(3.5f * scale, bore + 2.5f * scale), 0f, 1f), Dim(color, 0.8f));
            if (w.SpreadDegrees >= 1.2f)
            {
                // Scatter: the barrel flares.
                c.Fill(IconCanvas.Polygon(muzzle + new float2(-2f, -bore), muzzle + new float2(9f, -bore - 6f),
                    muzzle + new float2(9f, bore + 6f), muzzle + new float2(-2f, bore)), Dim(color, 0.8f));
            }
            if (w.ProjectileSpeed >= 1000f)
            {
                // Rails along the barrel (electromagnetic).
                c.Fill(IconCanvas.Segment(breech + new float2(10f, bore + 4f), muzzle + new float2(0f, bore + 4f), 2f), new Color(0.5f, 0.8f, 1f));
                c.Fill(IconCanvas.Segment(breech + new float2(10f, -bore - 4f), muzzle + new float2(0f, -bore - 4f), 2f), new Color(0.5f, 0.8f, 1f));
            }

            // Shells under the gun: one per 2 rounds, up to 10.
            var shells = math.clamp((w.Ammo + 1) / 2, 1, 10);
            var shellColor = new Color(0.95f, 0.65f, 0.25f);
            var shellHeight = 4f + 6f * calibre;
            for (var i = 0; i < shells; i++)
            {
                var x = (0.15f + i * 0.072f) * Size;
                var y = 0.2f * Size;
                c.Fill(IconCanvas.Box(new float2(x, y), new float2(2.4f, shellHeight), 0f, 2.3f), shellColor);
                c.Fill(IconCanvas.Box(new float2(x, y - shellHeight + 1.5f), new float2(2.6f, 1.5f), 0f, 0f), Dim(shellColor, 0.6f));
            }
        }

        static void Rocket(IconCanvas c, float2 tail, float2 dir, float length, float radius, Color body, Color tip)
        {
            var normal = new float2(-dir.y, dir.x);
            var noseStart = tail + dir * length * 0.72f;
            var nose = tail + dir * length;
            c.Glow(IconCanvas.Circle(tail - dir * radius, radius * 0.9f), new Color(1f, 0.6f, 0.2f, 0.9f), radius * 1.6f);
            c.Fill(IconCanvas.Polygon(tail + normal * radius * 2f - dir * radius, tail + normal * radius, tail + dir * radius * 2.5f + normal * radius), Dim(body, 0.7f));
            c.Fill(IconCanvas.Polygon(tail - normal * radius * 2f - dir * radius, tail - normal * radius, tail + dir * radius * 2.5f - normal * radius), Dim(body, 0.7f));
            c.Fill(IconCanvas.Segment(tail, noseStart, radius * 2f), body);
            c.Fill(IconCanvas.Polygon(noseStart + normal * radius, nose, noseStart - normal * radius), tip);
        }

        // Missiles: one rocket per round (up to 8); the bigger the warhead, the bigger the rocket.
        static void PaintMissiles(IconCanvas c, ItemDefinition item, Color color)
        {
            var w = item.Weapon;
            var count = math.clamp(w.Ammo, 1, 8);
            var warhead = math.saturate((w.Damage + w.ShieldDamage) / 1100f);
            var dir = math.normalize(new float2(1f, 1f));
            var body = w.ShieldDamage > w.Damage ? new Color(0.55f, 0.75f, 1f) : new Color(0.85f, 0.86f, 0.9f);
            var tip = w.ShieldDamage > w.Damage ? new Color(0.4f, 0.6f, 1f) : color;

            if (count <= 2)
            {
                var length = Size * (0.45f + 0.2f * warhead);
                var radius = Size * (0.045f + 0.04f * warhead);
                for (var i = 0; i < count; i++)
                {
                    var offset = count == 1 ? 0f : (i == 0 ? -0.13f : 0.13f);
                    var tail = P(0.28f, 0.22f) + new float2(-dir.y, dir.x) * offset * Size;
                    Rocket(c, tail - dir * (count == 1 ? 0.02f * Size : 0f), dir, length, radius, body, tip);
                }
            }
            else
            {
                var columns = count <= 4 ? 2 : 4;
                var rows = (count + columns - 1) / columns;
                var length = Size * (count <= 4 ? 0.36f : 0.26f);
                var radius = Size * (count <= 4 ? 0.04f : 0.028f);
                var n = 0;
                for (var r = 0; r < rows && n < count; r++)
                for (var col = 0; col < columns && n < count; col++, n++)
                {
                    var tail = P(0.18f + col * (0.62f / columns), 0.14f + r * (0.66f / rows) * 0.6f);
                    Rocket(c, tail, new float2(0f, 1f), length, radius, body, tip);
                }
            }
            if (!w.OrderedTargetOnly && w.SpreadDegrees >= 1f)
                c.Fill(IconCanvas.Ring(P(0.8f, 0.8f), 7f, 2f), Dim(color, 0.8f));
            else if (w.OrderedTargetOnly)
            {
                // Guided: a target reticle.
                c.Fill(IconCanvas.Ring(P(0.8f, 0.78f), 8f, 2f), Color.white);
                c.Fill(IconCanvas.Circle(P(0.8f, 0.78f), 2f), Color.white);
            }
        }

        // Engine: nozzle width follows acceleration, flame length follows top speed, side thrusters
        // follow agility.
        static void PaintEngine(IconCanvas c, ItemDefinition item, Color color)
        {
            var speed = math.saturate((item.MaxSpeed - 35f) / 30f);
            var thrust = math.saturate((item.Acceleration - 2f) / 4f);
            var agility = math.saturate((item.TurnRateDegrees - 8f) / 24f);
            var top = 0.8f;
            var nozzleBottom = 0.55f;
            var halfTop = 0.1f + 0.02f * thrust;
            var halfBottom = 0.13f + 0.08f * thrust;

            c.Fill(IconCanvas.Box(P(0.5f, 0.84f), new float2(Size * 0.16f, Size * 0.06f), 0f, 3f), new Color(0.3f, 0.32f, 0.38f));
            var nozzle = IconCanvas.Polygon(P(0.5f - halfTop, top), P(0.5f + halfTop, top), P(0.5f + halfBottom, nozzleBottom), P(0.5f - halfBottom, nozzleBottom));
            c.FillGradient(nozzle, new Color(0.35f, 0.37f, 0.44f), new Color(0.6f, 0.62f, 0.7f), Size * nozzleBottom, Size * top);

            var flameLength = 0.18f + 0.3f * speed;
            var flame = IconCanvas.Polygon(P(0.5f - halfBottom * 0.85f, nozzleBottom), P(0.5f + halfBottom * 0.85f, nozzleBottom), P(0.5f, nozzleBottom - flameLength));
            c.Glow(flame, WithAlpha(color, 0.9f), 6f);
            c.Fill(flame, color);
            var core = IconCanvas.Polygon(P(0.5f - halfBottom * 0.4f, nozzleBottom), P(0.5f + halfBottom * 0.4f, nozzleBottom), P(0.5f, nozzleBottom - flameLength * 0.6f));
            c.Fill(core, new Color(0.9f, 0.95f, 1f));

            var thrusters = (int)math.round(agility * 3f);
            for (var i = 0; i < thrusters; i++)
            {
                var y = 0.74f - i * 0.07f;
                c.Fill(IconCanvas.Segment(P(0.25f, y), P(0.17f, y), 3f), WithAlpha(color, 0.9f));
                c.Fill(IconCanvas.Segment(P(0.75f, y), P(0.83f, y), 3f), WithAlpha(color, 0.9f));
            }

            for (var i = 0; i < item.PowerCapacity; i++)
                c.Fill(IconCanvas.Box(P(0.12f + i * 0.09f, 0.09f), new float2(Size * 0.032f, Size * 0.018f), 0f, 1f), new Color(1f, 0.75f, 0.25f));
        }

        // Armor: layered plates, more layers for more hull; light hulls show speed lines.
        static void PaintArmor(IconCanvas c, ItemDefinition item, Color color)
        {
            var layers = math.clamp((int)math.round((item.Hull - 250f) / 130f), 1, 6);
            for (var i = layers - 1; i >= 0; i--)
            {
                var offset = i * 0.045f;
                var plate = IconCanvas.Polygon(
                    P(0.25f + offset, 0.7f - offset), P(0.62f + offset, 0.78f - offset), P(0.78f + offset, 0.5f - offset),
                    P(0.6f + offset, 0.22f - offset), P(0.3f + offset, 0.28f - offset));
                var shade = 0.45f + 0.55f * (1f - (float)i / layers);
                c.Fill(plate, Dim(color, shade * 0.75f));
                c.Fill(IconCanvas.Outline(plate, 2f), Dim(color, shade));
            }
            // Rivets.
            c.Fill(IconCanvas.Circle(P(0.34f, 0.62f), 2.5f), new Color(0.85f, 0.88f, 0.85f));
            c.Fill(IconCanvas.Circle(P(0.6f, 0.68f), 2.5f), new Color(0.85f, 0.88f, 0.85f));
            c.Fill(IconCanvas.Circle(P(0.55f, 0.33f), 2.5f), new Color(0.85f, 0.88f, 0.85f));

            // Hull bar: its length follows the hull points.
            var hullShare = math.saturate(item.Hull / 900f);
            c.Fill(IconCanvas.Box(P(0.12f + 0.38f * hullShare, 0.9f), new float2(Size * 0.38f * hullShare, 2.5f), 0f, 1.5f), Dim(color, 1.2f));

            if (item.SpeedFactor > 1f)
                for (var i = 0; i < 3; i++)
                    c.Fill(IconCanvas.Segment(P(0.08f, 0.45f + i * 0.08f), P(0.2f, 0.45f + i * 0.08f), 2.5f), new Color(0.6f, 0.85f, 1f));
            else if (item.SpeedFactor < 0.95f)
                c.Fill(IconCanvas.Box(P(0.5f, 0.11f), new float2(Size * 0.3f * (1f - item.SpeedFactor) * 3f, 3f), 0f, 1.5f), new Color(1f, 0.5f, 0.3f));
        }

        // Drone: a small arrowhead ship whose size follows its hull, with its weapon on the nose and
        // speed lines behind it.
        static void PaintDrone(IconCanvas c, ItemDefinition item, Color color)
        {
            var size = 0.17f + 0.12f * math.saturate((item.DroneHull - 50f) / 130f);
            var centre = P(0.52f, 0.5f);
            var dir = math.normalize(new float2(1f, 0.55f));
            var normal = new float2(-dir.y, dir.x);
            var nose = centre + dir * Size * size;
            var left = centre - dir * Size * size * 0.8f + normal * Size * size * 0.8f;
            var right = centre - dir * Size * size * 0.8f - normal * Size * size * 0.8f;
            var notch = centre - dir * Size * size * 0.35f;

            var speed = math.saturate((item.DroneSpeed - 50f) / 120f);
            for (var i = 0; i < 1 + (int)(speed * 3f); i++)
            {
                var o = (i - 1f) * 7f;
                c.Fill(IconCanvas.Segment(notch - dir * 10f + normal * o, notch - dir * (18f + 18f * speed) + normal * o, 2f), WithAlpha(color, 0.6f));
            }

            var hull = IconCanvas.Polygon(nose, left, notch, right);
            c.Glow(hull, WithAlpha(color, 0.35f), 4f);
            c.Fill(hull, Dim(color, 0.65f));
            c.Fill(IconCanvas.Outline(hull, 2f), color);
            c.Fill(IconCanvas.Circle(centre, 3f), Color.white);

            var w = item.Weapon;
            var weaponColor = w.Type == DamageType.Ion ? new Color(0.5f, 0.65f, 1f)
                : w.Type == DamageType.Laser ? new Color(1f, 0.35f, 0.55f) : new Color(1f, 0.7f, 0.3f);
            var reach = 8f + 22f * math.saturate((w.Range - 250f) / 300f);
            c.Fill(IconCanvas.Segment(nose + dir * 4f, nose + dir * (4f + reach), w.IsEnergy ? 2.5f : 2f), weaponColor);
            if (w.Ammo > 0)
                c.Fill(IconCanvas.Circle(nose + dir * (8f + reach), 3f), weaponColor);
        }
    }
}
