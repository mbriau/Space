using System.Collections.Generic;

namespace Space.Core.Equipment
{
    /// <summary>
    /// Every upgrade that exists. As with items, several trade something for something else; the
    /// pure gains are rarer. First-pass numbers, to be balanced in playtests.
    /// </summary>
    public static class UpgradeCatalog
    {
        static List<UpgradeDefinition> _all;
        static Dictionary<string, UpgradeDefinition> _byId;

        public static IReadOnlyList<UpgradeDefinition> All
        {
            get
            {
                if (_all == null)
                    Build();
                return _all;
            }
        }

        public static UpgradeDefinition Get(string id)
        {
            if (_byId == null)
                Build();
            return id != null && _byId.TryGetValue(id, out var upgrade) ? upgrade : null;
        }

        const ItemCategory Energy = ItemCategory.EnergyWeapon;
        const ItemCategory Physical = ItemCategory.PhysicalWeapon;
        const ItemCategory Missile = ItemCategory.Missile;

        static void Build()
        {
            _all = new List<UpgradeDefinition>(24);

            // Energy weapons.
            Add("up_fast_capacitors", 1, "Condensateurs rapides", "Les tirs se rechargent plus vite.", Energy)
                .CooldownFactor = 0.8f;
            var lens = Add("up_focusing_lens", 1, "Lentilles focalisantes", "Le faisceau porte plus loin et se disperse moins.", Energy);
            lens.RangeFactor = 1.2f;
            lens.SpreadFactor = 0.7f;
            var overclock = Add("up_overclock", 2, "Surcadençage", "Des tirs bien plus puissants, mais l'arme demande une unité de puissance de plus.", Energy);
            overclock.DamageFactor = 1.3f;
            overclock.ShieldDamageFactor = 1.3f;
            overclock.PowerDrawDelta = 1;
            var stable = Add("up_stable_supercharge", 2, "Surcharge stabilisée", "La surcharge frappe plus fort et coûte une unité de moins.", Energy);
            stable.RequiresSupercharge = true;
            stable.SuperchargeDamageBonus = 0.3f;
            stable.SuperchargeDrawDelta = -1;

            // Physical weapons and missiles.
            var magazine = Add("up_extended_magazine", 1, "Chargeur élargi", "La moitié de munitions en plus à chaque niveau.", Physical, Missile);
            magazine.RequiresAmmo = true;
            magazine.AmmoFactor = 1.5f;
            var piercing = Add("up_piercing_rounds", 1, "Obus perforants", "Plus de dégâts à la coque, un peu moins au bouclier.", Physical);
            piercing.DamageFactor = 1.2f;
            piercing.ShieldDamageFactor = 0.85f;
            var rails = Add("up_accelerator_rails", 2, "Rails d'accélération", "Des projectiles plus rapides et plus précis.", Physical);
            rails.ProjectileSpeedFactor = 1.3f;
            rails.SpreadFactor = 0.8f;
            Add("up_seeker_heads", 1, "Têtes chercheuses", "Des missiles plus rapides, qui laissent moins de temps pour les esquiver.", Missile)
                .ProjectileSpeedFactor = 1.3f;
            var warheads = Add("up_heavy_warheads", 2, "Charges renforcées", "Plus de dégâts, mais un rechargement plus lent.", Missile);
            warheads.DamageFactor = 1.25f;
            warheads.ShieldDamageFactor = 1.25f;
            warheads.CooldownFactor = 1.15f;

            // Shields.
            Add("up_recharge_coils", 1, "Bobines de recharge", "Chaque unité de puissance recharge plus vite le bouclier.", ItemCategory.Shield)
                .ShieldRechargeFactor = 1.3f;
            Add("up_hot_reboot", 1, "Redémarrage à chaud", "Le bouclier tombé redémarre bien plus vite.", ItemCategory.Shield)
                .ShieldRebootFactor = 0.6f;
            var capacitor = Add("up_shield_capacitor", 2, "Condensateur de bouclier", "Un bouclier plus épais, un peu plus lourd.", ItemCategory.Shield);
            capacitor.ShieldMaxFactor = 1.25f;
            capacitor.SpeedFactor = 0.97f;

            // Engines.
            var injectors = Add("up_injectors", 1, "Injecteurs", "Le vaisseau accélère et tourne plus vite.", ItemCategory.Engine);
            injectors.AccelerationFactor = 1.25f;
            injectors.TurnRateFactor = 1.15f;
            var afterburner = Add("up_afterburner", 2, "Postcombustion", "Plus de vitesse de pointe, mais les moteurs acceptent une unité de puissance de plus.", ItemCategory.Engine);
            afterburner.MaxSpeedFactor = 1.15f;
            afterburner.PowerCapacityDelta = 1;

            // Armor.
            var plates = Add("up_extra_plates", 1, "Plaques supplémentaires", "Plus de coque, un peu plus lourd.", ItemCategory.Armor);
            plates.HullFactor = 1.15f;
            plates.SpeedFactor = 0.97f;

            // Reactor.
            Add("up_regulator", 2, "Régulateur", "Le réacteur produit une unité de puissance de plus.", ItemCategory.Reactor)
                .PowerDelta = 1;

            _byId = new Dictionary<string, UpgradeDefinition>(_all.Count);
            foreach (var upgrade in _all)
                _byId.Add(upgrade.Id, upgrade);
        }

        static UpgradeDefinition Add(string id, int tier, string name, string description, params ItemCategory[] targets)
        {
            var upgrade = new UpgradeDefinition { Id = id, Tier = tier, Name = name, Description = description, Targets = targets };
            _all.Add(upgrade);
            return upgrade;
        }
    }
}
