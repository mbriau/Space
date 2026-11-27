using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Space.Core.Equipment;

namespace Space.Core.Tests
{
    public class UpgradeTests
    {
        const string FastCapacitors = "up_fast_capacitors";
        const string FocusingLens = "up_focusing_lens";
        const string Overclock = "up_overclock";
        const string StableSupercharge = "up_stable_supercharge";
        const string Magazine = "up_extended_magazine";

        [Test]
        public void UpgradeIdsAreUniqueAndUpgradesAreNamed()
        {
            var ids = new HashSet<string>();
            foreach (var upgrade in UpgradeCatalog.All)
            {
                Assert.IsTrue(ids.Add(upgrade.Id), upgrade.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(upgrade.Name), upgrade.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(upgrade.Description), upgrade.Id);
                Assert.That(upgrade.Tier, Is.InRange(1, 3), upgrade.Id);
                Assert.IsNotEmpty(upgrade.Targets, upgrade.Id);
                Assert.IsNull(ItemCatalog.Get(upgrade.Id), "no id shared with an item");
            }
        }

        [Test]
        public void EveryUpgradeFitsSomeItem()
        {
            foreach (var upgrade in UpgradeCatalog.All)
                Assert.IsTrue(ItemCatalog.All.Any(item => UpgradeMath.Accepts(upgrade, item)), upgrade.Id);
        }

        [Test]
        public void UpgradesOnlyFitTheirKindOfEquipment()
        {
            var capacitors = UpgradeCatalog.Get(FastCapacitors);
            Assert.IsTrue(UpgradeMath.Accepts(capacitors, ItemCatalog.Get(ItemCatalog.StarterLaser)));
            Assert.IsFalse(UpgradeMath.Accepts(capacitors, ItemCatalog.Get(ItemCatalog.StarterCannon)));
            Assert.IsFalse(UpgradeMath.Accepts(capacitors, ItemCatalog.Get(ItemCatalog.StarterShield)));

            var supercharge = UpgradeCatalog.Get(StableSupercharge);
            Assert.IsTrue(UpgradeMath.Accepts(supercharge, ItemCatalog.Get(ItemCatalog.StarterLaser)), "the mining laser supercharges");
            Assert.IsFalse(UpgradeMath.Accepts(supercharge, ItemCatalog.Get("energy_pulse")), "no supercharge mode");

            var magazine = UpgradeCatalog.Get(Magazine);
            Assert.IsTrue(UpgradeMath.Accepts(magazine, ItemCatalog.Get(ItemCatalog.StarterCannon)));
            Assert.IsTrue(UpgradeMath.Accepts(magazine, ItemCatalog.Get("missile_hornet")));
            Assert.IsFalse(UpgradeMath.Accepts(magazine, ItemCatalog.Get(ItemCatalog.StarterLaser)), "unlimited shots");
        }

        [Test]
        public void ApplyingAnUpgradeChangesACopyOnly()
        {
            var laser = ItemCatalog.Get(ItemCatalog.StarterLaser);
            var upgraded = UpgradeMath.Apply(laser, new[] { UpgradeCatalog.Get(FastCapacitors) });
            Assert.AreEqual(6f * 0.8f, upgraded.Weapon.Cooldown, 1e-4f);
            Assert.AreEqual(6f, laser.Weapon.Cooldown, "the catalog item is untouched");
            Assert.AreEqual(laser.Weapon.Range, upgraded.Weapon.Range);
            Assert.AreEqual(laser.Id, upgraded.Id);
        }

        [Test]
        public void TwoUpgradesAddTheirEffects()
        {
            var laser = ItemCatalog.Get(ItemCatalog.StarterLaser);
            var upgraded = UpgradeMath.Apply(laser, new[] { UpgradeCatalog.Get(FastCapacitors), UpgradeCatalog.Get(Overclock) });
            Assert.AreEqual(6f * 0.8f, upgraded.Weapon.Cooldown, 1e-4f);
            Assert.AreEqual(laser.Weapon.ShieldDamage * 1.3f, upgraded.Weapon.ShieldDamage, 1e-3f);
            Assert.AreEqual(laser.Weapon.PowerDraw + 1, upgraded.Weapon.PowerDraw);
        }

