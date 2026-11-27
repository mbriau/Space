using System.Collections.Generic;
using System.Globalization;
using Space.Core.Combat;
using Space.Core.Equipment;

namespace Space.Presentation.UI
{
    /// <summary>French labels and stat lines of the items, shared by the menus.</summary>
    public static class ItemText
    {

        public static string Category(ItemCategory category) => category switch
        {
            ItemCategory.Reactor => "Réacteur",
            ItemCategory.Shield => "Bouclier",
            ItemCategory.EnergyWeapon => "Arme à énergie",
            ItemCategory.PhysicalWeapon => "Arme physique",
            ItemCategory.Missile => "Missiles",
            ItemCategory.Engine => "Moteurs",
            ItemCategory.Armor => "Blindage",
            _ => "Drone",
        };

        public static string CategoryPlural(ItemCategory category) => category switch
        {
            ItemCategory.Reactor => "Réacteurs",
            ItemCategory.Shield => "Boucliers",
            ItemCategory.EnergyWeapon => "Armes à énergie",
            ItemCategory.PhysicalWeapon => "Armes physiques",
            ItemCategory.Missile => "Missiles",
            ItemCategory.Engine => "Moteurs",
            ItemCategory.Armor => "Blindages",
            _ => "Drones",
        };

        public static string Slot(SlotKind kind, int index) => kind switch
        {
            SlotKind.Reactor => "Réacteur",
            SlotKind.Shield => "Bouclier",
            SlotKind.Engine => "Moteurs",
            SlotKind.Armor => "Blindage",
            SlotKind.Weapon => $"Arme {index + 1}",
            _ => $"Drone {index + 1}",
        };

        public static string Tier(int tier) => tier switch
        {
            >= 3 => "Rare",
            2 => "Peu commun",
            _ => "Commun",
        };

        public static string Credits(int amount) => $"{amount.ToString(CultureInfo.InvariantCulture)} cr";

        /// <summary>French decimal comma, without culture data (not every platform ships it).</summary>
        static string N(float value, string format = "0") =>
            value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

        static string DamageKind(DamageType type) => type switch
        {
            DamageType.Ion => "ionique (bouclier seulement)",
            DamageType.Laser => "laser (bouclier puis coque)",
            DamageType.Kinetic => "obus (bloqués par le bouclier)",
            _ => "missile (traverse le bouclier en partie)",
        };

