using System.Collections.Generic;
using Unity.Mathematics;

namespace Space.Core.Equipment
{
    /// <summary>
    /// Rolls the offers of a shop: a few items, a little at random, weighted towards the rarity that
    /// suits the progress of the run, never more than two of a category, and always including items
    /// the player cannot afford yet (something to aim for). Items in <c>exclude</c> (those the player
    /// already owns) are left out. Deterministic for a given seed.
    /// </summary>
    public static class ShopGenerator
    {
        public const int DefaultOfferCount = 8;
        public const int MaxPerCategory = 2;
        public const int MinUnaffordable = 2;

        /// <summary>Rarity the shop leans towards after <paramref name="levelsCompleted"/> levels.</summary>
        public static float TargetTier(int levelsCompleted) => math.min(3f, 1f + levelsCompleted / 4f);

        /// <summary>Relative chance of an item of <paramref name="tier"/> to be drawn.</summary>
        public static float Weight(int tier, int levelsCompleted)
        {
            var distance = math.abs(tier - TargetTier(levelsCompleted));
            return math.max(0.08f, 1f - 0.45f * distance);
        }

        /// <summary>What the shop pays for an item from the hold: half its price (starting equipment: 25).</summary>
        public static int SellPrice(ItemDefinition item) => item == null ? 0 : item.Starter ? 25 : item.Price / 2;

        public static List<ItemDefinition> Roll(IReadOnlyList<ItemDefinition> catalog, uint seed, int credits,
            int levelsCompleted, int count = DefaultOfferCount, ICollection<string> exclude = null)
        {
            var random = new Random(seed == 0 ? 1u : seed);
            var pool = new List<ItemDefinition>();
            foreach (var item in catalog)
                if (!item.Starter && (exclude == null || !exclude.Contains(item.Id)))
                    pool.Add(item);

            var offers = new List<ItemDefinition>(count);
            var perCategory = new int[ItemCategories.Count];

            while (offers.Count < count && pool.Count > 0)
            {
                var index = Draw(ref random, pool, levelsCompleted, perCategory);
                if (index < 0)
                    break;
                var item = pool[index];
                pool.RemoveAt(index);
                offers.Add(item);
                perCategory[(int)item.Category]++;
            }

            // Something to save for: make sure a couple of offers are out of reach.
            var unaffordable = 0;
            foreach (var item in offers)
                if (item.Price > credits)
                    unaffordable++;
            while (unaffordable < math.min(MinUnaffordable, offers.Count))
            {
                var expensive = new List<ItemDefinition>();
                foreach (var item in pool)
                    if (item.Price > credits && perCategory[(int)item.Category] < MaxPerCategory)
                        expensive.Add(item);
                if (expensive.Count == 0)
                    break;

                // Replace the cheapest affordable offer.
                var replace = -1;
                for (var i = 0; i < offers.Count; i++)
                    if (offers[i].Price <= credits && (replace < 0 || offers[i].Price < offers[replace].Price))
                        replace = i;
                if (replace < 0)
                    break;

                var pick = expensive[random.NextInt(expensive.Count)];
                perCategory[(int)offers[replace].Category]--;
                pool.Add(offers[replace]);
                pool.Remove(pick);
                offers[replace] = pick;
                perCategory[(int)pick.Category]++;
                unaffordable++;
            }

            offers.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : a.Price.CompareTo(b.Price));
            return offers;
        }

        static int Draw(ref Random random, List<ItemDefinition> pool, int levelsCompleted, int[] perCategory)
        {
            var total = 0f;
            foreach (var item in pool)
                if (perCategory[(int)item.Category] < MaxPerCategory)
                    total += Weight(item.Tier, levelsCompleted);
            if (total <= 0f)
                return -1;

            var roll = random.NextFloat(total);
            for (var i = 0; i < pool.Count; i++)
            {
                if (perCategory[(int)pool[i].Category] >= MaxPerCategory)
                    continue;
                roll -= Weight(pool[i].Tier, levelsCompleted);
                if (roll <= 0f)
                    return i;
            }
            for (var i = pool.Count - 1; i >= 0; i--)
                if (perCategory[(int)pool[i].Category] < MaxPerCategory)
                    return i;
            return -1;
        }
    }
}