        [Test]
        public void SuperchargeAndAmmoUpgradesKeepSensibleNumbers()
        {
            var laser = UpgradeMath.Apply(ItemCatalog.Get(ItemCatalog.StarterLaser), new[] { UpgradeCatalog.Get(StableSupercharge) });
            Assert.AreEqual(1, laser.Weapon.SuperchargeDraw, "2 - 1");
            Assert.AreEqual(1.6f + 0.3f, laser.Weapon.SuperchargeDamageFactor, 1e-4f);

            var cannon = UpgradeMath.Apply(ItemCatalog.Get(ItemCatalog.StarterCannon), new[] { UpgradeCatalog.Get(Magazine) });
            Assert.AreEqual(12, cannon.Weapon.Ammo, "8 x 1.5");
            var torpedo = UpgradeMath.Apply(ItemCatalog.Get("missile_black_sun"), new[] { UpgradeCatalog.Get(Magazine) });
            Assert.AreEqual(2, torpedo.Weapon.Ammo, "1 x 1.5, rounded up");
        }

        [Test]
        public void AnItemTakesTwoUpgradesThenOneMustBeReplaced()
        {
            var loadout = ShipLoadout.Starter();
            var laser = ItemCatalog.StarterLaser;
            Assert.IsTrue(loadout.InstallUpgrade(laser, FastCapacitors));
            Assert.IsFalse(loadout.CanInstall(laser, FastCapacitors), "the same upgrade twice");
            Assert.IsTrue(loadout.InstallUpgrade(laser, FocusingLens));
            Assert.AreEqual(2, loadout.UpgradeCount(laser));

            Assert.IsTrue(loadout.CanInstall(laser, Overclock));
            Assert.IsFalse(loadout.InstallUpgrade(laser, Overclock), "no free slot: a slot to replace is needed");
            Assert.IsTrue(loadout.InstallUpgrade(laser, Overclock, 0));
            CollectionAssert.AreEqual(new[] { Overclock, FocusingLens }, loadout.UpgradesOf(laser));
        }

        [Test]
        public void UpgradesNeedAnOwnedItemThatFits()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsFalse(loadout.InstallUpgrade("energy_pulse", FastCapacitors), "not owned");
            Assert.IsFalse(loadout.InstallUpgrade(ItemCatalog.StarterCannon, FastCapacitors), "wrong kind");
            Assert.IsTrue(loadout.CanInstallAnywhere(FastCapacitors));
            Assert.IsFalse(loadout.CanInstallAnywhere("up_seeker_heads"), "no missiles on the Cargo");
        }

        [Test]
        public void UpgradesCountInTheShipStatsAndFollowTheItemToTheHold()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsTrue(loadout.InstallUpgrade(ItemCatalog.StarterLaser, FastCapacitors));
            Assert.IsTrue(loadout.InstallUpgrade(ItemCatalog.StarterShield, "up_recharge_coils"));
            Assert.IsTrue(loadout.InstallUpgrade(ItemCatalog.StarterReactor, "up_regulator"));

            var stats = LoadoutMath.Compute(loadout);
            Assert.AreEqual(6f * 0.8f, stats.Weapons[0].Cooldown, 1e-4f);
            Assert.AreEqual(1.5f * 1.3f, stats.ShieldRechargePerPower, 1e-4f);
            Assert.AreEqual(9, stats.Power.Reactor);

