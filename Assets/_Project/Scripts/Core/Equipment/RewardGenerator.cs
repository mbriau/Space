using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Space.Core.Equipment
{
    public enum RewardKind : byte
    {
        Item = 0,
        Upgrade = 1,
    }

    /// <summary>One of the offers after a won level: a new item, or an upgrade for an item the player owns.</summary>
    [Serializable]
    public struct RewardOffer
    {
        public RewardKind Kind;
        public string Id;
    }

    /// <summary>
    /// Rolls the offers after a won level (pick one, or skip for a few credits): the first is an
    /// item, the second an upgrade that fits something the player owns, the third either. Items
    /// the player already owns are never offered, and two items never share a category. Weighted
    /// towards the rarity that suits the progress of the run, like the shop. Deterministic for a seed.
    /// </summary>
    public static class RewardGenerator
    {
        public const int OfferCount = 3;

        /// <summary>Credits for passing on every offer.</summary>
        public const int SkipCredits = 40;

        /// <summary>An elite fight's offers are drawn as if the run were this many levels further: rarer things.</summary>
        public const int EliteProgressBonus = 4;

        public static List<RewardOffer> Roll(ShipLoadout loadout, uint seed, int levelsCompleted, bool elite = false,
            int count = OfferCount) =>
            Roll(ItemCatalog.All, UpgradeCatalog.All, loadout, seed, levelsCompleted + (elite ? EliteProgressBonus : 0), count);

        public static List<RewardOffer> Roll(IReadOnlyList<ItemDefinition> items, IReadOnlyList<UpgradeDefinition> upgrades,
            ShipLoadout loadout, uint seed, int levelsCompleted, int count = OfferCount)
        {
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);

            var itemPool = new List<ItemDefinition>();
            foreach (var item in items)
                if (!item.Starter && !loadout.Owns(item.Id))
                    itemPool.Add(item);

            var upgradePool = new List<UpgradeDefinition>();
            foreach (var upgrade in upgrades)
                if (loadout.CanInstallAnywhere(upgrade.Id))
                    upgradePool.Add(upgrade);

            var offers = new List<RewardOffer>(count);
            var categories = new HashSet<ItemCategory>();
            for (var i = 0; i < count; i++)
            {
                var kind = i == 0 ? RewardKind.Item : i == 1 ? RewardKind.Upgrade
                    : random.NextBool() ? RewardKind.Item : RewardKind.Upgrade;
                if (kind == RewardKind.Item && itemPool.Count == 0)
                    kind = RewardKind.Upgrade;
                if (kind == RewardKind.Upgrade && upgradePool.Count == 0)
                    kind = RewardKind.Item;

                if (kind == RewardKind.Item)
                {
                    // A category not offered yet, if any is left.
                    var fresh = itemPool.FindAll(item => !categories.Contains(item.Category));
                    var choices = fresh.Count > 0 ? fresh : itemPool;
                    if (choices.Count == 0)
                        break;
                    var pick = choices[Draw(ref random, choices, item => item.Tier, levelsCompleted)];
                    itemPool.Remove(pick);
                    categories.Add(pick.Category);
                    offers.Add(new RewardOffer { Kind = RewardKind.Item, Id = pick.Id });
                }
                else
                {
                    if (upgradePool.Count == 0)
                        break;
                    var index = Draw(ref random, upgradePool, upgrade => upgrade.Tier, levelsCompleted);
                    offers.Add(new RewardOffer { Kind = RewardKind.Upgrade, Id = upgradePool[index].Id });
                    upgradePool.RemoveAt(index);
                }
            }
            return offers;
        }

        static int Draw<T>(ref Unity.Mathematics.Random random, List<T> pool, Func<T, int> tier, int levelsCompleted)
        {
            var total = 0f;
            foreach (var entry in pool)
                total += ShopGenerator.Weight(tier(entry), levelsCompleted);
            var roll = random.NextFloat(total);
            for (var i = 0; i < pool.Count; i++)
            {
                roll -= ShopGenerator.Weight(tier(pool[i]), levelsCompleted);
                if (roll <= 0f)
                    return i;
            }
            return math.max(0, pool.Count - 1);
        }
    }
}
