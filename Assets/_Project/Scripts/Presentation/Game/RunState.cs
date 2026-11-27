using System.Collections.Generic;
using Space.Core.Equipment;
using Space.Core.Sectors;

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

        /// <summary>How the ship was set up at the end of the last won level (power, weapon controls); null at the start.</summary>
        public ShipSettings ShipSettings;

        /// <summary>Offers of the shop (ids), and those already bought.</summary>
        public List<string> ShopOffers = new();
        public HashSet<string> ShopSold = new();

        /// <summary>The run is between two levels: equipment can be changed.</summary>
        public bool BetweenLevels;

        /// <summary>A shop is open (a shop beacon of the sector map, coming with the map). Buying and selling need one.</summary>
        public bool ShopOpen;

        /// <summary>The offers after the last won level; one can be taken, or all skipped.</summary>
        public List<RewardOffer> RewardOffers = new();
        public bool RewardClaimed = true;

        /// <summary>Sectors of phase 1, each a delivery contract.</summary>
        public const int SectorsInPhase = 3;

        /// <summary>The sector being crossed (0-based) and the way through it.</summary>
        public int SectorIndex;
        public SectorRun Sector;

        /// <summary>The exit of the sector has been won: the delivery is made.</summary>
        public bool SectorDone;

        public bool PhaseComplete => SectorDone && SectorIndex + 1 >= SectorsInPhase;

        public void StartNewRun()
        {
            RunIndex++;
            Credits = 0;
            LevelsCompleted = 0;
            Loadout = ShipLoadout.Starter();
            ShipSettings = null;
            ShopOffers.Clear();
            ShopSold.Clear();
            BetweenLevels = false;
            ShopOpen = false;
            RewardOffers.Clear();
            RewardClaimed = true;
            SectorIndex = 0;
            Sector = null;
            SectorDone = false;
        }

        /// <summary>Opens a new sector map, the ship at its start.</summary>
        public void StartSector(uint seed, int levelCount)
        {
            Sector = new SectorRun(SectorMapGenerator.Generate(seed, levelCount));
            SectorDone = false;
            ShopOpen = false;
        }

        /// <summary>Moves on to the next sector of the phase once the delivery is made.</summary>
        public bool NextSector(uint seed, int levelCount)
        {
            if (!SectorDone || PhaseComplete)
                return false;
            SectorIndex++;
            StartSector(seed, levelCount);
            return true;
        }

        public void RollShop(uint seed)
        {
            ShopOffers.Clear();
            ShopSold.Clear();
            var owned = new HashSet<string>(Loadout.Owned());
            foreach (var item in ShopGenerator.Roll(ItemCatalog.All, seed, Credits, LevelsCompleted, exclude: owned))
                ShopOffers.Add(item.Id);
        }

        public bool CanBuy(ItemDefinition item) =>
            item != null && BetweenLevels && ShopOpen && !ShopSold.Contains(item.Id) && !Loadout.Owns(item.Id) &&
            Credits >= item.Price;

        // ------------------------------------------------------------------ rewards

        public void RollRewards(uint seed, bool elite = false)
        {
            RewardOffers = RewardGenerator.Roll(Loadout, seed, LevelsCompleted, elite);
            RewardClaimed = RewardOffers.Count == 0;
        }

        bool TryGetOffer(int index, RewardKind kind, out RewardOffer offer)
        {
            offer = default;
            if (RewardClaimed || index < 0 || index >= RewardOffers.Count || RewardOffers[index].Kind != kind)
                return false;
            offer = RewardOffers[index];
            return true;
        }

        /// <summary>Takes an item offer: fitted right away if a weapon or drone slot is free, otherwise it goes to the hold.</summary>
        public bool TakeItemReward(int index)
        {
            if (!TryGetOffer(index, RewardKind.Item, out var offer))
                return false;
            var item = ItemCatalog.Get(offer.Id);
            if (item == null || Loadout.Owns(item.Id))
                return false;
            Loadout.Hold.Add(item.Id);
            if (Loadout.HasFreeSlotFor(item))
                Loadout.EquipFromHold(item.Id);
            RewardClaimed = true;
            return true;
        }

        /// <summary>Takes an upgrade offer and fits it onto an owned item (replacing the upgrade in <paramref name="slot"/> if given).</summary>
        public bool TakeUpgradeReward(int index, string itemId, int slot = -1)
        {
            if (!TryGetOffer(index, RewardKind.Upgrade, out var offer) || !Loadout.InstallUpgrade(itemId, offer.Id, slot))
                return false;
            RewardClaimed = true;
            return true;
        }

        public void SkipReward()
        {
            if (RewardClaimed)
                return;
            Credits += RewardGenerator.SkipCredits;
            RewardClaimed = true;
        }

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
            if (!BetweenLevels || !ShopOpen || !Loadout.Hold.Remove(id))
                return false;
            Loadout.ForgetUpgrades(id);
            Credits += ShopGenerator.SellPrice(ItemCatalog.Get(id));
            return true;
        }
    }
}
