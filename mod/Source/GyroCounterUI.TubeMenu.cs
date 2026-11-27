using System;
using System.Collections.Generic;
using TMPro;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Torpedo tube actions on the tube indicator:
    ///  - right-click a tube: a small menu with (only what can be done right now; selecting a
    ///    tube is a left click, or the firing selector)
    ///      Flood / Drain (whichever applies, as a double-click does; drain only with the outer door shut),
    ///      Open / Shut outer door (flooded tubes, TubeDoors.cs),
    ///      Launch (flooded tubes): pulls the tube's own launch lever in the torpedo room;
    ///  - Ctrl+click a tube: Launch straight away.
    /// The lever (TorpedoLauncherLaunchLever.Launch) fires that tube on its tube group's set gyro
    /// angle, with the torpedo depth and speed set, exactly as when the lever is pulled in the
    /// torpedo room; it doesn't go through the bridge firing circuit. Same checks as the game's
    /// lever: the tube flooded and its breech door shut; and here also its outer door fully open.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float MenuW = 132f;

        RectTransform tubeMenu, tubeMenuBlocker;
        TubeDot tubeMenuDot;
        TextMeshProUGUI tubeMenuTitle, tubeMenuNone;
        MenuItem menuFlood, menuDoor, menuLaunch;
        readonly List<MenuItem> menuItems = new List<MenuItem>();

        // the menu's own colours: slate blue, the launch button red
        const float MenuBtnH = 22f, MenuBtnGap = 4f, MenuPad = 6f, MenuTitleH = 18f;
        static readonly Color MenuBack = new Color(0.10f, 0.12f, 0.15f, 0.96f);
        static readonly Color MenuEdge = new Color(0.36f, 0.44f, 0.52f, 1f);
        static readonly Color MenuTitleInk = new Color(0.72f, 0.82f, 0.92f, 1f);
        static readonly Color MenuBtnIdle = new Color(0.20f, 0.26f, 0.33f, 1f);
        static readonly Color MenuBtnHover = new Color(0.28f, 0.40f, 0.53f, 1f);
        static readonly Color MenuFireIdle = new Color(0.46f, 0.15f, 0.13f, 1f);
        static readonly Color MenuFireHover = new Color(0.64f, 0.20f, 0.17f, 1f);
        readonly Dictionary<ExtendedTorpedoLauncher, TorpedoLauncherLaunchLever> tubeLevers = new Dictionary<ExtendedTorpedoLauncher, TorpedoLauncherLaunchLever>();

        sealed class MenuItem
        {
            public RectTransform Rect;
            public Image Bg;
            public TextMeshProUGUI Label;
            public bool Hover;
            public bool Enabled;        // shown (only what can be done right now is offered)
            public bool Fire;           // the launch button: red
        }

        // ------------------------------------------------------------------ actions

        static bool CanFlood(ExtendedTorpedoLauncher l)
        {
            if (l == null) return false;
            if (TubeDoors.WetEmpty(l)) return TubeDoors.CanDrainEmpty(l); // fired: drain once the door is shut
            if (!TubeHasTorpedo(l) || TubeBusy(l.CurrentState)) return false;
            var s = l.CurrentState;
            if (s != TorpedoLauncher.State.Loaded && s != TorpedoLauncher.State.Flooded && s != TorpedoLauncher.State.Flooding) return false;
            bool flooding = !(!l.IsFlooded && l.FloodingProgress == 0f);
            if (!flooding && l.Group != null && l.Group.HasDisableFlag((DeviceDisableFlag)1)) return false; // tube group damaged
            if (flooding && !TubeDoors.DoorShut(l)) return false; // no draining with the outer door open
            return true;
        }

        static bool WillDrain(ExtendedTorpedoLauncher l)
        {
            return l != null && (l.IsFlooded || l.FloodingProgress > 0f || TubeDoors.WetEmpty(l));
        }

        bool CanLaunch(ExtendedTorpedoLauncher l)
        {
            // the game's own lever check (UseTorpedoLauncherLaunchLever.GetState): flooded, breech door shut
            // and here: the outer door fully open
            return l != null && l.CurrentState == TorpedoLauncher.State.Flooded && l.Torpedo != null // a fired tube is flooded too, with nothing in it
                   && !l.HatchOpened && TubeDoors.DoorReady(l) && LeverOf(l) != null;
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
                SayLaunchOrder();
                LeverOf(l).Launch();
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " tube " + dot.Number + " launched with its own lever");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>
        /// The commander's "Los!" ("LOS" line, English "Launch!"), as the game's own fire sequence
        /// calls it (PlayerShip.LaunchAllTorpedoesInternal); the tube's lever launches silently.
        /// </summary>
        static void SayLaunchOrder()
        {
            try
            {
                var crew = Get<PlayerCrew>();
                var co = crew != null ? crew.CommandingOfficer : null;
                if (co != null) co.Say("LOS");
            }
            catch (Exception e) { if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " launch callout: " + e.Message); }
        }

        /// <summary>The door is open or on its way: the menu offers to shut it.</summary>
        static bool DoorOpening(ExtendedTorpedoLauncher l)
        {
            var d = TubeDoors.Door(l);
            return d == DoorState.Open || d == DoorState.Opening;
        }

        /// <summary>Open the outer door of a flooded tube, or shut it.</summary>
        void ToggleDoor(TubeDot dot)
        {
            var l = dot != null ? dot.Launcher : null;
            if (l == null) return;
            if (TubeDoors.CanClose(l)) TubeDoors.Close(l);
            else if (TubeDoors.CanOpen(l)) TubeDoors.Open(l);
            else return;
            PlayGameSound(AnyBoxKnobSound());
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
            var back = tubeMenu.gameObject.AddComponent<Image>();
            back.color = MenuBack;
            RoundValueBox(back);
            back.raycastTarget = true;
            var edge = tubeMenu.gameObject.AddComponent<Outline>();
            edge.effectColor = MenuEdge;
            edge.effectDistance = new Vector2(1f, -1f);
            tubeMenu.localScale = Vector3.one * EffectiveScale(ResizableWindow.Tubes);
            tubeMenu.gameObject.AddComponent<MenuWatch>().Owner = this;

            tubeMenuTitle = NewText("Title", tubeMenu, font, "", CtlFont, MenuTitleInk);
            tubeMenuTitle.fontStyle = FontStyles.Bold;
            tubeMenuTitle.alignment = TextAlignmentOptions.Center;
            PlaceV(tubeMenuTitle.rectTransform, MenuPad, -MenuPad, MenuW, MenuTitleH);

            // shown when there is nothing to do with the tube right now
            tubeMenuNone = NewText("None", tubeMenu, font, Strings.MenuNothing, CtlFont - 1f, new Color(MenuTitleInk.r, MenuTitleInk.g, MenuTitleInk.b, 0.6f));
            tubeMenuNone.fontStyle = FontStyles.Italic;
            tubeMenuNone.alignment = TextAlignmentOptions.Center;
            PlaceV(tubeMenuNone.rectTransform, MenuPad, -MenuPad - MenuTitleH - MenuBtnGap, MenuW, MenuBtnH);

            menuItems.Clear();
            // top to bottom: Launch, outer door, Flood / Drain (so Open door sits above Drain, and
            // Launch above Shut door; each shows only when it can be used). Selecting a tube is a
            // left click on it, or the firing selector.
            menuLaunch = MenuButton(font, () => LaunchTube(dot), () => Strings.TipMenuLaunch);
            menuLaunch.Fire = true;
            if (TubeDoors.Enabled)
                menuDoor = MenuButton(font, () => ToggleDoor(dot), () => DoorOpening(dot.Launcher) ? Strings.TipMenuDoorClose : Strings.TipMenuDoorOpen);
            menuFlood = MenuButton(font, () => { if (CanFlood(dot.Launcher)) ToggleTubeFlood(dot); }, () => WillDrain(dot.Launcher) ? Strings.TipMenuDrain : Strings.TipMenuFlood);

            // top-left corner at the pointer, kept on screen (room for every button, however many show)
            tubeMenu.sizeDelta = new Vector2(MenuW + MenuPad * 2f, MenuHeight(menuItems.Count));
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

        readonly List<MenuItem> menuOrder = new List<MenuItem>();

        /// <summary>The buttons top to bottom: as built, or with Flood / Drain moved above the door.</summary>
        List<MenuItem> MenuOrder(bool drainFirst)
        {
            menuOrder.Clear();
            menuOrder.AddRange(menuItems);
            if (drainFirst && menuFlood != null && menuDoor != null)
            {
                menuOrder.Remove(menuFlood);
                menuOrder.Insert(menuOrder.IndexOf(menuDoor), menuFlood);
            }
            return menuOrder;
        }

        /// <summary>Menu height with n buttons shown (n = 0: the "nothing to do" line).</summary>
        static float MenuHeight(int n)
        {
            return MenuPad + MenuTitleH + MenuBtnGap + Mathf.Max(1, n) * (MenuBtnH + MenuBtnGap) - MenuBtnGap + MenuPad;
        }

        /// <summary>One menu button: rounded, with a hover colour; placed by RefreshTubeMenu.</summary>
        MenuItem MenuButton(TMP_FontAsset font, Action onClick, Func<string> tip)
        {
            var row = NewRect("Item", tubeMenu);
            PlaceV(row, MenuPad, 0f, MenuW, MenuBtnH);
            var item = new MenuItem();
            item.Rect = row;
            item.Bg = row.gameObject.AddComponent<Image>();
            item.Bg.color = MenuBtnIdle;
            RoundValueBox(item.Bg);
            item.Bg.raycastTarget = true;
            menuItems.Add(item);

            item.Label = NewText("Text", row, font, "", CtlFont, DigitLight);
            item.Label.fontStyle = FontStyles.Bold;
            item.Label.alignment = TextAlignmentOptions.Center;
            var lr = item.Label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(4f, 0f); lr.offsetMax = new Vector2(-4f, 0f);

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
            if (menuDoor != null)
                PaintMenuItem(menuDoor, DoorOpening(l) ? Strings.MenuDoorClose : Strings.MenuDoorOpen, TubeDoors.CanOpen(l) || TubeDoors.CanClose(l));
            PaintMenuItem(menuLaunch, Strings.MenuLaunch, CanLaunch(l));

            // only the buttons that can be used, stacked under the title
            float y = -MenuPad - MenuTitleH - MenuBtnGap;
            int shown = 0;
            // a fired tube still flooded: Drain first (getting it ready to reload), the door after
            bool drainFirst = TubeDoors.WetEmpty(l) && menuDoor != null;
            foreach (var item in MenuOrder(drainFirst))
            {
                if (item.Rect.gameObject.activeSelf != item.Enabled) item.Rect.gameObject.SetActive(item.Enabled);
                if (!item.Enabled) { item.Hover = false; continue; }
                item.Rect.anchoredPosition = new Vector2(MenuPad, y);
                y -= MenuBtnH + MenuBtnGap;
                shown++;
            }
            if (tubeMenuNone.gameObject.activeSelf != (shown == 0)) tubeMenuNone.gameObject.SetActive(shown == 0);
            float h = MenuHeight(shown);
            if (!Mathf.Approximately(tubeMenu.sizeDelta.y, h)) tubeMenu.sizeDelta = new Vector2(tubeMenu.sizeDelta.x, h);
        }

        static void PaintMenuItem(MenuItem item, string text, bool enabled)
        {
            item.Enabled = enabled;
            if (item.Label.text != text) item.Label.text = text;
            item.Bg.color = item.Fire ? (item.Hover ? MenuFireHover : MenuFireIdle) : (item.Hover ? MenuBtnHover : MenuBtnIdle);
            item.Label.color = DigitLight;
        }

        void CloseTubeMenu()
        {
            if (tubeMenu != null) Destroy(tubeMenu.gameObject);
            if (tubeMenuBlocker != null) Destroy(tubeMenuBlocker.gameObject);
            tubeMenu = tubeMenuBlocker = null;
            tubeMenuDot = null;
            menuFlood = menuDoor = menuLaunch = null;
            menuItems.Clear();
            tubeMenuNone = null;
            HideTip();
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
