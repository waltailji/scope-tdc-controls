using System;
using System.Collections.Generic;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Torpedo tube actions on the tube indicator:
    ///  - right-click a tube: a small menu with
    ///      Flood / Drain (whichever applies, as a double-click does),
    ///      Select (flooded tubes; ticked while selected, as a single click does),
    ///      Launch (flooded tubes): pulls the tube's own launch lever in the torpedo room;
    ///  - Ctrl+click a tube: Launch straight away.
    /// The lever (TorpedoLauncherLaunchLever.Launch) fires that tube on its tube group's set gyro
    /// angle, with the torpedo depth and speed set, exactly as when the lever is pulled in the
    /// torpedo room; it doesn't go through the bridge firing circuit. Same checks as the game's
    /// lever: the tube flooded and its breech door shut.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float MenuW = 132f;

        RectTransform tubeMenu, tubeMenuBlocker;
        TubeDot tubeMenuDot;
        TextMeshProUGUI tubeMenuTitle;
        MenuItem menuFlood, menuSelect, menuLaunch;
        readonly Dictionary<ExtendedTorpedoLauncher, TorpedoLauncherLaunchLever> tubeLevers = new Dictionary<ExtendedTorpedoLauncher, TorpedoLauncherLaunchLever>();

        sealed class MenuItem
        {
            public Image Bg;
            public TextMeshProUGUI Label;
            public Image Box, Tick;     // Select only: the check box and its fill
            public bool Hover;
            public bool Enabled;
        }

        // ------------------------------------------------------------------ actions

        static bool CanFlood(ExtendedTorpedoLauncher l)
        {
            if (l == null || !TubeHasTorpedo(l) || TubeBusy(l.CurrentState)) return false;
            var s = l.CurrentState;
            if (s != TorpedoLauncher.State.Loaded && s != TorpedoLauncher.State.Flooded && s != TorpedoLauncher.State.Flooding) return false;
            bool flooding = !(!l.IsFlooded && l.FloodingProgress == 0f);
            if (!flooding && l.Group != null && l.Group.HasDisableFlag((DeviceDisableFlag)1)) return false; // tube group damaged
            return true;
        }

        static bool WillDrain(ExtendedTorpedoLauncher l)
        {
            return l != null && (l.IsFlooded || l.FloodingProgress > 0f);
        }

        bool CanSelect(ExtendedTorpedoLauncher l)
        {
            var ship = Ship();
            return l != null && l.IsFlooded && ship != null && !FiringCircuitLive && !ship.IsPreparingToLaunchTorpedoes;
        }

        bool CanLaunch(ExtendedTorpedoLauncher l)
        {
            // the game's own lever check (UseTorpedoLauncherLaunchLever.GetState): flooded, breech door shut
            return l != null && l.CurrentState == TorpedoLauncher.State.Flooded && !l.HatchOpened && LeverOf(l) != null;
        }

        /// <summary>The tube's own launch lever in the torpedo room (looked up once per tube).</summary>
        TorpedoLauncherLaunchLever LeverOf(ExtendedTorpedoLauncher l)
        {
            if (l == null) return null;
            TorpedoLauncherLaunchLever lever;
            if (tubeLevers.TryGetValue(l, out lever) && lever != null) return lever;
            lever = null;
            try
            {
                foreach (var lv in Resources.FindObjectsOfTypeAll<TorpedoLauncherLaunchLever>())
                {
                    if (lv == null || !lv.gameObject.scene.IsValid()) continue;
                    if (ReferenceEquals(lv.Launcher, l)) { lever = lv; break; }
                }
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube launch lever lookup: " + e.Message); }
            if (lever == null && GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " no launch lever found for tube '" + l.name + "'");
            tubeLevers[l] = lever;
            return lever;
        }

        /// <summary>Pull the tube's own launch lever, as in the torpedo room.</summary>
        void LaunchTube(TubeDot dot)
        {
            var l = dot != null ? dot.Launcher : null;
            if (!CanLaunch(l)) return;
            try
            {
                LeverOf(l).Launch();
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " tube " + dot.Number + " launched with its own lever");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ the menu

        void OpenTubeMenu(TubeDot dot, Vector2 screenPos)
        {
            CloseTubeMenu();
            HideTip();
            pendingTubeClick = null;
            tubeMenuDot = dot;
            var font = ResolveFont();

            // full-screen catcher behind the menu: any click outside closes it
            tubeMenuBlocker = NewRect("TubeMenuBlocker", transform);
            tubeMenuBlocker.anchorMin = Vector2.zero; tubeMenuBlocker.anchorMax = Vector2.one;
            tubeMenuBlocker.offsetMin = tubeMenuBlocker.offsetMax = Vector2.zero;
            var bi = tubeMenuBlocker.gameObject.AddComponent<Image>();
            bi.color = Color.clear;
            bi.raycastTarget = true;
            tubeMenuBlocker.gameObject.AddComponent<MenuCloser>().Owner = this;

            tubeMenu = NewRect("TubeMenu", transform);
            tubeMenu.anchorMin = tubeMenu.anchorMax = new Vector2(0.5f, 0.5f);
            tubeMenu.pivot = new Vector2(0f, 1f);
            Frame(tubeMenu, true);
            tubeMenu.localScale = Vector3.one * EffectiveScale(ResizableWindow.Tubes);
            tubeMenu.gameObject.AddComponent<MenuWatch>().Owner = this;

            float y = -CPad;
            tubeMenuTitle = NewText("Title", tubeMenu, font, "", CtlFont, LabelColor);
            tubeMenuTitle.fontStyle = FontStyles.Bold;
            tubeMenuTitle.alignment = TextAlignmentOptions.Center;
            PlaceV(tubeMenuTitle.rectTransform, CPad, y, MenuW, RowH);
            y -= RowH + RowGap;

            menuFlood = MenuRow(font, ref y, false, () => { if (CanFlood(dot.Launcher)) ToggleTubeFlood(dot); }, () => WillDrain(dot.Launcher) ? Strings.TipMenuDrain : Strings.TipMenuFlood);
            menuSelect = MenuRow(font, ref y, true, () => { if (CanSelect(dot.Launcher)) SelectTube(dot); }, () => Strings.TipMenuSelect);
            menuLaunch = MenuRow(font, ref y, false, () => LaunchTube(dot), () => Strings.TipMenuLaunch);

            tubeMenu.sizeDelta = new Vector2(MenuW + CPad * 2f, -y + CPad - RowGap);

            // top-left corner at the pointer, kept on screen
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out local))
            {
                Vector2 size = tubeMenu.sizeDelta * tubeMenu.localScale.x;
                Vector2 half = canvasRect.rect.size * 0.5f;
                local.x = Mathf.Clamp(local.x + 2f, -half.x, half.x - size.x);
                local.y = Mathf.Clamp(local.y - 2f, -half.y + size.y, half.y);
                tubeMenu.anchoredPosition = local;
            }
            if (tipPanel != null) tipPanel.SetAsLastSibling(); // tooltips still over the menu
            RefreshTubeMenu();
        }

        /// <summary>One menu row: hover highlight, a check box gutter on the left (Select only), the label.</summary>
        MenuItem MenuRow(TMP_FontAsset font, ref float y, bool withBox, Action onClick, Func<string> tip)
        {
            var row = NewRect("Item", tubeMenu);
            PlaceV(row, CPad, y, MenuW, RowH);
            y -= RowH + RowGap;
            var item = new MenuItem();
            item.Bg = row.gameObject.AddComponent<Image>();
            item.Bg.color = BtnIdle;
            item.Bg.raycastTarget = true;

            if (withBox)
            {
                var box = NewRect("Box", row);
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
                box.sizeDelta = new Vector2(13f, 13f);
                box.anchoredPosition = new Vector2(4f, 0f);
                item.Box = box.gameObject.AddComponent<Image>();
                item.Box.color = DrumBlack;
                item.Box.raycastTarget = false;
                var edge = box.gameObject.AddComponent<Outline>();
                edge.effectColor = FrameEdge;
                edge.effectDistance = new Vector2(1f, -1f);
                var tick = NewRect("Tick", box);
                tick.anchorMin = Vector2.zero; tick.anchorMax = Vector2.one;
                tick.offsetMin = new Vector2(1f, 1f); tick.offsetMax = new Vector2(-1f, -1f);
                item.Tick = tick.gameObject.AddComponent<Image>();
                item.Tick.sprite = TickSprite();
                item.Tick.color = BtnSelected;
                item.Tick.raycastTarget = false;
            }

            item.Label = NewText("Text", row, font, "", CtlFont, DigitLight);
            item.Label.fontStyle = FontStyles.Bold;
            item.Label.alignment = TextAlignmentOptions.Left;
            var lr = item.Label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(23f, 0f); lr.offsetMax = new Vector2(-4f, 0f);

            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.targetGraphic = item.Bg;
            button.onClick.AddListener(() =>
            {
                if (!item.Enabled) return;
                try { onClick(); }
                catch (Exception e) { Debug.LogException(e); }
                CloseTubeMenu();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var relay = AddTooltip(row.gameObject, tip);
            relay.OnHover = h => item.Hover = h;
            return item;
        }

        /// <summary>Labels and what can be done follow the tube while the menu is open.</summary>
        void RefreshTubeMenu()
        {
            if (tubeMenu == null || tubeMenuDot == null) return;
            var l = tubeMenuDot.Launcher;
            tubeMenuTitle.text = Strings.TubeWord.ToUpperInvariant() + " " + tubeMenuDot.Number + " (" + (tubeMenuDot.Stern ? Strings.Stern : Strings.Bow).ToUpperInvariant() + ")";
            PaintMenuItem(menuFlood, WillDrain(l) ? Strings.MenuDrain : Strings.MenuFlood, CanFlood(l));
            PaintMenuItem(menuSelect, Strings.MenuSelect, CanSelect(l));
            menuSelect.Tick.enabled = l != null && l.IsFlooded && l.IsMarkedForLaunch;
            PaintMenuItem(menuLaunch, Strings.MenuLaunch, CanLaunch(l));
        }

        static void PaintMenuItem(MenuItem item, string text, bool enabled)
        {
            item.Enabled = enabled;
            if (item.Label.text != text) item.Label.text = text;
            item.Bg.color = item.Hover && enabled ? BtnHover : BtnIdle;
            Color c = DigitLight;
            if (!enabled) c.a = 0.35f;
            item.Label.color = c;
        }

        void CloseTubeMenu()
        {
            if (tubeMenu != null) Destroy(tubeMenu.gameObject);
            if (tubeMenuBlocker != null) Destroy(tubeMenuBlocker.gameObject);
            tubeMenu = tubeMenuBlocker = null;
            tubeMenuDot = null;
            menuFlood = menuSelect = menuLaunch = null;
            HideTip();
        }

        static Sprite tickSprite;

        /// <summary>A check mark.</summary>
        static Sprite TickSprite()
        {
            if (tickSprite != null) return tickSprite;
            const int n = 32;
            tickSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                // short stroke down-right from (6,16) to (13,8), long stroke up-right from (13,8) to (27,25)
                return SegDist(x, y, 6f, 16f, 13f, 8f) <= 2.6f || SegDist(x, y, 13f, 8f, 27f, 25f) <= 2.6f;
            });
            return tickSprite;
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>A click anywhere outside the menu closes it.</summary>
        sealed class MenuCloser : MonoBehaviour, IPointerDownHandler
        {
            public GyroCounterUI Owner;
            public void OnPointerDown(PointerEventData e) { if (Owner != null) Owner.CloseTubeMenu(); }
        }

        /// <summary>Keeps the menu current; closes it on Escape or when the tube window goes away.</summary>
        sealed class MenuWatch : MonoBehaviour
        {
            public GyroCounterUI Owner;
            void Update()
            {
                if (Owner == null) return;
                if (Input.GetKeyDown(KeyCode.Escape) || Owner.tubesPanel == null || !Owner.tubesPanel.gameObject.activeInHierarchy
                    || Owner.tubeMenuDot == null || !Owner.tubeDots.Contains(Owner.tubeMenuDot))
                {
                    Owner.CloseTubeMenu();
                    return;
                }
                Owner.RefreshTubeMenu();
            }
        }
    }
}
