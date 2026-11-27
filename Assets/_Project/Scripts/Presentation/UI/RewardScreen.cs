using System;
using System.Collections.Generic;
using Space.Core.Equipment;
using Space.Presentation.Game;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// The choice after a won level (provisional IMGUI, drawn by <see cref="LevelRunner"/>): one
    /// offer out of three, or skip for a few credits. An upgrade then asks which owned item gets
    /// it, and which upgrade it replaces when the item already carries two.
    /// </summary>
    public sealed class RewardScreen
    {
        const float RowHeight = 60f;

        int _upgradeOffer = -1;
        string _targetItem;
        Action _pending;

        public void Reset()
        {
            _upgradeOffer = -1;
            _targetItem = null;
            _pending = null;
        }

        /// <summary>Height of the panel content for the current step.</summary>
        public float ContentHeight(RunState run)
        {
            if (_upgradeOffer < 0)
                return 96f + run.RewardOffers.Count * (RowHeight + 4f) + 44f;
            if (_targetItem != null)
                return 120f + ShipLoadout.UpgradeSlots * 44f + 44f;
            return 120f + Targets(run).Count * (RowHeight + 4f) + 44f;
        }

        public void Draw(RunState run)
        {
            MenuStyles.Ensure();
            if (_upgradeOffer >= 0 && (_upgradeOffer >= run.RewardOffers.Count || run.RewardClaimed))
                Reset();

            if (_upgradeOffer < 0)
                DrawOffers(run);
            else if (_targetItem == null)
                DrawTargets(run);
            else
                DrawReplace(run);

            // Applied after the pass so the layout of a pass never changes halfway through.
            if (_pending != null && Event.current.type == EventType.Repaint)
            {
                var pending = _pending;
                _pending = null;
                pending();
            }
        }

        void DrawOffers(RunState run)
        {
            GUILayout.Label("Choisis une récompense", MenuStyles.Heading);
            GUILayout.Label("Un nouvel équipement, ou une amélioration pour un équipement que tu possèdes.", MenuStyles.Muted);
            GUILayout.Space(6);
            for (var i = 0; i < run.RewardOffers.Count; i++)
            {
                var offer = run.RewardOffers[i];
                var index = i;
                if (offer.Kind == RewardKind.Item)
                {
                    var item = ItemCatalog.Get(offer.Id);
                    if (item == null)
                        continue;
                    var label = $"Équipement · {ItemText.Category(item.Category)} · {ItemText.Tier(item.Tier)}\n" +
                                $"{item.Name} : {ItemText.Summary(item)}";
                    if (GUILayout.Button(new GUIContent(label, ItemIcons.Get(item)), MenuStyles.Slot, GUILayout.Height(RowHeight)))
                        _pending = () => run.TakeItemReward(index);
                }
                else
                {
                    var upgrade = UpgradeCatalog.Get(offer.Id);
                    if (upgrade == null)
                        continue;
                    var icon = upgrade.Targets.Length > 0 ? ItemIcons.Get(upgrade.Targets[0]) : null;
                    var label = $"Amélioration pour {ItemText.UpgradeTargets(upgrade)} · {ItemText.Tier(upgrade.Tier)}\n" +
                                $"{upgrade.Name} : {upgrade.Description}";
                    if (GUILayout.Button(new GUIContent(label, icon), MenuStyles.Slot, GUILayout.Height(RowHeight)))
                        _pending = () => _upgradeOffer = index;
                }
                GUILayout.Space(4);
            }
            GUILayout.Space(4);
            if (GUILayout.Button($"Passer (+{ItemText.Credits(RewardGenerator.SkipCredits)})", MenuStyles.Button, GUILayout.Height(32)))
                _pending = run.SkipReward;
        }

        List<string> Targets(RunState run)
        {
            var targets = new List<string>();
            if (_upgradeOffer < 0 || _upgradeOffer >= run.RewardOffers.Count)
                return targets;
            var upgradeId = run.RewardOffers[_upgradeOffer].Id;
            foreach (var id in run.Loadout.Owned())
                if (run.Loadout.CanInstall(id, upgradeId))
                    targets.Add(id);
            return targets;
        }

        void DrawTargets(RunState run)
        {
            var loadout = run.Loadout;
            var upgrade = UpgradeCatalog.Get(run.RewardOffers[_upgradeOffer].Id);
            GUILayout.Label($"Installer « {upgrade.Name} » sur :", MenuStyles.Heading);
            GUILayout.Label($"Chaque équipement accepte {ShipLoadout.UpgradeSlots} améliorations, qui le suivent dans la soute.", MenuStyles.Muted);
            GUILayout.Space(6);
            foreach (var id in Targets(run))
            {
                var current = loadout.Resolve(id);
                var preview = UpgradeMath.Apply(current, new[] { upgrade });
                var where = loadout.Hold.Contains(id) ? " (soute)" : "";
                var fitted = ItemText.Upgrades(loadout, id);
                var effect = ItemText.UpgradeEffect(current, preview) ?? upgrade.Description;
                var label = $"{current.Name}{where} · {loadout.UpgradeCount(id)}/{ShipLoadout.UpgradeSlots}" +
                            (fitted != null ? $" ({fitted})" : "") + $"\n{effect}";
                if (GUILayout.Button(new GUIContent(label, ItemIcons.Get(current)), MenuStyles.Slot, GUILayout.Height(RowHeight)))
                {
                    var target = id;
                    var index = _upgradeOffer;
                    _pending = loadout.UpgradeCount(target) < ShipLoadout.UpgradeSlots
                        ? () => run.TakeUpgradeReward(index, target)
                        : () => _targetItem = target;
                }
                GUILayout.Space(4);
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Retour", MenuStyles.Button, GUILayout.Height(32)))
                _pending = Reset;
        }

        void DrawReplace(RunState run)
        {
            var loadout = run.Loadout;
            var upgrade = UpgradeCatalog.Get(run.RewardOffers[_upgradeOffer].Id);
            var item = ItemCatalog.Get(_targetItem);
            GUILayout.Label($"{item.Name} porte déjà {ShipLoadout.UpgradeSlots} améliorations.", MenuStyles.Heading);
            GUILayout.Label($"Laquelle « {upgrade.Name} » remplace-t-elle ? L'ancienne est perdue.", MenuStyles.Muted);
            GUILayout.Space(6);
            var slots = loadout.UpgradesOf(_targetItem);
            for (var slot = 0; slot < ShipLoadout.UpgradeSlots; slot++)
            {
                var old = slots != null ? UpgradeCatalog.Get(slots[slot]) : null;
                if (old == null)
                    continue;
                if (GUILayout.Button($"Remplacer {old.Name}", MenuStyles.Button, GUILayout.Height(40)))
                {
                    var target = _targetItem;
                    var index = _upgradeOffer;
                    var s = slot;
                    _pending = () => run.TakeUpgradeReward(index, target, s);
                }
                GUILayout.Space(4);
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Retour", MenuStyles.Button, GUILayout.Height(32)))
                _pending = () => _targetItem = null;
        }
    }
}