            Assert.IsTrue(loadout.Unequip(SlotKind.Weapon, 0));
            Assert.AreEqual(6f * 0.8f, loadout.Resolve(ItemCatalog.StarterLaser).Weapon.Cooldown, 1e-4f, "still upgraded in the hold");
            Assert.IsTrue(loadout.EquipFromHold(ItemCatalog.StarterLaser, 0));
            Assert.AreEqual(6f * 0.8f, LoadoutMath.Compute(loadout).Weapons[0].Cooldown, 1e-4f);
        }

        [Test]
        public void ForgottenUpgradesNoLongerApply()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsTrue(loadout.InstallUpgrade(ItemCatalog.StarterLaser, FastCapacitors));
            Assert.AreEqual(4.8f, loadout.Resolve(ItemCatalog.StarterLaser).Weapon.Cooldown, 1e-4f);
            loadout.ForgetUpgrades(ItemCatalog.StarterLaser);
            Assert.IsNull(loadout.UpgradesOf(ItemCatalog.StarterLaser));
            Assert.AreSame(ItemCatalog.Get(ItemCatalog.StarterLaser), loadout.Resolve(ItemCatalog.StarterLaser));
        }

        // ------------------------------------------------------------------ rewards

        [Test]
        public void RewardsOfferAnItemAnUpgradeAndOneMore([Values(1u, 7u, 12345u, 987654321u)] uint seed)
        {
            var loadout = ShipLoadout.Starter();
            var offers = RewardGenerator.Roll(loadout, seed, 0);
            Assert.AreEqual(RewardGenerator.OfferCount, offers.Count);
            Assert.AreEqual(RewardKind.Item, offers[0].Kind);
            Assert.AreEqual(RewardKind.Upgrade, offers[1].Kind);
            Assert.AreEqual(offers.Count, offers.Select(o => o.Id).Distinct().Count());

            var categories = new HashSet<ItemCategory>();
            foreach (var offer in offers)
            {
                if (offer.Kind == RewardKind.Item)
                {
                    var item = ItemCatalog.Get(offer.Id);
                    Assert.IsNotNull(item, offer.Id);
                    Assert.IsFalse(item.Starter, offer.Id);
                    Assert.IsFalse(loadout.Owns(item.Id), offer.Id);
                    Assert.IsTrue(categories.Add(item.Category), "two items of one category");
                }
                else
                {
                    Assert.IsTrue(loadout.CanInstallAnywhere(offer.Id), offer.Id);
                }
            }
        }

        [Test]
        public void RewardsAreDeterministicForASeed()
        {
            var loadout = ShipLoadout.Starter();
            var a = RewardGenerator.Roll(loadout, 42u, 2).Select(o => o.Id).ToArray();
            var b = RewardGenerator.Roll(loadout, 42u, 2).Select(o => o.Id).ToArray();
            CollectionAssert.AreEqual(a, b);
            var differs = false;
            for (var seed = 43u; seed < 60u && !differs; seed++)
                differs = !a.SequenceEqual(RewardGenerator.Roll(loadout, seed, 2).Select(o => o.Id));
            Assert.IsTrue(differs);
        }

        [Test]
        public void RewardsNeverOfferAnOwnedItem()
        {
            var loadout = ShipLoadout.Starter();
            var items = new[] { ItemCatalog.Get("energy_pulse"), ItemCatalog.Get("shield_quick") };
            loadout.Hold.Add("energy_pulse");
            var offers = RewardGenerator.Roll(items, UpgradeCatalog.All, loadout, 5u, 0);
            Assert.IsTrue(offers.Where(o => o.Kind == RewardKind.Item).All(o => o.Id == "shield_quick"));
        }

        [Test]
        public void WithNothingToUpgradeRewardsAreItems()
        {
            var loadout = ShipLoadout.Starter();
            var offers = RewardGenerator.Roll(ItemCatalog.All, new[] { UpgradeCatalog.Get("up_seeker_heads") }, loadout, 9u, 0);
            Assert.AreEqual(RewardGenerator.OfferCount, offers.Count);
            Assert.IsTrue(offers.All(o => o.Kind == RewardKind.Item));
        }

        [Test]
        public void ShopLeavesOutOwnedItems()
        {
            var owned = new HashSet<string>(ItemCatalog.All.Where(i => i.Category == ItemCategory.Shield).Select(i => i.Id));
            for (var seed = 1u; seed < 20u; seed++)
            {
                var offers = ShopGenerator.Roll(ItemCatalog.All, seed, 150, 1, exclude: owned);
                Assert.IsFalse(offers.Any(o => owned.Contains(o.Id)), $"seed {seed}");
            }
        }
    }
}
