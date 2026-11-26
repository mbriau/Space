using System.Collections.Generic;
using Space.Core.Combat;
using Space.Presentation.Controls;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// One card per weapon of the player's armed ship (IMGUI, provisional until the UI Toolkit pass):
    /// power switch (the weapon draws its whole cost from the reactor, or nothing), supercharge for
    /// the weapons that have it, control (held / auto / manual), a blue reload bar and, under manual
    /// control, the fire button. Drawn by <see cref="DebugHud"/>.
    /// </summary>
    public static class WeaponPanel
    {
        public const float CardWidth = 210f;
        public const float CardHeight = 200f;
        const float Gap = 10f;
        const float TitleHeight = 24f;

        static readonly Color CardColor = new(0.05f, 0.07f, 0.12f, 0.92f);
        static readonly Color CardBorder = new(1f, 1f, 1f, 0.12f);
        static readonly Color ReadyBorder = new(0.45f, 0.85f, 1f, 0.9f);
        static readonly Color PoweredColor = new(0.25f, 0.85f, 0.45f);
        static readonly Color UnpoweredColor = new(0.55f, 0.25f, 0.25f);
        static readonly Color BlockedColor = new(0.25f, 0.25f, 0.28f);
        static readonly Color SuperchargeColor = new(0.75f, 0.4f, 1f);
        static readonly Color IdleColor = new(0.18f, 0.2f, 0.26f);
        static readonly Color ReloadColor = new(0.2f, 0.5f, 1f);
        static readonly Color ReadyColor = new(0.45f, 0.85f, 1f);
        static readonly Color TrackColor = new(1f, 1f, 1f, 0.08f);
        static readonly Color OffColor = new(0.6f, 0.6f, 0.65f);
        static readonly Color AutoColor = new(0.35f, 0.75f, 1f);
        static readonly Color ManualColor = new(1f, 0.65f, 0.25f);
        static readonly Color FireColor = new(1f, 0.45f, 0.2f);

        static readonly List<PlayerControls.WeaponStatus> Weapons = new();
        static GUIStyle _title, _name, _tag, _ammo, _button, _buttonBold, _buttonDark, _bar, _status, _fire, _muted;

        /// <summary>Total height of the panel (title and cards).</summary>
        public static float Height => TitleHeight + CardHeight;

        /// <summary>Draws the panel with its bottom-left corner at <paramref name="bottomLeft"/>.</summary>
        public static void Draw(PlayerControls controls, Vector2 bottomLeft, float maxWidth)
        {
            if (controls == null || !controls.TryGetArmedShipWeapons(Weapons, out var shipName) || Weapons.Count == 0)
                return;
            EnsureStyles();

            var cardWidth = Mathf.Min(CardWidth, (maxWidth - Gap * (Weapons.Count - 1)) / Weapons.Count);
            var width = cardWidth * Weapons.Count + Gap * (Weapons.Count - 1);
            var top = bottomLeft.y - Height;
            UiFocus.AddHudRect(new Rect(bottomLeft.x, top, width, Height));

            GUI.Label(new Rect(bottomLeft.x, top, width, TitleHeight), $"ARMEMENT · {shipName}", _title);
            for (var i = 0; i < Weapons.Count; i++)
            {
                var rect = new Rect(bottomLeft.x + i * (cardWidth + Gap), top + TitleHeight, cardWidth, CardHeight);
                DrawCard(controls, i, Weapons[i], rect);
            }
        }

        static void DrawCard(PlayerControls controls, int index, PlayerControls.WeaponStatus weapon, Rect card)
        {
            var engaged = weapon.Ready && weapon.HasTarget && weapon.Powered && weapon.Control != WeaponControl.Off;
            MenuStyles.Fill(card, engaged ? ReadyBorder : weapon.Supercharged ? SuperchargeColor : CardBorder);
            MenuStyles.Fill(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), CardColor);

            var x = card.x + 10;
            var w = card.width - 20;
            var y = card.y + 8;

            // Name, kind, power cost, ammunition.
            GUI.Label(new Rect(x, y, w, 22), $"{index + 1}  {WeaponName(weapon.Type)}", _name);
            var ammo = weapon.Ammo < 0 ? "∞" : weapon.Ammo == 0 ? "VIDE" : $"{weapon.Ammo} mun.";
            GUI.Label(new Rect(x, y, w, 22), ammo, _ammo);
            y += 20;
            GUI.Label(new Rect(x, y, w, 16), $"{(weapon.IsEnergy ? "énergie" : "physique")} · consomme {weapon.PowerDraw} unité{(weapon.PowerDraw > 1 ? "s" : "")}", _tag);
            y += 20;

            // Power switch: the whole cost or nothing; refused when the reactor is full.
            string powerLabel;
            Color powerColor;
            if (weapon.Powered)
            {
                powerLabel = $"●  SOUS TENSION · {weapon.PowerDraw + (weapon.Supercharged ? weapon.SuperchargeDraw : 0)} u.";
                powerColor = PoweredColor;
            }
            else if (weapon.CanPowerUp)
            {
                powerLabel = $"○  HORS TENSION · {weapon.PowerDraw} u.";
                powerColor = UnpoweredColor;
            }
            else
            {
                powerLabel = $"RÉACTEUR PLEIN ({weapon.PowerDraw} u.)";
                powerColor = BlockedColor;
            }
            if (ColorButton(new Rect(x, y, w, 24), powerLabel, powerColor, _buttonBold, weapon.CanPowerUp))
                controls.SetWeaponPowered(index, !weapon.Powered);
            y += 30;

            // Supercharge, for the weapons that have it.
            var superRect = new Rect(x, y, w, 24);
            if (!weapon.CanSupercharge)
            {
                GUI.Label(superRect, "pas de surcharge", _muted);
            }
            else
            {
                var factor = $"×{weapon.SuperchargeDamageFactor:0.#}";
                string label;
                if (weapon.Supercharged)
                    label = $"SURCHARGÉE · dégâts {factor}";
                else if (!weapon.Powered)
                    label = $"Surcharge +{weapon.SuperchargeDraw} u. · hors tension";
                else if (!weapon.CanSuperchargeNow)
                    label = $"Surcharge · manque {weapon.SuperchargeDraw - weapon.ReactorFree} u.";
                else
                    label = $"Surcharger +{weapon.SuperchargeDraw} u. · dégâts {factor}";
                var color = weapon.Supercharged ? SuperchargeColor : weapon.CanSuperchargeNow ? IdleColor : BlockedColor;
                if (ColorButton(superRect, label, color, weapon.Supercharged ? _buttonBold : _button, weapon.CanSuperchargeNow))
                    controls.SetWeaponSupercharged(index, !weapon.Supercharged);
            }
            y += 30;

            // Control: held, automatic or manual.
            var third = (w - 8f) / 3f;
            ControlButton(controls, index, weapon.Control, WeaponControl.Off, "Arrêt", OffColor, new Rect(x, y, third, 24));
            ControlButton(controls, index, weapon.Control, WeaponControl.Auto, "Auto", AutoColor, new Rect(x + third + 4, y, third, 24));
            ControlButton(controls, index, weapon.Control, WeaponControl.Manual, "Manuel", ManualColor, new Rect(x + 2 * (third + 4), y, third, 24));
            y += 30;

            // Reload bar.
            var bar = new Rect(x, y, w, 16);
            MenuStyles.Fill(bar, TrackColor);
            var ready = weapon.Charge >= 1f;
            MenuStyles.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(weapon.Charge), bar.height), ready ? ReadyColor : ReloadColor);
            GUI.Label(bar, ReloadText(weapon), _bar);
            y += 22;

            // Manual fire, or what the weapon is doing on its own.
            var bottom = new Rect(x, y, w, card.yMax - 8 - y);
            if (weapon.Control == WeaponControl.Manual)
            {
                var canFire = weapon.Ready && weapon.HasTarget && weapon.Powered;
                var label = canFire ? "FEU !" : !weapon.Powered ? "FEU · hors tension" : !weapon.Ready ? "FEU · recharge" : "FEU · aucune cible";
                if (ColorButton(bottom, label, canFire ? FireColor : new Color(0.3f, 0.22f, 0.18f), _fire, canFire))
                    controls.FireWeapon(index);
            }
            else
            {
                GUI.Label(bottom, StatusText(weapon), _status);
            }
        }

        static void ControlButton(PlayerControls controls, int index, WeaponControl current, WeaponControl value, string label,
                                  Color color, Rect rect)
        {
            var active = current == value;
            if (ColorButton(rect, label, active ? color : IdleColor, active ? _buttonDark : _button) && !active)
                controls.SetWeaponControl(index, value);
        }

        /// <summary>A flat button filled with <paramref name="color"/>, lighter under the pointer.</summary>
        static bool ColorButton(Rect rect, string label, Color color, GUIStyle style, bool enabled = true)
        {
            var hover = enabled && rect.Contains(Event.current.mousePosition);
            MenuStyles.Fill(rect, hover ? Color.Lerp(color, Color.white, 0.2f) : color);
            var previous = GUI.enabled;
            GUI.enabled = enabled;
            var clicked = GUI.Button(rect, label, style);
            GUI.enabled = previous;
            return clicked;
        }

        static string ReloadText(PlayerControls.WeaponStatus weapon)
        {
            if (weapon.Ammo == 0)
                return "Plus de munitions";
            if (weapon.Charge >= 1f)
                return "PRÊTE";
            if (!weapon.Powered)
                return $"Hors tension · {weapon.Charge * 100f:0} %";
            return $"Recharge · {weapon.SecondsToReady:0.0} s";
        }

        static string StatusText(PlayerControls.WeaponStatus weapon)
        {
            if (weapon.Control == WeaponControl.Off)
                return "Tir suspendu";
            if (!weapon.Powered)
                return "Tir auto · hors tension";
            return weapon.HasTarget ? "Tir auto · cible en portée" : "Tir auto · aucune cible";
        }

        public static string WeaponName(DamageType type) => type switch
        {
            DamageType.Ion => "IONIQUE",
            DamageType.Laser => "LASER",
            DamageType.Kinetic => "CANON",
            _ => "MISSILES",
        };

        static void EnsureStyles()
        {
            if (_title != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.8f, 0.95f) } };
            _name = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _tag = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.65f, 0.7f, 0.78f) } };
            _ammo = new GUIStyle(_tag) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _muted = new GUIStyle(_tag) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, normal = { textColor = new Color(0.45f, 0.5f, 0.58f) } };
            // Flat buttons: the colour is filled behind them (ColorButton), the style only draws the text.
            _button = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.75f, 0.8f, 0.88f) }, hover = { textColor = Color.white },
            };
            _buttonBold = new GUIStyle(_button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _buttonDark = new GUIStyle(_buttonBold) { normal = { textColor = new Color(0.05f, 0.07f, 0.12f) }, hover = { textColor = new Color(0.05f, 0.07f, 0.12f) } };
            _bar = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _status = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.75f, 0.8f, 0.88f) } };
            _fire = new GUIStyle(_buttonBold) { fontSize = 15 };
        }
    }
}
