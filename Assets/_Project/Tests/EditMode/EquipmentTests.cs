using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Space.Core.Combat;
using Space.Core.Equipment;

namespace Space.Core.Tests
{
    public class EquipmentTests
    {
        [Test]
        public void EveryCategoryHasEightItems()
        {
            for (var c = 0; c < ItemCategories.Count; c++)
                Assert.AreEqual(8, ItemCatalog.InCategory((ItemCategory)c).Count(), ((ItemCategory)c).ToString());
        }

        [Test]
        public void ItemIdsAreUniqueAndItemsAreNamed()
        {
            var ids = new HashSet<string>();
            foreach (var item in ItemCatalog.All)
            {
                Assert.IsTrue(ids.Add(item.Id), item.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Name), item.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Description), item.Id);
                Assert.That(item.Tier, Is.InRange(1, 3), item.Id);
            }
        }

        [Test]
        public void WeaponsAreInTheRightCategoryAndUsable()
        {
            foreach (var item in ItemCatalog.All.Where(i => ItemCategories.IsWeapon(i.Category) || i.Category == ItemCategory.Drone))
            {
                var w = item.Weapon;
                Assert.Greater(w.Range, 0f, item.Id);
                Assert.Greater(w.Cooldown, 0f, item.Id);
                Assert.Greater(w.ProjectileSpeed, 0f, item.Id);
                Assert.Greater(w.Damage + w.ShieldDamage, 0f, item.Id);
                Assert.Greater(w.PowerDraw, 0, item.Id);
                if (item.Category == ItemCategory.EnergyWeapon)
                    Assert.IsTrue(w.IsEnergy, item.Id);
                if (item.Category == ItemCategory.PhysicalWeapon)
                    Assert.AreEqual(DamageType.Kinetic, w.Type, item.Id);
                if (item.Category == ItemCategory.Missile)
                {
                    Assert.AreEqual(DamageType.Missile, w.Type, item.Id);
                    Assert.That(w.Ammo, Is.InRange(1, 8), item.Id + " : missiles must stay scarce");
                }
                if (!w.IsEnergy && item.Category != ItemCategory.Drone)
                    Assert.Greater(w.Ammo, 0, item.Id + " : physical weapons have limited ammunition");
            }
        }

        [Test]
        public void StarterLoadoutMatchesTheDuelCargo()
        {
            var stats = LoadoutMath.Compute(ShipLoadout.Starter());

            Assert.AreEqual(450f, stats.Hull);
            Assert.AreEqual(150f, stats.ShieldMax);
            Assert.AreEqual(1.5f, stats.ShieldRechargePerPower);
            Assert.AreEqual(10f, stats.ShieldRebootSeconds);
            Assert.AreEqual(45f, stats.MaxSpeed, 1e-4f);
            Assert.AreEqual(3f, stats.Acceleration, 1e-4f);
            Assert.AreEqual(15f, stats.TurnRateDegrees, 1e-4f);

            var grid = stats.Power;
            Assert.AreEqual(8, grid.Reactor);
            Assert.AreEqual(4, grid.ShieldCapacity);
            Assert.AreEqual(3, grid.EnergyWeaponsCapacity);
            Assert.AreEqual(2, grid.PhysicalWeaponsCapacity);
            Assert.AreEqual(3, grid.EnginesCapacity);
            Assert.AreEqual(3, grid.Shield);
            Assert.AreEqual(3, grid.EnergyWeapons);
            Assert.AreEqual(0, grid.PhysicalWeapons);
            Assert.AreEqual(2, grid.Engines);

            Assert.AreEqual(2, stats.Weapons.Count);
            Assert.AreEqual(DamageType.Laser, stats.Weapons[0].Type);
            Assert.AreEqual(55f, stats.Weapons[0].ShieldDamage);
            Assert.AreEqual(DamageType.Kinetic, stats.Weapons[1].Type);
            Assert.AreEqual(8, stats.Weapons[1].Ammo);
            Assert.AreEqual(0, stats.Drones.Count);
        }

        [Test]
        public void HeavyAndFragileEquipmentChangesSpeedAndHull()
        {
            var loadout = ShipLoadout.Starter();
            loadout.Hold.Add("reactor_overdrive"); // 12 power, speed x0.85, hull -50
            loadout.Hold.Add("armor_reactive");    // hull 780, speed x0.82
            Assert.IsTrue(loadout.EquipFromHold("reactor_overdrive"));
            Assert.IsTrue(loadout.EquipFromHold("armor_reactive"));

            var stats = LoadoutMath.Compute(loadout);
            Assert.AreEqual(12, stats.Power.Reactor);
            Assert.AreEqual(730f, stats.Hull);
            Assert.AreEqual(45f * 0.85f * 0.82f, stats.MaxSpeed, 1e-3f);
            CollectionAssert.Contains(loadout.Hold, ItemCatalog.StarterReactor);
            CollectionAssert.Contains(loadout.Hold, ItemCatalog.StarterArmor);
        }

        [Test]
        public void WeaponPowerDrawSetsTheSubsystemCapacities()
        {
            var loadout = ShipLoadout.Starter();
            loadout.Hold.Add("missile_hammer"); // physical, draw 2
            Assert.IsTrue(loadout.EquipFromHold("missile_hammer"));
            Assert.AreEqual("missile_hammer", loadout.Weapons[2], "goes to the empty weapon slot");

            var grid = LoadoutMath.Compute(loadout).Power;
            Assert.AreEqual(3, grid.EnergyWeaponsCapacity);
            Assert.AreEqual(4, grid.PhysicalWeaponsCapacity);
        }

        [Test]
        public void WithoutEnergyWeaponsTheOpeningPowerGoesToPhysicalWeapons()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsTrue(loadout.Unequip(SlotKind.Weapon, 0));
            var grid = LoadoutMath.Compute(loadout).Power;
            Assert.AreEqual(0, grid.EnergyWeapons);
            Assert.AreEqual(2, grid.PhysicalWeapons);
            Assert.LessOrEqual(grid.Allocated, grid.Reactor);
        }

        [Test]
        public void CoreSlotsCannotBeEmptied()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsFalse(loadout.Unequip(SlotKind.Reactor, 0));
            Assert.IsFalse(loadout.Unequip(SlotKind.Shield, 0));
            Assert.AreEqual(ItemCatalog.StarterReactor, loadout.Reactor);
        }

        [Test]
        public void EquippingAnItemNotInTheHoldFails()
        {
            var loadout = ShipLoadout.Starter();
            Assert.IsFalse(loadout.EquipFromHold("energy_plasma"));
        }

        [Test]
        public void ReplacingAWeaponSendsTheOldOneToTheHold()
        {
            var loadout = ShipLoadout.Starter();
            loadout.Hold.Add("energy_ionizer");
            Assert.IsTrue(loadout.EquipFromHold("energy_ionizer", 0));
            Assert.AreEqual("energy_ionizer", loadout.Weapons[0]);
            CollectionAssert.AreEqual(new[] { ItemCatalog.StarterLaser }, loadout.Hold);
        }

        [Test]
        public void DronesAreListedInTheStats()
        {
            var loadout = ShipLoadout.Starter();
            loadout.Hold.Add("drone_escort");
            loadout.Hold.Add("drone_heavy");
            Assert.IsTrue(loadout.EquipFromHold("drone_escort"));
            Assert.IsTrue(loadout.EquipFromHold("drone_heavy"));
            var drones = LoadoutMath.Compute(loadout).Drones;
            Assert.AreEqual(2, drones.Count);
            Assert.AreEqual("drone_heavy", drones[1].Id);
        }

        [Test]
        public void ShopOffersEightDistinctItemsWithVariety([Values(1u, 7u, 12345u, 987654321u)] uint seed)
        {
            var offers = ShopGenerator.Roll(ItemCatalog.All, seed, 150, 1);
            Assert.AreEqual(ShopGenerator.DefaultOfferCount, offers.Count);
            Assert.AreEqual(offers.Count, offers.Select(o => o.Id).Distinct().Count());
            Assert.IsFalse(offers.Any(o => o.Starter));
            foreach (var group in offers.GroupBy(o => o.Category))
                Assert.LessOrEqual(group.Count(), ShopGenerator.MaxPerCategory, group.Key.ToString());
        }

        [Test]
        public void ShopAlwaysShowsSomethingToSaveFor([Values(0, 150, 400)] int credits)
        {
            for (var seed = 1u; seed < 40u; seed++)
            {
                var offers = ShopGenerator.Roll(ItemCatalog.All, seed, credits, 0);
                Assert.GreaterOrEqual(offers.Count(o => o.Price > credits), ShopGenerator.MinUnaffordable, $"seed {seed}");
            }
        }

        [Test]
        public void ShopIsDeterministicForASeed()
        {
            var a = ShopGenerator.Roll(ItemCatalog.All, 42u, 150, 2).Select(o => o.Id).ToArray();
            var b = ShopGenerator.Roll(ItemCatalog.All, 42u, 150, 2).Select(o => o.Id).ToArray();
            var c = ShopGenerator.Roll(ItemCatalog.All, 43u, 150, 2).Select(o => o.Id).ToArray();
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void RarerItemsBecomeMoreLikelyAsTheRunProgresses()
        {
            Assert.Greater(ShopGenerator.Weight(1, 0), ShopGenerator.Weight(3, 0));
            Assert.Greater(ShopGenerator.Weight(3, 8), ShopGenerator.Weight(3, 0));
        }

        [Test]
        public void SellingPaysHalfThePrice()
        {
            Assert.AreEqual(110, ShopGenerator.SellPrice(ItemCatalog.Get("missile_hornet")));
            Assert.AreEqual(25, ShopGenerator.SellPrice(ItemCatalog.Get(ItemCatalog.StarterLaser)));
        }
    }
}