        /// <summary>The numbers that matter for the item, one line each.</summary>
        public static List<string> Stats(ItemDefinition item)
        {
            var lines = new List<string>();
            switch (item.Category)
            {
                case ItemCategory.Reactor:
                    lines.Add($"Puissance produite : {item.Power} unités");
                    break;
                case ItemCategory.Shield:
                    lines.Add($"Protection : {N(item.ShieldMax)} points");
                    lines.Add($"Puissance requise : {item.PowerCapacity} unités");
                    lines.Add($"Recharge : {N(item.ShieldRechargePerPower * item.PowerCapacity, "0.#")} pts/s à pleine puissance");
                    lines.Add($"Redémarrage après rupture : {N(item.ShieldRebootSeconds)} s");
                    break;
                case ItemCategory.Engine:
                    lines.Add($"Vitesse max : {N(item.MaxSpeed)} m/s");
                    lines.Add($"Accélération : {N(item.Acceleration, "0.#")} m/s²");
                    lines.Add($"Rotation : {N(item.TurnRateDegrees)}°/s");
                    lines.Add($"Puissance requise : {item.PowerCapacity} unités");
                    break;
                case ItemCategory.Armor:
                    lines.Add($"Coque : {N(item.Hull)} points");
                    break;
                case ItemCategory.Drone:
                    lines.Add($"Coque du drone : {N(item.DroneHull)} · vitesse {N(item.DroneSpeed)} m/s");
                    AddWeapon(lines, item.Weapon, showPower: false);
                    break;
                default:
                    AddWeapon(lines, item.Weapon, showPower: true);
                    break;
            }

            if (item.SpeedFactor > 1.001f)
                lines.Add($"Allège le vaisseau : vitesse +{N((item.SpeedFactor - 1f) * 100f)} %");
            else if (item.SpeedFactor < 0.999f)
                lines.Add($"Alourdit le vaisseau : vitesse −{N((1f - item.SpeedFactor) * 100f)} %");
            if (item.HullBonus > 0f)
                lines.Add($"Coque +{N(item.HullBonus)}");
            else if (item.HullBonus < 0f)
                lines.Add($"Coque −{N(-item.HullBonus)}");
            return lines;
        }

        static void AddWeapon(List<string> lines, WeaponStats w, bool showPower)
        {
            lines.Add($"Type : {DamageKind(w.Type)}");
            var damage = w.Type == DamageType.Ion ? $"{N(w.ShieldDamage)} au bouclier"
                : w.ShieldDamage > 0f ? $"{N(w.ShieldDamage)} au bouclier, {N(w.Damage)} à la coque"
                : $"{N(w.Damage)} à la coque";
            lines.Add($"Dégâts par tir : {damage}");
            lines.Add($"Portée : {N(w.Range)} m · tir toutes les {N(w.Cooldown, "0.#")} s");
            lines.Add(w.Ammo < 0 ? "Munitions : illimitées" : $"Munitions : {w.Ammo} par combat");
            var precision = w.SpreadDegrees <= 0.25f ? "très précis" : w.SpreadDegrees <= 0.6f ? "précis"
                : w.SpreadDegrees <= 1f ? "dispersé" : "très dispersé";
            var speed = w.ProjectileSpeed >= 1400f ? "projectiles rapides" : w.ProjectileSpeed >= 600f ? "projectiles moyens" : "projectiles lents";
            lines.Add($"{char.ToUpper(precision[0])}{precision.Substring(1)}, {speed}" + (w.OrderedTargetOnly ? ", sur ta cible désignée" : ""));
            if (showPower)
                lines.Add($"Puissance requise : {w.PowerDraw} unité{(w.PowerDraw > 1 ? "s" : "")}");
        }

        /// <summary>One short line that sums the item up (shop cards, slots).</summary>
        public static string Summary(ItemDefinition item) => item.Category switch
        {
            ItemCategory.Reactor => $"{item.Power} unités",
            ItemCategory.Shield => $"{N(item.ShieldMax)} pts · {item.PowerCapacity} u. · {N(item.ShieldRebootSeconds)} s",
            ItemCategory.Engine => $"{N(item.MaxSpeed)} m/s · {N(item.TurnRateDegrees)}°/s · {item.PowerCapacity} u.",
            ItemCategory.Armor => $"Coque {N(item.Hull)}",
            ItemCategory.Drone => $"Coque {N(item.DroneHull)} · {N(item.DroneSpeed)} m/s",
            _ => WeaponSummary(item.Weapon),
        };

        static string WeaponSummary(WeaponStats w)
        {
            var damage = w.Type == DamageType.Ion ? $"{N(w.ShieldDamage)} bouclier"
                : w.ShieldDamage > 0f ? $"{N(w.ShieldDamage)}/{N(w.Damage)}" : $"{N(w.Damage)} coque";
            var ammo = w.Ammo < 0 ? "" : $" · {w.Ammo} coups";
            return $"{damage} · {N(w.Range)} m · {N(w.Cooldown, "0.#")} s{ammo}";
        }

        /// <summary>How an item compares with what is fitted in its slot (shop), in a short line.</summary>
        public static string Compare(ItemDefinition candidate, ItemDefinition fitted)
        {
            if (fitted == null || candidate == null || fitted.Category != candidate.Category)
                return null;
            string Delta(float a, float b, string label, string unit = "", bool lowerIsBetter = false)
            {
                var d = a - b;
                if (System.Math.Abs(d) < 0.01f)
                    return null;
                var better = lowerIsBetter ? d < 0f : d > 0f;
                return $"{label} {(d > 0 ? "+" : "−")}{N(System.Math.Abs(d), "0.#")}{unit}{(better ? " ▲" : " ▼")}";
            }

            var parts = new List<string>();
            void Add(string s) { if (s != null) parts.Add(s); }
            switch (candidate.Category)
            {
                case ItemCategory.Reactor:
                    Add(Delta(candidate.Power, fitted.Power, "puissance"));
                    break;
                case ItemCategory.Shield:
                    Add(Delta(candidate.ShieldMax, fitted.ShieldMax, "protection"));
                    Add(Delta(candidate.PowerCapacity, fitted.PowerCapacity, "puissance", "", true));
                    Add(Delta(candidate.ShieldRebootSeconds, fitted.ShieldRebootSeconds, "redémarrage", " s", true));
                    break;
                case ItemCategory.Engine:
                    Add(Delta(candidate.MaxSpeed, fitted.MaxSpeed, "vitesse"));
                    Add(Delta(candidate.TurnRateDegrees, fitted.TurnRateDegrees, "rotation"));
                    Add(Delta(candidate.PowerCapacity, fitted.PowerCapacity, "puissance", "", true));
                    break;
                case ItemCategory.Armor:
                    Add(Delta(candidate.Hull, fitted.Hull, "coque"));
                    break;
                default:
                    Add(Delta(candidate.Weapon.Range, fitted.Weapon.Range, "portée", " m"));
                    Add(Delta(candidate.Weapon.Damage + candidate.Weapon.ShieldDamage, fitted.Weapon.Damage + fitted.Weapon.ShieldDamage, "dégâts"));
                    Add(Delta(candidate.Weapon.Cooldown, fitted.Weapon.Cooldown, "recharge", " s", true));
                    break;
            }
            Add(Delta(candidate.SpeedFactor * 100f, fitted.SpeedFactor * 100f, "vitesse", " %"));
            Add(Delta(candidate.HullBonus, fitted.HullBonus, "coque"));
            return parts.Count == 0 ? null : $"Par rapport à {fitted.Name} : " + string.Join(", ", parts);
        }
    
        // ------------------------------------------------------------------ upgrades

        /// <summary>"armes à énergie ou boucliers": what an upgrade fits, in lower case.</summary>
        public static string UpgradeTargets(UpgradeDefinition upgrade)
        {
            var names = new List<string>();
            foreach (var category in upgrade.Targets)
                names.Add(CategoryPlural(category).ToLowerInvariant());
            var text = string.Join(" ou ", names);
            if (upgrade.RequiresSupercharge)
                text += " avec surcharge";
            if (upgrade.RequiresAmmo)
                text += " à munitions limitées";
            return text;
        }

        /// <summary>Names of the upgrades fitted onto an item, or null if it has none.</summary>
        public static string Upgrades(ShipLoadout loadout, string itemId)
        {
            var slots = loadout.UpgradesOf(itemId);
            if (slots == null)
                return null;
            var names = new List<string>();
            foreach (var id in slots)
                if (UpgradeCatalog.Get(id) is { } upgrade)
                    names.Add(upgrade.Name);
            return names.Count == 0 ? null : string.Join(", ", names);
        }

        /// <summary>Short tag after an item name that carries upgrades: " [+2]".</summary>
        public static string UpgradeTag(ShipLoadout loadout, string itemId)
        {
            var count = loadout.UpgradeCount(itemId);
            return count > 0 ? $" [+{count}]" : "";
        }

        /// <summary>What an upgrade changes on an item, as a short line of differences.</summary>
        public static string UpgradeEffect(ItemDefinition current, ItemDefinition upgraded)
        {
            var comparison = Compare(upgraded, current);
            return comparison?.Replace($"Par rapport à {current.Name} : ", "");
        }
    }
}
