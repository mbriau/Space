using NUnit.Framework;
using Space.Core.Equipment;
using Space.Presentation.Game;

namespace Space.Presentation.Tests
{
    public class RunStateTests
    {
        static RunState WonLevel(uint seed = 3u)
        {
            var run = new RunState();
            run.StartNewRun();
            run.BetweenLevels = true;
            run.RollRewards(seed);
            return run;
        }

        [Test]
        public void TakingAnItemFitsItAndClosesTheChoice()
        {
            var run = WonLevel();
            Assert.IsFalse(run.RewardClaimed);
            var offer = run.RewardOffers[0];
            Assert.AreEqual(RewardKind.Item, offer.Kind);
            Assert.IsTrue(run.TakeItemReward(0));
            Assert.IsTrue(run.Loadout.Owns(offer.Id));
            Assert.IsTrue(run.RewardClaimed);
            Assert.IsFalse(run.TakeItemReward(2), "only one reward");
        }

        [Test]
        public void TakingAnUpgradeFitsItOntoTheChosenItem()
        {
            var run = WonLevel();
            var upgradeId = run.RewardOffers[1].Id;
            string target = null;
            foreach (var id in run.Loadout.Owned())
                if (run.Loadout.CanInstall(id, upgradeId))
                    target = id;
            Assert.IsNotNull(target);
            Assert.IsFalse(run.TakeUpgradeReward(0, target), "offer 0 is an item");
            Assert.IsTrue(run.TakeUpgradeReward(1, target));
            CollectionAssert.Contains(run.Loadout.UpgradesOf(target), upgradeId);
            Assert.IsTrue(run.RewardClaimed);
        }

        [Test]
        public void SkippingPaysAFewCredits()
        {
            var run = WonLevel();
            run.SkipReward();
            Assert.AreEqual(RewardGenerator.SkipCredits, run.Credits);
            run.SkipReward();
            Assert.AreEqual(RewardGenerator.SkipCredits, run.Credits, "once");
        }

        [Test]
        public void NoShopBetweenLevelsWithoutAShopBeacon()
        {
            var run = WonLevel();
            run.Credits = 10000;
            run.RollShop(1u);
            Assert.IsFalse(run.CanBuy(ItemCatalog.Get(run.ShopOffers[0])));
            run.ShopOpen = true;
            Assert.IsTrue(run.CanBuy(ItemCatalog.Get(run.ShopOffers[0])));
        }
    }
}
