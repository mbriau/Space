using System.Collections.Generic;
using Space.Core.Equipment;

namespace Space.Presentation.Game
{
    /// <summary>
    /// What a run keeps from one level to the next. Plain data so it can be saved later
    /// (save of the run in progress, M2).
    /// </summary>
    public sealed class RunState
    {
        public int RunIndex;
        public int Credits;
        public int LevelsCompleted;

        /// <summary>The player's ship equipment and hold.</summary>
        public ShipLoadout Loadout = ShipLoadout.Starter();

        /// <summary>Offers of the shop open between levels (ids), and those already bought.</summary>
        public List<string> ShopOffers = new();
        public HashSet<string> ShopSold = new();

        /// <summary>The run is between two levels: equipment can be changed and the shop is open.</summary>
        public bool BetweenLevels;

        public void StartNewRun()
        {
            RunIndex++;
            Credits = 0;
            LevelsCompleted = 0;
            Loadout = ShipLoadout.Starter();
            ShopOffers.Clear();
            ShopSold.Clear();
            BetweenLevels = false;
        }

        public void RollShop(uint seed)
        {
            ShopOffers.Clear();
            ShopSold.Clear();
            foreach (var item in ShopGenerator.Roll(ItemCatalog.All, seed, Credits, LevelsCompleted))
                ShopOffers.Add(item.Id);
        }

        public bool CanBuy(ItemDefinition item) =>
            item != null && BetweenLevels && !ShopSold.Contains(item.Id) && Credits >= item.Price;

        /// <summary>Buys an offer: it is fitted right away if a weapon or drone slot is free, otherwise it goes to the hold.</summary>
        public bool Buy(ItemDefinition item)
        {
            if (!CanBuy(item))
                return false;
            Credits -= item.Price;
            ShopSold.Add(item.Id);
            Loadout.Hold.Add(item.Id);
            if (Loadout.HasFreeSlotFor(item))
                Loadout.EquipFromHold(item.Id);
            return true;
        }

        public bool Sell(string id)
        {
            if (!BetweenLevels || !Loadout.Hold.Remove(id))
                return false;
            Credits += ShopGenerator.SellPrice(ItemCatalog.Get(id));
            return true;
        }
    }
}
