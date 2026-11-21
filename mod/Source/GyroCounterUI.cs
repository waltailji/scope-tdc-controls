using System;
using System.Collections.Generic;
using System.Reflection;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.UI;
using UBOAT.Game.UI.Periscope;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Overlay drawn while the periscope / UZO view is open.
    ///
    ///  Under the bearing tape:        Beside the periscope raise/lower control:
    ///     [ 3 5 7 ° (o) ]               [ (o) PEILUNG    ]
    ///                                   [ FOLGEN         ]
    ///                                   [ NICHT FOLGEN   ]
    ///                                   [ EMPFÄNGER      ]
    ///                                   [ AUS ] [ BUG ] [ HECK ]  (stacked)
    ///
    /// Positions are taken from the game UI while it is visible and then held,
    /// so hiding the game UI (Alt) does not move anything.
    /// </summary>
    [NonSerializedInGameState]
    public partial class GyroCounterUI : MonoBehaviour
    {
        // ---- counter layout (reference pixels at 1080p, before Scale) ----
        const float DigitW = 15f;
        const float DigitH = 21f;
        const float DigitGap = 1.5f;
        const float Pad = 3f;

        // ---- controls layout ----
        const float RowH = 20f;
        const float RowGap = 3f;
        const float CPad = 5f;
        const float CtlFont = 11f;
        const float HeaderFont = 10f;

        const float SyncTolerance = 3.5f; // same tolerance as the TDC's own "gyro set" light
        const float TipDelay = 0.35f;
        const float TipMaxWidth = 280f;

        static readonly Color FrameColor = new Color(0.13f, 0.12f, 0.10f, 0.92f);
        static readonly Color FrameEdge = new Color(0.62f, 0.50f, 0.28f, 1f);
        static readonly Color DrumBlack = new Color(0.04f, 0.04f, 0.04f, 1f);
        static readonly Color DrumWhite = new Color(0.92f, 0.90f, 0.84f, 1f);
        static readonly Color DigitLight = new Color(0.95f, 0.93f, 0.86f, 1f);
        static readonly Color DigitDark = new Color(0.06f, 0.06f, 0.06f, 1f);
        static readonly Color LabelColor = new Color(0.86f, 0.74f, 0.48f, 1f);
        static readonly Color BtnIdle = new Color(0.20f, 0.18f, 0.14f, 1f);
        static readonly Color BtnHover = new Color(0.30f, 0.26f, 0.19f, 1f);
        static readonly Color BtnSelected = new Color(0.74f, 0.60f, 0.33f, 1f);
        static readonly Color LampGreen = new Color(0.35f, 1f, 0.40f, 1f);
        static readonly Color LampRed = new Color(1f, 0.25f, 0.18f, 1f);
        static readonly Color LampAmber = new Color(1f, 0.72f, 0.22f, 1f);
        static readonly Color LampBlue = new Color(0.30f, 0.62f, 1f, 1f);

        static GyroCounterUI instance;
        static PeriscopeUI periscopeUI;
        static RawImage lastCompass;
        static int lastCompassFrame = -100;
        static bool userHidden;

        // positions remembered while the game UI is visible (canvas-local, before offsets)
        static bool hasCounterLatch, hasControlsLatch;
        static Vector2 counterLatch, controlsLatch;
        static Vector2 controlsLatchPivot = new Vector2(0f, 0.5f);

        static AccessTools.FieldRef<PeriscopeUI, TorpedoCalculator> tdcField;
        static AccessTools.FieldRef<PeriscopeUI, TextMeshProUGUI> fontSourceField;
        static AccessTools.FieldRef<PeriscopeUI, Toggle> followToggleField;
        static AccessTools.FieldRef<TorpedoFireControlBoxes, TorpedoLauncherGroup> bowReceiverField;
        static AccessTools.FieldRef<TorpedoFireControlBoxes, TorpedoLauncherGroup> sternReceiverField;
        static AccessTools.FieldRef<TorpedoFireControlBoxes, Periscope> uzoField, obsScopeField, attackScopeField;
        static FieldInfo elevatorUpField, elevatorDownField;
        static MethodInfo followUpMethod;
        static bool fieldsResolved;
        static Sprite circleSprite, glowSprite;

        Canvas canvas;
        RectTransform canvasRect;
        RectTransform counterPanel;
        RectTransform controlsPanel;
        RectTransform tipPanel;
        TextMeshProUGUI tipText;
        Lamp syncLamp, followLamp;
        Btn btnFollow, btnNoFollow, btnOff, btnBow, btnStern;
        Btn btnSettings;
        RectTransform settingsPanel;      // drop-down under the control panel
        bool settingsOpen;
        readonly List<Check> checks = new List<Check>();

        sealed class Check
        {
            public Image Fill;
            public Image Bg;
            public Func<bool> Get;
            public bool Hover;
        }
        CounterRow tdcRow;   // TDC gyro angle (the movable panel)
        CounterRow parRow;   // TDC parallax correction, attached below tdcRow
        CounterRow rcvRowA;  // active receiver (or Bow when all receivers are shown)
        CounterRow rcvRowB;  // Stern, only when all receivers are shown
        Btn btnParFwd, btnParBack;
        Btn btnTxNone, btnTxUzo, btnTxObs, btnTxAttack;
        Image unmannedVeil;
        TextMeshProUGUI unmannedNote;
        RectTransform raiseBtn, lowerBtn;   // raise fully / lower fully, beside the stock raise/lower control
        Image raiseBg, lowerBg, raiseIcon, lowerIcon;
        bool raiseHover, lowerHover;
        static Sprite raiseSprite, lowerSprite;
        TorpedoCalculator stationTdc;
        Interactable tdcStation;
        readonly Vector3[] corners = new Vector3[4];
        bool loggedCounterAnchor, loggedControlsAnchor;

        /// <summary>One labelled drum readout: [ LABEL | 3 5 7 ° ]</summary>
        sealed class CounterRow
        {
            public RectTransform Panel;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Side;
            public readonly List<RectTransform> Strips = new List<RectTransform>(); // most significant first
            public readonly List<TextMeshProUGUI[]> Digits = new List<TextMeshProUGUI[]>(); // the 11 slots of each drum
            public readonly List<int> Patched = new List<int>(); // slot relabelled "0" during 359 -> 000, or -1

            public void AddDrum(RectTransform strip, TextMeshProUGUI[] digits)
            {
                Strips.Add(strip);
                Digits.Add(digits);
                Patched.Add(-1);
            }
            public float Displayed = float.NaN;
            public bool Signed;  // shows +/- and the magnitude (parallax) instead of 000-359
            public bool Tenths;
            public TorpedoLauncherGroup ShownGroup; // receiver rows: the tube group on display
        }

        /// <summary>Double-click on a receiver row.</summary>
        sealed class DoubleClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public Action OnDoubleClick;
            public void OnPointerClick(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2 && OnDoubleClick != null) OnDoubleClick();
            }
        }

        Func<string> pendingTip;
        float pendingTipTime;
        bool tipShown;

        // ------------------------------------------------------------------ hooks

        public static void Attach(PeriscopeUI ui)
        {
            periscopeUI = ui;
            GyroConfig.Load();
            Strings.Resolve();
            TdcOperator.Apply(); // so the station bar's TDC button reflects the setting
            if (!GyroConfig.Enabled) { if (instance != null) instance.SetVisible(false); return; }

            if (instance == null)
            {
                var go = new GameObject("ScopeTdcControls");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GyroCounterUI>();
            }
            instance.Build();
            instance.SetVisible(!userHidden);
            SidebarButton.Ensure(ui, OnSidebarButton);
        }

        /// <summary>The sidebar button: show or hide the control panel (remembered in the config).</summary>
        static void OnSidebarButton(bool visible)
        {
            GyroConfig.ControlsVisible = visible;
            GyroConfig.Save("ControlsVisible", visible);
            SidebarButton.SetState(visible);
            if (!visible && instance != null) instance.HideTip();
        }

        public static void Detach(PeriscopeUI ui)
        {
            if (periscopeUI == ui) periscopeUI = null;
            if (instance != null)
            {
                instance.HideTip();
                instance.SetVisible(false);
            }
            ScopeAim.Stop();
            PeriscopeTravel.Stop();
        }

        public static void ReportCompass(RawImage img)
        {
            if (img == null || !img.isActiveAndEnabled) return;
            lastCompass = img;
            lastCompassFrame = Time.frameCount;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                gameObject.AddComponent<GraphicRaycaster>();
                canvasRect = (RectTransform)transform;
            }
            canvas.sortingOrder = GyroConfig.SortingOrder;

            if (counterPanel != null) Destroy(counterPanel.gameObject);
            if (controlsPanel != null) Destroy(controlsPanel.gameObject);
            if (tipPanel != null) Destroy(tipPanel.gameObject);
            if (raiseBtn != null) Destroy(raiseBtn.gameObject);
            if (lowerBtn != null) Destroy(lowerBtn.gameObject);
            if (dialsPanel != null) Destroy(dialsPanel.gameObject);
            counterPanel = controlsPanel = tipPanel = raiseBtn = lowerBtn = dialsPanel = null;
            dialsGroup = null;
            dials.Clear();
            tdcRow = parRow = rcvRowA = rcvRowB = null;
            btnParFwd = btnParBack = null;
            btnTxNone = btnTxUzo = btnTxObs = btnTxAttack = null;
            syncLamp = followLamp = null;
            btnFollow = btnNoFollow = btnOff = btnBow = btnStern = null;
            btnSettings = null;
            settingsPanel = null; // destroyed with controlsPanel
            checks.Clear();
            pendingTip = null;
            tipShown = false;

            TMP_FontAsset font = ResolveFont();
            BuildCounter(font);
            if (GyroConfig.ShowControls) BuildControls(font);
            if (GyroConfig.ElevatorButtons) BuildElevatorButtons();
            if (GyroConfig.ShowDials) BuildDials(font);
            BuildTooltip(font);
        }

        // ------------------------------------------------------------------ raise fully / lower fully

        void BuildElevatorButtons()
        {
            if (raiseSprite == null) raiseSprite = ArrowToBarSprite(true);
            if (lowerSprite == null) lowerSprite = ArrowToBarSprite(false);
            raiseBtn = MakeIconButton("RaiseFully", raiseSprite, () => PeriscopeTravel.Toggle(periscopeUI, +1),
                                      () => Strings.TipRaiseFully, h => raiseHover = h, out raiseBg, out raiseIcon);
            lowerBtn = MakeIconButton("LowerFully", lowerSprite, () => PeriscopeTravel.Toggle(periscopeUI, -1),
                                      () => Strings.TipLowerFully, h => lowerHover = h, out lowerBg, out lowerIcon);
        }

        RectTransform MakeIconButton(string name, Sprite icon, Action onClick, Func<string> tip, Action<bool> hover,
                                     out Image bg, out Image iconImage)
        {
            var r = NewRect(name, transform);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(30f, 30f);
            bg = r.gameObject.AddComponent<Image>();
            bg.color = BtnIdle;
            bg.raycastTarget = true;
            var edge = r.gameObject.AddComponent<Outline>();
            edge.effectColor = FrameEdge;
            edge.effectDistance = new Vector2(1f, -1f);

            var ir = NewRect("Icon", r);
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(3f, 2f); ir.offsetMax = new Vector2(-3f, -2f);
            iconImage = ir.gameObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = DigitLight;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var button = r.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                try { onClick(); }
                catch (Exception e) { Debug.LogException(e); }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var relay = AddTooltip(r.gameObject, tip);
            relay.OnHover = hover;
            return r;
        }

        /// <summary>Above the stock raise button / below the stock lower button; hidden when that control isn't shown.</summary>
        void UpdateElevatorButtons(bool uiHidden)
        {
            if (raiseBtn == null || lowerBtn == null) return;
            Vector2 mn = Vector2.zero, mx = Vector2.zero;
            bool have = !uiHidden && ElevatorRect(out mn, out mx);
            if (raiseBtn.gameObject.activeSelf != have) { raiseBtn.gameObject.SetActive(have); lowerBtn.gameObject.SetActive(have); }
            if (!have) return;

            Vector2 lmin = ToLocal(mn), lmax = ToLocal(mx);
            // as wide as the stock control, half as tall
            float width = Mathf.Max(lmax.x - lmin.x, 22f);
            float height = Mathf.Max(width * 0.5f, 12f);
            float cx = (lmin.x + lmax.x) * 0.5f;
            const float gap = 4f;
            raiseBtn.sizeDelta = lowerBtn.sizeDelta = new Vector2(width, height);
            raiseBtn.anchoredPosition = new Vector2(cx, lmax.y + gap + height * 0.5f);
            lowerBtn.anchoredPosition = new Vector2(cx, lmin.y - gap - height * 0.5f);

            PaintTravel(raiseBg, raiseIcon, PeriscopeTravel.Direction > 0, raiseHover);
            PaintTravel(lowerBg, lowerIcon, PeriscopeTravel.Direction < 0, lowerHover);
        }

        static void PaintTravel(Image bg, Image icon, bool active, bool hover)
        {
            bg.color = active ? BtnSelected : (hover ? BtnHover : BtnIdle);
            icon.color = active ? DigitDark : DigitLight;
        }

        /// <summary>White arrow to a bar: up (⤒) for raise fully, down (⤓) for lower fully.</summary>
        static Sprite ArrowToBarSprite(bool up)
        {
            const int S = 48, SS = 4;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float fx = x + (sx + 0.5f) / SS;
                            float fy = y + (sy + 0.5f) / SS;
                            if (!up) fy = S - fy; // draw the "up" shape, mirrored for "down"
                            bool bar = fx >= 10f && fx <= 38f && fy >= 37f && fy <= 42f;
                            // arrow head: apex (24, 33), base (9..39) at y 17
                            bool head = fy >= 17f && fy <= 33f && Mathf.Abs(fx - 24f) <= (33f - fy) * (15f / 16f);
                            bool stem = fx >= 20f && fx <= 28f && fy >= 6f && fy < 17f;
                            if (bar || head || stem) hits++;
                        }
                    px[y * S + x] = new Color(1f, 1f, 1f, hits / (float)(SS * SS));
                }
            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        const float RowLabelFont = 9.5f;
        const float RowStackGap = 3f;

        enum RowKind { Tdc, Parallax, Receiver }

        void BuildCounter(TMP_FontAsset font)
        {
            // one label column wide enough for every label, so all rows line up
            float labelW = 0f;
            foreach (var s in new[] { Strings.TdcLabel, Strings.ParallaxLabel, Strings.Bow, Strings.Stern })
                labelW = Mathf.Max(labelW, TextWidth(font, s, RowLabelFont));
            labelW = Mathf.Ceil(labelW) + 4f;

            tdcRow = BuildCounterRow(transform, font, "GyroCounter", Strings.TdcLabel, labelW, RowKind.Tdc);
            counterPanel = tdcRow.Panel;
            counterPanel.anchorMin = counterPanel.anchorMax = new Vector2(0.5f, 0.5f);
            counterPanel.localScale = Vector3.one * GyroConfig.Scale;
            AddTooltip(counterPanel.gameObject, () => Strings.TipCounter);
            AddDrag(counterPanel.gameObject, false);

            // "TDC not manned": grey veil over the TDC row (only its own rect, the
            // hanging rows are hidden then anyway) and a small note under it
            var veil = NewRect("UnmannedVeil", counterPanel);
            veil.anchorMin = Vector2.zero; veil.anchorMax = Vector2.one;
            veil.offsetMin = veil.offsetMax = Vector2.zero;
            unmannedVeil = veil.gameObject.AddComponent<Image>();
            unmannedVeil.color = new Color(0.10f, 0.10f, 0.10f, 0.62f);
            unmannedVeil.raycastTarget = false;
            veil.gameObject.SetActive(false);

            unmannedNote = NewText("UnmannedNote", counterPanel, font, Strings.TdcNotManned, 10f, LabelColor);
            unmannedNote.fontStyle = FontStyles.Italic;
            unmannedNote.alignment = TextAlignmentOptions.Center;
            var nr = unmannedNote.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(counterPanel.sizeDelta.x + 40f, 14f);
            nr.anchoredPosition = new Vector2(0f, -2f);
            unmannedNote.gameObject.SetActive(false);

            // parallax and receiver rows hang under the TDC row, left-aligned so the drums line up,
            // and move with it (Ctrl+drag on any row moves the whole stack)
            if (GyroConfig.ShowParallax)
            {
                parRow = BuildCounterRow(counterPanel, font, "ParallaxCounter", Strings.ParallaxLabel, labelW, RowKind.Parallax);
                HangUnder(parRow.Panel);
                AddTooltip(parRow.Panel.gameObject, () => Strings.TipParallaxCounter);
                AddDrag(parRow.Panel.gameObject, false);
            }

            rcvRowA = BuildReceiverRow(font, "ReceiverCounterA", labelW);
            rcvRowB = BuildReceiverRow(font, "ReceiverCounterB", labelW);
        }

        CounterRow BuildReceiverRow(TMP_FontAsset font, string name, float labelW)
        {
            var row = BuildCounterRow(counterPanel, font, name, Strings.Bow, labelW, RowKind.Receiver);
            HangUnder(row.Panel);
            AddTooltip(row.Panel.gameObject, () => Strings.TipReceiverCounter);
            AddDrag(row.Panel.gameObject, false);
            row.Panel.gameObject.AddComponent<DoubleClickRelay>().OnDoubleClick = () => OnReceiverDoubleClick(row);
            row.Panel.gameObject.SetActive(false);
            return row;
        }

        /// <summary>
        /// Double-click a receiver row:
        ///   all receivers shown: switch the receiver to that tube group;
        ///   active receiver only: switch to the other group.
        /// Then turn the scope so the TDC's gyro angle matches the angle that
        /// group's gyros are already set to (follow-up is switched on for that).
        /// Tubes checked at the other end are unchecked, so tubes, receiver and
        /// parallax all refer to the same end.
        /// </summary>
        void OnReceiverDoubleClick(CounterRow row)
        {
            try
            {
                var tdc = Tdc();
                if (tdc == null || !IsTdcManned(tdc)) return;
                TorpedoLauncherGroup bow, stern;
                ReceiverGroups(out bow, out stern);

                TorpedoLauncherGroup target;
                if (GyroConfig.ActiveReceiverOnly)
                {
                    var active = tdc.GyroAngleReceiver;
                    if (active == null) return;
                    target = ReferenceEquals(active, bow) ? stern : bow;
                }
                else target = row.ShownGroup;
                if (target == null) return;

                float gyro = target.SetGyroAngle; // the angle that group's gyros already hold
                bool toStern = IsSternGroup(target);
                var setting = toStern ? GyroAngleReceiverSetting.Stern : GyroAngleReceiverSetting.Bow;

                // tubes first (changing them clears manual receiver picks), then disconnect the
                // receiver while the scope swings, so the TDC's passing gyro angles don't drag
                // the target group's gyros along; connect it once the TDC angle matches
                TubeSelection.KeepOnlyEnd(toStern);
                SetReceiver(GyroAngleReceiverSetting.Off);

                // the TDC must take its bearing from this scope for the aim to work
                TransmitterOverride.Clear();
                SetFollowUp(true);
                ScopeAim.Start(gyro, toStern, () => SetReceiver(setting));
                HideTip();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void HangUnder(RectTransform r)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(0f, -RowStackGap);
        }

        CounterRow BuildCounterRow(Transform parent, TMP_FontAsset font, string name, string label, float labelW, RowKind kind)
        {
            var row = new CounterRow();
            row.Panel = NewRect(name, parent);
            row.Signed = kind == RowKind.Parallax;
            row.Tenths = GyroConfig.ShowTenths;
            var p = row.Panel;
            Frame(p, true);

            float x = Pad + 1f;
            row.Label = NewText("Label", p, font, label, RowLabelFont, LabelColor);
            row.Label.fontStyle = FontStyles.Bold;
            row.Label.alignment = TextAlignmentOptions.Center;
            PlaceH(row.Label.rectTransform, x, labelW, DigitH);
            x += labelW + 2f;

            // sign / L-R slot: reserved in every row whenever any row needs it, so the drums align
            if (GyroConfig.LeftRight || GyroConfig.ShowParallax)
            {
                row.Side = NewText("Side", p, font, "", DigitH * 0.7f, LabelColor);
                row.Side.fontStyle = FontStyles.Bold;
                row.Side.alignment = TextAlignmentOptions.Center;
                PlaceH(row.Side.rectTransform, x, DigitW - 3f, DigitH);
                x += DigitW - 3f + DigitGap;
            }

            for (int i = 0; i < 3; i++)
            {
                MakeDrum(row, p, font, x, false);
                x += DigitW + DigitGap;
            }

            if (row.Tenths)
            {
                var dot = NewText("Dot", p, font, ".", DigitH * 0.8f, DigitLight);
                dot.alignment = TextAlignmentOptions.Bottom;
                PlaceH(dot.rectTransform, x - DigitGap, 5f, DigitH);
                x += 4f;
                MakeDrum(row, p, font, x, true);
                x += DigitW + DigitGap;
            }

            var deg = NewText("Deg", p, font, "°", DigitH * 0.6f, DigitLight);
            deg.alignment = TextAlignmentOptions.Top;
            PlaceH(deg.rectTransform, x - DigitGap, 8f, DigitH);
            x += 8f;

            if (GyroConfig.ShowSyncLamp)
            {
                x += 3f;
                if (kind == RowKind.Tdc)
                {
                    var slot = NewRect("SyncSlot", p);
                    PlaceH(slot, x, 12f, DigitH);
                    var hit = slot.gameObject.AddComponent<Image>();
                    hit.color = Color.clear; // invisible hover target for its own tooltip
                    syncLamp = MakeLamp(slot, new Vector2(0.5f, 0.5f), Vector2.zero, 9f, LampGreen);
                    AddTooltip(slot.gameObject, () => Strings.TipSyncLamp);
                }
                x += 12f; // keep all rows the same width
            }

            p.sizeDelta = new Vector2(x + Pad, DigitH + Pad * 2f);
            return row;
        }

        void BuildControls(TMP_FontAsset font)
        {
            controlsPanel = NewRect("GyroControls", transform);
            controlsPanel.anchorMin = controlsPanel.anchorMax = new Vector2(0.5f, 0.5f);
            Frame(controlsPanel, true);

            // width = widest label
            string[] labels = { Strings.BearingHeader, Strings.Follow, Strings.NoFollow,
                                Strings.SourceHeader, Strings.TxNone, Strings.TxUzo, Strings.TxObservation, Strings.TxAttack,
                                Strings.ReceiverHeader, Strings.Off, Strings.Bow, Strings.Stern,
                                Strings.ParallaxHeader, Strings.ParForward, Strings.ParBackward,
                                "+ " + Strings.SettingsHeader };
            float inner = 80f;
            foreach (var s in labels) inner = Mathf.Max(inner, Mathf.Ceil(TextWidth(font, s, CtlFont)) + 18f);
            inner = Mathf.Max(inner, 16f + Mathf.Ceil(TextWidth(font, Strings.BearingHeader, HeaderFont)) + 6f);

            float y = -CPad;

            // --- bearing follow-up ---
            y = Header(font, Strings.BearingHeader, y, inner, true, () => Strings.TipBearingHeader);
            btnFollow = MakeButton(controlsPanel, font, Strings.Follow, ref y, inner, () => SetFollowUp(true), () => Strings.TipFollow);
            btnNoFollow = MakeButton(controlsPanel, font, Strings.NoFollow, ref y, inner, () => SetFollowUp(false), () => Strings.TipNoFollow);
            Divider(ref y, inner);

            // --- bearing source (transmitter) ---
            y = Header(font, Strings.SourceHeader, y, inner, false, () => Strings.TipSourceHeader);
            btnTxNone = MakeButton(controlsPanel, font, Strings.TxNone, ref y, inner, () => SetTransmitter(BearingTransmitter.None), () => Strings.TipTxNone);
            btnTxUzo = MakeButton(controlsPanel, font, Strings.TxUzo, ref y, inner, () => SetTransmitter(BearingTransmitter.UZO), () => Strings.TipTxUzo);
            btnTxObs = MakeButton(controlsPanel, font, Strings.TxObservation, ref y, inner, () => SetTransmitter(BearingTransmitter.ObservationPeriscope), () => Strings.TipTxObservation);
            btnTxAttack = MakeButton(controlsPanel, font, Strings.TxAttack, ref y, inner, () => SetTransmitter(BearingTransmitter.AttackPeriscope), () => Strings.TipTxAttack);
            Divider(ref y, inner);

            // --- gyro receiver ---
            y = Header(font, Strings.ReceiverHeader, y, inner, false, () => Strings.TipReceiverHeader);
            btnOff = MakeButton(controlsPanel, font, Strings.Off, ref y, inner, () => SetReceiver(GyroAngleReceiverSetting.Off), () => Strings.TipOff);
            btnBow = MakeButton(controlsPanel, font, Strings.Bow, ref y, inner, () => SetReceiver(GyroAngleReceiverSetting.Bow), () => Strings.TipBow);
            btnStern = MakeButton(controlsPanel, font, Strings.Stern, ref y, inner, () => SetReceiver(GyroAngleReceiverSetting.Stern), () => Strings.TipStern);
            Divider(ref y, inner);

            // --- parallax direction ---
            y = Header(font, Strings.ParallaxHeader, y, inner, false, () => Strings.TipParallaxHeader);
            btnParFwd = MakeButton(controlsPanel, font, Strings.ParForward, ref y, inner, () => SetParallax(Parallax.Forward), () => Strings.TipParForward);
            btnParBack = MakeButton(controlsPanel, font, Strings.ParBackward, ref y, inner, () => SetParallax(Parallax.Backward), () => Strings.TipParBackward);
            Divider(ref y, inner);

            // --- settings drop-down toggle ---
            btnSettings = MakeButton(controlsPanel, font, SettingsLabel(), ref y, inner, ToggleSettings, () => Strings.TipSettings);

            controlsPanel.sizeDelta = new Vector2(inner + CPad * 2f, -y + CPad - RowGap);
            controlsPanel.localScale = Vector3.one * GyroConfig.Scale;

            AddDrag(controlsPanel.gameObject, true);
            BuildSettings(font);
        }

        void Divider(ref float y, float inner)
        {
            y -= 2f;
            var div = NewRect("Divider", controlsPanel);
            PlaceV(div, CPad, y, inner, 1.5f);
            var di = div.gameObject.AddComponent<Image>();
            di.color = FrameEdge;
            di.raycastTarget = false;
            y -= 1.5f + RowGap + 2f;
        }

        string SettingsLabel() { return (settingsOpen ? "- " : "+ ") + Strings.SettingsHeader; }

        void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(settingsOpen);
            if (btnSettings != null) btnSettings.Label.text = SettingsLabel();
        }

        /// <summary>Drop-down with check boxes, hanging under the control panel.</summary>
        void BuildSettings(TMP_FontAsset font)
        {
            settingsPanel = NewRect("GyroSettings", controlsPanel);
            Frame(settingsPanel, true);

            string[] labels = { Strings.OptShowReceiver, Strings.OptActiveReceiverOnly, Strings.OptShowParallax, Strings.OptAutoReceiver, Strings.OptAutoParallax, Strings.OptRealisticTdc, Strings.OptRealisticSync, Strings.OptRealisticFire, Strings.OptTdcOperator };
            float inner = 0f;
            foreach (var s in labels) inner = Mathf.Max(inner, Mathf.Ceil(TextWidth(font, s, CtlFont)) + 26f);

            float y = -CPad;
            MakeCheck(font, Strings.OptShowReceiver, ref y, inner,
                () => GyroConfig.ShowReceiverAngle,
                v => { GyroConfig.ShowReceiverAngle = v; GyroConfig.Save("ShowReceiverAngle", v); },
                () => Strings.TipOptShowReceiver);
            MakeCheck(font, Strings.OptActiveReceiverOnly, ref y, inner,
                () => GyroConfig.ActiveReceiverOnly,
                v => { GyroConfig.ActiveReceiverOnly = v; GyroConfig.Save("ActiveReceiverOnly", v); },
                () => Strings.TipOptActiveReceiverOnly);
            MakeCheck(font, Strings.OptShowParallax, ref y, inner,
                () => GyroConfig.ShowParallax,
                v =>
                {
                    GyroConfig.ShowParallax = v;
                    GyroConfig.Save("ShowParallax", v);
                    Build(); // changes the row layout (sign column in every row)
                },
                () => Strings.TipOptShowParallax);
            MakeCheck(font, Strings.OptAutoReceiver, ref y, inner,
                () => GyroConfig.AutoReceiver,
                v => { GyroConfig.AutoReceiver = v; GyroConfig.Save("AutoReceiver", v); },
                () => Strings.TipOptAutoReceiver);
            MakeCheck(font, Strings.OptAutoParallax, ref y, inner,
                () => GyroConfig.AutoParallax,
                v => { GyroConfig.AutoParallax = v; GyroConfig.Save("AutoParallax", v); },
                () => Strings.TipOptAutoParallax);
            MakeCheck(font, Strings.OptRealisticTdc, ref y, inner,
                () => GyroConfig.RealisticTdc,
                v => { GyroConfig.RealisticTdc = v; GyroConfig.Save("RealisticTdc", v); },
                () => Strings.TipOptRealisticTdc);
            MakeCheck(font, Strings.OptRealisticSync, ref y, inner,
                () => GyroConfig.RealisticReceiverSync,
                v => { GyroConfig.RealisticReceiverSync = v; GyroConfig.Save("RealisticReceiverSync", v); },
                () => Strings.TipOptRealisticSync);
            MakeCheck(font, Strings.OptRealisticFire, ref y, inner,
                () => GyroConfig.SingleTorpedoLaunchFix,
                v => { GyroConfig.SingleTorpedoLaunchFix = v; GyroConfig.Save("SingleTorpedoLaunchFix", v); },
                () => Strings.TipOptRealisticFire);
            MakeCheck(font, Strings.OptTdcOperator, ref y, inner,
                () => GyroConfig.TdcOperatorRequired,
                v => { GyroConfig.TdcOperatorRequired = v; GyroConfig.Save("TdcOperatorRequired", v); TdcOperator.Apply(); },
                () => Strings.TipOptTdcOperator);

            settingsPanel.sizeDelta = new Vector2(inner + CPad * 2f, -y + CPad - RowGap);
            settingsPanel.gameObject.SetActive(settingsOpen);
        }

        void MakeCheck(TMP_FontAsset font, string text, ref float y, float width, Func<bool> get, Action<bool> set, Func<string> tip)
        {
            var row = NewRect("Opt_" + text, settingsPanel);
            PlaceV(row, CPad, y, width, RowH);
            y -= RowH + RowGap;

            var bg = row.gameObject.AddComponent<Image>();
            bg.color = Color.clear;
            bg.raycastTarget = true;

            // check box
            var box = NewRect("Box", row);
            box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(13f, 13f);
            box.anchoredPosition = new Vector2(3f, 0f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = DrumBlack;
            boxImg.raycastTarget = false;
            var boxEdge = box.gameObject.AddComponent<Outline>();
            boxEdge.effectColor = FrameEdge;
            boxEdge.effectDistance = new Vector2(1f, -1f);

            var fill = NewRect("Fill", box);
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0.5f, 0.5f);
            fill.sizeDelta = new Vector2(7f, 7f);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = BtnSelected;
            fillImg.raycastTarget = false;

            var label = NewText("Text", row, font, text, CtlFont, DigitLight);
            label.alignment = TextAlignmentOptions.Left;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(22f, 0f); lr.offsetMax = Vector2.zero;

            var check = new Check { Fill = fillImg, Bg = bg, Get = get };
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                try { set(!get()); }
                catch (Exception e) { Debug.LogException(e); }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var relay = AddTooltip(row.gameObject, tip);
            relay.OnHover = h => check.Hover = h;
            checks.Add(check);
        }

        float Header(TMP_FontAsset font, string text, float y, float inner, bool withLamp, Func<string> tip)
        {
            var row = NewRect("Header_" + text, controlsPanel);
            PlaceV(row, CPad, y, inner, RowH - 4f);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            AddTooltip(row.gameObject, tip);

            float textX = 0f;
            if (withLamp)
            {
                followLamp = MakeLamp(row, new Vector2(0f, 0.5f), new Vector2(6f, 0f), 10f, LampBlue);
                textX = 16f;
            }
            var t = NewText("Text", row, font, text, HeaderFont, LabelColor);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 2f;
            t.alignment = withLamp ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            var tr = t.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(textX, 0f); tr.offsetMax = Vector2.zero;
            return y - (RowH - 4f) - RowGap;
        }

        static float TextWidth(TMP_FontAsset font, string s, float size)
        {
            var go = new GameObject("tmp", typeof(RectTransform));
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            float w = t.GetPreferredValues(s).x;
            Destroy(go);
            return w;
        }

        void BuildTooltip(TMP_FontAsset font)
        {
            tipPanel = NewRect("GyroTooltip", transform);
            tipPanel.anchorMin = tipPanel.anchorMax = new Vector2(0.5f, 0.5f);
            tipPanel.pivot = new Vector2(0f, 1f);
            var bg = tipPanel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.06f, 0.96f);
            bg.raycastTarget = false;
            var edge = tipPanel.gameObject.AddComponent<Outline>();
            edge.effectColor = FrameEdge;
            edge.effectDistance = new Vector2(1f, -1f);
            var cg = tipPanel.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            tipText = NewText("Text", tipPanel, font, "", 13f, DigitLight);
            tipText.enableWordWrapping = true;
            tipText.richText = true;
            tipText.alignment = TextAlignmentOptions.TopLeft;
            var r = tipText.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(8f, 6f); r.offsetMax = new Vector2(-8f, -6f);
            tipPanel.gameObject.SetActive(false);
        }

        static void Frame(RectTransform r, bool raycast)
        {
            var frame = r.gameObject.AddComponent<Image>();
            frame.color = FrameColor;
            frame.raycastTarget = raycast;
            var edge = r.gameObject.AddComponent<Outline>();
            edge.effectColor = FrameEdge;
            edge.effectDistance = new Vector2(1f, -1f);
        }

        void MakeDrum(CounterRow row, Transform parent, TMP_FontAsset font, float x, bool inverse)
        {
            var digits = new TextMeshProUGUI[11];
            var cell = NewRect("Drum", parent);
            PlaceH(cell, x, DigitW, DigitH);
            var bg = cell.gameObject.AddComponent<Image>();
            bg.color = inverse ? DrumWhite : DrumBlack;
            bg.raycastTarget = false;
            cell.gameObject.AddComponent<RectMask2D>();

            var strip = NewRect("Strip", cell);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.sizeDelta = new Vector2(0f, DigitH * 11f);
            strip.anchoredPosition = Vector2.zero;

            for (int i = 0; i <= 10; i++)
            {
                var t = NewText("D" + i, strip, font, (i % 10).ToString(), DigitH * 0.8f, inverse ? DigitDark : DigitLight);
                t.alignment = TextAlignmentOptions.Center;
                var r = t.rectTransform;
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, DigitH);
                r.anchoredPosition = new Vector2(0f, -i * DigitH);
                digits[i] = t;
            }

            Shade(cell, true);
            Shade(cell, false);
            row.AddDrum(strip, digits);
        }

        static void Shade(RectTransform cell, bool top)
        {
            var s = NewRect(top ? "ShadeTop" : "ShadeBottom", cell);
            s.anchorMin = new Vector2(0f, top ? 1f : 0f);
            s.anchorMax = new Vector2(1f, top ? 1f : 0f);
            s.pivot = new Vector2(0.5f, top ? 1f : 0f);
            s.sizeDelta = new Vector2(0f, DigitH * 0.2f);
            s.anchoredPosition = Vector2.zero;
            var img = s.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = false;
        }

        // ------------------------------------------------------------------ lamps

        sealed class Lamp
        {
            public Image Glow, Core;
            public Color OnColor;
            int state = -1;

            public void Set(bool lit, Color color)
            {
                int s = lit ? (color == LampGreen ? 1 : color == LampRed ? 2 : 3) : 0;
                if (s == state) return;
                state = s;
                if (lit)
                {
                    Core.color = color;
                    Glow.color = new Color(color.r, color.g, color.b, 0.55f);
                }
                else
                {
                    Color c = OnColor;
                    Core.color = new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.22f, 1f);
                    Glow.color = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        static Lamp MakeLamp(RectTransform parent, Vector2 anchor, Vector2 pos, float size, Color onColor)
        {
            var holder = NewRect("Lamp", parent);
            holder.anchorMin = holder.anchorMax = anchor;
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(size, size);
            holder.anchoredPosition = pos;

            var glow = Disc("Glow", holder, GlowSprite(), size * 2.1f, Color.clear);
            Disc("Bezel", holder, CircleSprite(), size + 2.5f, new Color(0.03f, 0.03f, 0.03f, 1f));
            var core = Disc("Core", holder, CircleSprite(), size, onColor);
            var hl = Disc("Highlight", holder, CircleSprite(), size * 0.35f, new Color(1f, 1f, 1f, 0.35f));
            hl.rectTransform.anchoredPosition = new Vector2(-size * 0.18f, size * 0.18f);

            var lamp = new Lamp { Glow = glow, Core = core, OnColor = onColor };
            lamp.Set(false, onColor);
            return lamp;
        }

        static Image Disc(string name, RectTransform parent, Sprite sprite, float size, Color color)
        {
            var r = NewRect(name, parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Sprite CircleSprite()
        {
            if (circleSprite == null) circleSprite = MakeRadialSprite(false);
            return circleSprite;
        }

        static Sprite GlowSprite()
        {
            if (glowSprite == null) glowSprite = MakeRadialSprite(true);
            return glowSprite;
        }

        static Sprite MakeRadialSprite(bool soft)
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            float r = s * 0.5f;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = soft ? Mathf.Pow(Mathf.Clamp01(1f - d / r), 2f) : Mathf.Clamp01(r - d);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        // ------------------------------------------------------------------ buttons

        sealed class Btn
        {
            public Button Button;
            public Image Bg;
            public TextMeshProUGUI Label;
            public bool Hover;

            public void Paint(bool selected, bool available)
            {
                Button.interactable = available;
                Bg.color = selected ? BtnSelected : (Hover && available ? BtnHover : BtnIdle);
                Color txt = selected ? DigitDark : DigitLight;
                if (!available) txt.a = 0.35f;
                Label.color = txt;
            }
        }

        Btn MakeButton(RectTransform parent, TMP_FontAsset font, string text, ref float y, float width,
                       UnityEngine.Events.UnityAction onClick, Func<string> tip)
        {
            var r = NewRect("Btn_" + text, parent);
            PlaceV(r, CPad, y, width, RowH);
            y -= RowH + RowGap;
            return BuildButton(r, font, text, CtlFont, onClick, tip);
        }

        /// <summary>Turns an already placed rect into a button with a centred label.</summary>
        Btn BuildButton(RectTransform r, TMP_FontAsset font, string text, float fontSize,
                        UnityEngine.Events.UnityAction onClick, Func<string> tip)
        {
            var bg = r.gameObject.AddComponent<Image>();
            bg.color = BtnIdle;
            bg.raycastTarget = true;
            var outline = r.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            var label = NewText("Text", r, font, text, fontSize, DigitLight);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            var button = r.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.targetGraphic = bg;

            var btn = new Btn { Button = button, Bg = bg, Label = label };
            button.onClick.AddListener(() =>
            {
                try { onClick(); }
                catch (Exception e) { Debug.LogException(e); }
                // drop keyboard focus so Space/Enter in the scope don't re-press it
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var relay = AddTooltip(r.gameObject, tip);
            relay.OnHover = h => btn.Hover = h;
            return btn;
        }

        // ------------------------------------------------------------------ tooltips & dragging

        sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public GyroCounterUI Owner;
            public Func<string> Tip;
            public Action<bool> OnHover;
            public bool Hovered;

            public void OnPointerEnter(PointerEventData e)
            {
                Hovered = true;
                if (OnHover != null) OnHover(true);
                if (Owner == null || Tip == null) return;
                // Unity also sends "enter" to every parent; only the innermost element
                // under the pointer (e.g. the receiver row, not the TDC row) shows its tip
                var innermost = e.pointerEnter != null ? e.pointerEnter.GetComponentInParent<HoverRelay>() : null;
                if (innermost != null && innermost != this) return;
                Owner.RequestTip(Tip);
            }

            public void OnPointerExit(PointerEventData e)
            {
                Hovered = false;
                if (OnHover != null) OnHover(false);
                if (Owner == null || Tip == null) return;
                Owner.ReleaseTip(Tip);
                // back onto a parent element that is still hovered (e.g. lamp -> counter)
                var parent = transform.parent != null ? transform.parent.GetComponentInParent<HoverRelay>() : null;
                if (parent != null && parent.Hovered && parent.Tip != null) Owner.RequestTip(parent.Tip);
            }

            void OnDisable()
            {
                Hovered = false;
                if (OnHover != null) OnHover(false);
            }
        }

        sealed class DragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public GyroCounterUI Owner;
            public bool Controls;
            public bool Dials;   // the TDC dial window
            bool dragging;

            static bool CtrlHeld { get { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }

            public void OnBeginDrag(PointerEventData e)
            {
                dragging = CtrlHeld;
                if (!dragging) { e.pointerDrag = null; return; }
                if (Owner != null) Owner.HideTip();
            }

            public void OnDrag(PointerEventData e)
            {
                if (!dragging || Owner == null) return;
                float k = Owner.canvas != null && Owner.canvas.scaleFactor > 0f ? Owner.canvas.scaleFactor : 1f;
                Vector2 d = e.delta / k;
                if (Dials) { GyroConfig.DialsOffsetX += d.x; GyroConfig.DialsOffsetY += d.y; }
                else if (Controls) { GyroConfig.ControlsOffsetX += d.x; GyroConfig.ControlsOffsetY += d.y; }
                else { GyroConfig.OffsetX += d.x; GyroConfig.OffsetY += d.y; }
            }

            public void OnEndDrag(PointerEventData e)
            {
                if (!dragging) return;
                dragging = false;
                if (Dials)
                {
                    GyroConfig.Save("DialsOffsetX", GyroConfig.DialsOffsetX);
                    GyroConfig.Save("DialsOffsetY", GyroConfig.DialsOffsetY);
                }
                else if (Controls)
                {
                    GyroConfig.Save("ControlsOffsetX", GyroConfig.ControlsOffsetX);
                    GyroConfig.Save("ControlsOffsetY", GyroConfig.ControlsOffsetY);
                }
                else
                {
                    GyroConfig.Save("OffsetX", GyroConfig.OffsetX);
                    GyroConfig.Save("OffsetY", GyroConfig.OffsetY);
                }
            }
        }

        HoverRelay AddTooltip(GameObject go, Func<string> tip)
        {
            var h = go.AddComponent<HoverRelay>();
            h.Owner = this;
            h.Tip = tip;
            return h;
        }

        DragRelay AddDrag(GameObject go, bool controls)
        {
            var d = go.AddComponent<DragRelay>();
            d.Owner = this;
            d.Controls = controls;
            return d;
        }

        void RequestTip(Func<string> tip)
        {
            if (!GyroConfig.Tooltips) return;
            pendingTip = tip;
            pendingTipTime = Time.unscaledTime + TipDelay;
            if (tipShown) ShowTipNow(); // moving between elements: switch immediately
        }

        void ReleaseTip(Func<string> tip)
        {
            if (pendingTip == tip) HideTip();
        }

        void HideTip()
        {
            pendingTip = null;
            tipShown = false;
            if (tipPanel != null) tipPanel.gameObject.SetActive(false);
        }

        void ShowTipNow()
        {
            if (tipPanel == null || pendingTip == null) return;
            string text = pendingTip();
            tipText.text = text;
            float w = Mathf.Min(tipText.GetPreferredValues(text).x + 1f, TipMaxWidth - 16f);
            float h = tipText.GetPreferredValues(text, w, 0f).y;
            tipPanel.sizeDelta = new Vector2(w + 16f, h + 12f);
            tipPanel.gameObject.SetActive(true);
            tipPanel.SetAsLastSibling();
            tipShown = true;
            PositionTip();
        }

        void PositionTip()
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out local)) return;
            Rect cr = canvasRect.rect;
            Vector2 size = tipPanel.sizeDelta;
            float x = local.x + 16f;
            float y = local.y - 18f;
            if (x + size.x > cr.xMax - 4f) x = local.x - 12f - size.x;   // flip to the left near the right edge
            if (y - size.y < cr.yMin + 4f) y = local.y + 12f + size.y;   // flip above near the bottom
            tipPanel.anchoredPosition = new Vector2(x, y);
        }

        // ------------------------------------------------------------------ helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
        {
            var r = NewRect(name, parent);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Horizontal layout: left-anchored, vertically centred.</summary>
        static void PlaceH(RectTransform r, float x, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x, 0f);
        }

        /// <summary>Vertical layout: top-left anchored, y counts down from the top (negative).</summary>
        static void PlaceV(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x, y);
        }

        static void ResolveFields()
        {
            if (fieldsResolved) return;
            fieldsResolved = true;
            tdcField = TryRef<PeriscopeUI, TorpedoCalculator>("torpedoCalculator");
            fontSourceField = TryRef<PeriscopeUI, TextMeshProUGUI>("shipNameLabel");
            followToggleField = TryRef<PeriscopeUI, Toggle>("bearingFollowUpToggle");
            bowReceiverField = TryRef<TorpedoFireControlBoxes, TorpedoLauncherGroup>("bowReceiver");
            sternReceiverField = TryRef<TorpedoFireControlBoxes, TorpedoLauncherGroup>("sternReceiver");
            uzoField = TryRef<TorpedoFireControlBoxes, Periscope>("uzo");
            obsScopeField = TryRef<TorpedoFireControlBoxes, Periscope>("observationPeriscope");
            attackScopeField = TryRef<TorpedoFireControlBoxes, Periscope>("attackPeriscope");
            elevatorUpField = AccessTools.Field(typeof(PeriscopeUI), "elevatorUp");
            elevatorDownField = AccessTools.Field(typeof(PeriscopeUI), "elevatorDown");
            followUpMethod = AccessTools.Method(typeof(PeriscopeUI), "OnBearingFollowUpChanged", new[] { typeof(bool) });
            if (followUpMethod == null) Debug.LogWarning(ScopeTdcControlsMod.Tag + " PeriscopeUI.OnBearingFollowUpChanged not found; follow-up buttons will set the TDC directly.");
        }

        static AccessTools.FieldRef<TObj, TField> TryRef<TObj, TField>(string name) where TObj : class
        {
            try { return AccessTools.FieldRefAccess<TObj, TField>(name); }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " field " + typeof(TObj).Name + "." + name + " not found: " + e.Message);
                return null;
            }
        }

        static TMP_FontAsset ResolveFont()
        {
            ResolveFields();
            try
            {
                if (periscopeUI != null && fontSourceField != null)
                {
                    var src = fontSourceField(periscopeUI);
                    if (src != null && src.font != null) return src.font;
                }
            }
            catch { }
            return TMP_Settings.defaultFontAsset;
        }

        // ------------------------------------------------------------------ game state

        static T Get<T>() where T : class
        {
            try
            {
                var fw = InjectionFramework.Instance;
                return fw != null ? fw.GetInstance<T>() : null;
            }
            catch { return null; }
        }

        static bool GameUiHidden()
        {
            var ui = Get<GameUI>();
            return ui != null && ui.CurrentMode == GameUI.Mode.Disabled;
        }

        static TorpedoCalculator Tdc()
        {
            ResolveFields();
            TorpedoCalculator tdc = null;
            try
            {
                if (periscopeUI != null && tdcField != null) tdc = tdcField(periscopeUI);
                if (tdc == null)
                {
                    var ship = Get<PlayerShip>();
                    if (ship != null) tdc = ship.GetEquipment<TorpedoCalculator>();
                }
            }
            catch { }
            return tdc;
        }

        static TorpedoFireControlBoxes Boxes()
        {
            try
            {
                var ship = Get<PlayerShip>();
                return ship != null ? ship.GetEquipment<TorpedoFireControlBoxes>() : null;
            }
            catch { return null; }
        }

        static void SetFollowUp(bool on)
        {
            var tdc = Tdc();
            if (tdc == null) return;
            if (periscopeUI != null && followUpMethod != null)
            {
                // vanilla handler: sets the TDC, picks the bearing transmitter
                // for this scope/UZO and has the crew call it out
                followUpMethod.Invoke(periscopeUI, new object[] { on });
                if (followToggleField != null)
                {
                    var t = followToggleField(periscopeUI);
                    if (t != null) t.SetIsOnWithoutNotify(on);
                }
            }
            else
            {
                tdc.BearingFollowUpEnabled = on;
                FollowUpOverride.UserSet(on);
            }
        }

        static bool HasScope(TorpedoFireControlBoxes boxes, AccessTools.FieldRef<TorpedoFireControlBoxes, Periscope> field)
        {
            if (boxes == null) return false;
            if (field == null) return true; // can't tell: allow
            return field(boxes) != null;
        }

        static void SetTransmitter(BearingTransmitter value)
        {
            TransmitterOverride.SetManual(Boxes(), value);
        }

        static void SetParallax(float direction)
        {
            if (GyroConfig.AutoParallax) return; // buttons are indicators only while auto is on
            Parallax.Set(Tdc(), direction);
        }

        static void SetReceiver(GyroAngleReceiverSetting setting)
        {
            var boxes = Boxes();
            if (boxes == null) return;
            ReceiverOverride.SetManual(boxes, setting); // keeps the periscope torpedo panel from overriding it
            boxes.GyroAngleReceiver = setting;
        }

        // ------------------------------------------------------------------ runtime

        void SetVisible(bool v)
        {
            if (canvas != null) canvas.enabled = v;
        }

        void Update()
        {
            if (ScopeAim.Active)
            {
                if (periscopeUI == null) ScopeAim.Stop();
                else ScopeAim.Tick(Tdc());
            }
            PeriscopeTravel.Tick();

            if (GyroConfig.ToggleKey != KeyCode.None && periscopeUI != null && Input.GetKeyDown(GyroConfig.ToggleKey))
            {
                userHidden = !userHidden;
                SetVisible(!userHidden);
                if (userHidden) HideTip();
            }

            if (pendingTip != null)
            {
                if (!tipShown && Time.unscaledTime >= pendingTipTime) ShowTipNow();
                else if (tipShown) PositionTip();
            }
        }

        void LateUpdate()
        {
            if (canvas == null || !canvas.enabled || counterPanel == null) return;
            if (periscopeUI == null || !periscopeUI.isActiveAndEnabled) { SetVisible(false); return; }

            bool uiHidden = GameUiHidden();
            var tdc = Tdc();
            bool show = tdc != null && !(uiHidden && GyroConfig.HideWithGameUI);
            counterPanel.gameObject.SetActive(show);
            if (controlsPanel != null)
            {
                bool panel = show && GyroConfig.ControlsVisible;
                if (controlsPanel.gameObject.activeSelf != panel) controlsPanel.gameObject.SetActive(panel);
            }
            bool dialsOn = show && GyroConfig.ControlsVisible && dialsPanel != null;
            if (dialsPanel != null && dialsPanel.gameObject.activeSelf != dialsOn) dialsPanel.gameObject.SetActive(dialsOn);
            UpdateElevatorButtons(uiHidden);
            if (!show) { HideTip(); return; }

            UpdateCounter(tdc);
            UpdateControls(tdc);
            PositionCounter(uiHidden);
            PositionControls(uiHidden);
            if (dialsOn)
            {
                UpdateDials(tdc, IsTdcManned(tdc));
                PositionDials();
            }
        }

        /// <summary>Same test the game uses for the TDC station: someone working at its Interactable.</summary>
        bool IsTdcManned(TorpedoCalculator tdc)
        {
            if (!GyroConfig.TdcOperatorRequired) return true; // works without an operator: never shown as unmanned
            if (!ReferenceEquals(tdc, stationTdc))
            {
                stationTdc = tdc;
                tdcStation = tdc != null ? tdc.GetComponent<Interactable>() : null;
                if (tdcStation == null && tdc != null) tdcStation = tdc.GetComponentInParent<Interactable>();
            }
            if (tdcStation == null) return true; // can't tell: treat as manned
            var list = tdcStation.InteractingCharacters;
            return list != null && list.Count > 0;
        }

        void UpdateCounter(TorpedoCalculator tdc)
        {
            Drive(tdcRow, tdc.GyroAngle);

            // TDC not manned: grey TDC counter + note, every other row hidden
            bool manned = IsTdcManned(tdc);
            if (unmannedVeil.gameObject.activeSelf == manned)
            {
                unmannedVeil.gameObject.SetActive(!manned);
                unmannedNote.gameObject.SetActive(!manned);
                unmannedVeil.transform.SetAsLastSibling(); // over the drums and lamp of the TDC row
            }

            // parallax readout
            float y = -RowStackGap;
            if (parRow != null)
            {
                parRow.Panel.gameObject.SetActive(manned);
                if (manned)
                {
                    parRow.Panel.anchoredPosition = new Vector2(0f, y);
                    y -= parRow.Panel.sizeDelta.y + RowStackGap;
                    Render(parRow, Parallax.CorrectionDegrees(tdc)); // the TDC's parallax motor already smooths it
                }
            }

            // receiver readouts: the gyro angle actually set on the tube groups
            var group = tdc.GyroAngleReceiver;
            TorpedoLauncherGroup bow, stern;
            ReceiverGroups(out bow, out stern);

            TorpedoLauncherGroup groupA = null, groupB = null;
            if (GyroConfig.ShowReceiverAngle && manned)
            {
                if (GyroConfig.ActiveReceiverOnly) groupA = group; // only the selected one, none while off
                else { groupA = bow; groupB = stern; }             // every receiver the boat has, even while off
                if (groupA == null && groupB != null) { groupA = groupB; groupB = null; }
            }
            ShowReceiverRow(rcvRowA, groupA, group, ref y);
            ShowReceiverRow(rcvRowB, groupB, group, ref y);

            if (syncLamp != null)
            {
                if (group == null) syncLamp.Set(false, LampGreen);
                else
                {
                    bool synced = Mathf.Abs(Mathf.DeltaAngle(group.SetGyroAngle, group.ReceivedGyroAngle)) < SyncTolerance;
                    syncLamp.Set(true, synced ? LampGreen : LampRed);
                }
            }
        }

        static void ReceiverGroups(out TorpedoLauncherGroup bow, out TorpedoLauncherGroup stern)
        {
            bow = stern = null;
            var boxes = Boxes();
            if (boxes == null) return;
            if (bowReceiverField != null) bow = bowReceiverField(boxes);
            if (sternReceiverField != null) stern = sternReceiverField(boxes);
        }

        void ShowReceiverRow(CounterRow row, TorpedoLauncherGroup shown, TorpedoLauncherGroup active, ref float y)
        {
            bool visible = shown != null;
            if (row.Panel.gameObject.activeSelf != visible)
            {
                row.Panel.gameObject.SetActive(visible);
                row.Displayed = float.NaN; // jump straight to the value when it reappears
            }
            if (!visible) return;

            row.Panel.anchoredPosition = new Vector2(0f, y);
            y -= row.Panel.sizeDelta.y + RowStackGap;
            row.ShownGroup = shown;

            string label = IsSternGroup(shown) ? Strings.Stern : Strings.Bow;
            if (row.Label.text != label) row.Label.text = label;
            // with all receivers shown, the one receiving the TDC angle has the bright label
            bool isActive = ReferenceEquals(shown, active);
            row.Label.color = isActive || GyroConfig.ActiveReceiverOnly ? LabelColor : new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.45f);
            Drive(row, shown.SetGyroAngle);
        }

        static void Drive(CounterRow row, float target)
        {
            if (row == null || float.IsNaN(target) || float.IsInfinity(target)) return;
            if (float.IsNaN(row.Displayed) || GyroConfig.RollSpeed <= 0f)
                row.Displayed = target;
            else
                row.Displayed = Mathf.MoveTowardsAngle(row.Displayed, target, GyroConfig.RollSpeed * Time.unscaledDeltaTime);
            Render(row, Normalize(row.Displayed));
        }

        static bool IsSternGroup(TorpedoLauncherGroup group)
        {
            var boxes = Boxes();
            if (boxes != null)
            {
                if (sternReceiverField != null && ReferenceEquals(sternReceiverField(boxes), group)) return true;
                if (bowReceiverField != null && ReferenceEquals(bowReceiverField(boxes), group)) return false;
                if (boxes.GyroAngleReceiver == GyroAngleReceiverSetting.Stern) return true;
            }
            return !group.name.Contains("Bow");
        }

        void UpdateControls(TorpedoCalculator tdc)
        {
            if (controlsPanel == null) return;

            bool follow = tdc.BearingFollowUpEnabled;
            followLamp.Set(!follow, LampBlue); // blue = NOT following, dark = following
            btnFollow.Paint(follow, true);
            btnNoFollow.Paint(!follow, true);

            var boxes = Boxes();
            bool haveBoxes = boxes != null;
            var setting = haveBoxes ? boxes.GyroAngleReceiver : GyroAngleReceiverSetting.Off;
            bool off = setting == GyroAngleReceiverSetting.Off || setting == GyroAngleReceiverSetting.Off2;
            bool hasBow = haveBoxes && (bowReceiverField == null || bowReceiverField(boxes) != null);
            bool hasStern = haveBoxes && (sternReceiverField == null || sternReceiverField(boxes) != null);
            btnOff.Paint(haveBoxes && off, haveBoxes);
            btnBow.Paint(setting == GyroAngleReceiverSetting.Bow, hasBow);
            btnStern.Paint(setting == GyroAngleReceiverSetting.Stern, hasStern);

            // bearing source: grey out scopes this boat doesn't have
            var tx = haveBoxes ? boxes.BearingTransmitter : BearingTransmitter.None;
            btnTxNone.Paint(haveBoxes && tx == BearingTransmitter.None, haveBoxes);
            btnTxUzo.Paint(tx == BearingTransmitter.UZO, HasScope(boxes, uzoField));
            btnTxObs.Paint(tx == BearingTransmitter.ObservationPeriscope, HasScope(boxes, obsScopeField));
            btnTxAttack.Paint(tx == BearingTransmitter.AttackPeriscope, HasScope(boxes, attackScopeField));

            // parallax direction: indicator only while auto is on
            bool fwd = Parallax.IsForward(tdc);
            bool manualPar = !GyroConfig.AutoParallax;
            btnParFwd.Paint(fwd, manualPar);
            btnParBack.Paint(!fwd, manualPar);

            if (btnSettings != null) btnSettings.Paint(settingsOpen, true);
            if (settingsOpen)
            {
                foreach (var c in checks)
                {
                    c.Fill.enabled = c.Get();
                    c.Bg.color = c.Hover ? new Color(BtnHover.r, BtnHover.g, BtnHover.b, 0.6f) : Color.clear;
                }
            }
        }

        static float Normalize(float a)
        {
            a %= 360f;
            if (a < 0f) a += 360f;
            return a;
        }

        /// <summary>Odometer logic: lowest drum turns smoothly, higher drums only roll during carry.</summary>
        static void Render(CounterRow row, float angle)
        {
            bool tenths = row.Tenths;
            float value = angle;
            float wrapLimit = 360f;

            if (row.Signed)
            {
                // signed small angle (parallax): +/- sign and the magnitude
                value = Mathf.Min(Mathf.Abs(angle), 999f);
                wrapLimit = float.MaxValue;
                if (row.Side != null)
                    row.Side.text = value < 0.05f ? "" : (angle < 0f ? "-" : "+");
            }
            else if (GyroConfig.LeftRight)
            {
                float signed = angle > 180f ? angle - 360f : angle;
                value = Mathf.Abs(signed);
                wrapLimit = float.MaxValue; // 0..180, no wrap
                if (row.Side != null)
                    row.Side.text = value < (tenths ? 0.05f : 0.5f) ? "" : (signed < 0f ? "L" : "R");
            }

            double unit = tenths ? 10.0 : 1.0;
            double n = value * unit;
            double nLimit = wrapLimit * unit;
            var strips = row.Strips;
            int count = strips.Count;

            // final step before 360 (the lowest drum passing its last digit): every drum rolls
            // over to 0 together with it (359 -> 000), using that step's fraction. The normal
            // carry rule only covers drums whose right-hand neighbours all show 9 (the tens),
            // not the hundreds, which sit on 59.
            bool wrapping = n > nLimit - 1.0;
            double wrapRoll = wrapping ? n - Math.Floor(n) : 0.0;

            for (int k = 0; k < count; k++)
            {
                int idx = count - 1 - k;
                double p = Math.Pow(10.0, k);
                double pos;
                int patch = -1;
                if (k == 0)
                {
                    pos = n % 10.0;
                }
                else
                {
                    // a drum turns while every drum to its right is passing 9 -> 0
                    double digit = Math.Floor(n / p) % 10.0;
                    double rem = n % p;
                    double roll = rem - (p - 1.0);
                    if (wrapping) roll = wrapRoll;
                    pos = digit + (roll > 0.0 ? roll : 0.0);
                    if (wrapping && roll > 0.0) patch = (int)digit + 1; // that slot shows "0" while rolling over
                }
                SetWrapPatch(row, idx, patch);
                row.Strips[idx].anchoredPosition = new Vector2(0f, (float)(pos * DigitH));
            }
        }

        /// <summary>Relabels one slot of a drum as "0" (or restores it), for the 359 -> 000 roll-over.</summary>
        static void SetWrapPatch(CounterRow row, int drum, int slot)
        {
            if (row.Patched[drum] == slot) return;
            var digits = row.Digits[drum];
            int old = row.Patched[drum];
            if (old >= 0 && old < digits.Length) digits[old].text = (old % 10).ToString();
            if (slot >= 0 && slot < digits.Length) digits[slot].text = "0";
            row.Patched[drum] = slot;
        }

        // ------------------------------------------------------------------ positioning

        bool ScreenRect(RectTransform rt, out Vector2 min, out Vector2 max)
        {
            rt.GetWorldCorners(corners);
            var c = rt.GetComponentInParent<Canvas>();
            Canvas rc = c != null ? c.rootCanvas : null;
            Camera cam = (rc == null || rc.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rc.worldCamera;
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return max.x > min.x && max.y > min.y;
        }

        Vector2 ToLocal(Vector2 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
            return local;
        }

        /// <summary>Store where the bearing tape is (screen fraction), only when it actually moved.</summary>
        static void RememberTapeAnchor(Vector2 screenPoint)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            float fx = screenPoint.x / Screen.width, fy = screenPoint.y / Screen.height;
            bool known = !float.IsNaN(GyroConfig.TapeAnchorX) && !float.IsNaN(GyroConfig.TapeAnchorY);
            if (known && Mathf.Abs(fx - GyroConfig.TapeAnchorX) < 0.001f && Mathf.Abs(fy - GyroConfig.TapeAnchorY) < 0.001f) return;
            GyroConfig.TapeAnchorX = fx;
            GyroConfig.TapeAnchorY = fy;
            GyroConfig.SavePrecise("TapeAnchorX", fx);
            GyroConfig.SavePrecise("TapeAnchorY", fy);
        }

        void PositionCounter(bool uiHidden)
        {
            Vector2 pivot = new Vector2(0.5f, GyroConfig.Above ? 0f : 1f);

            // only learn the spot from the bearing tape while the game UI is in its
            // normal state; with the UI hidden (Alt) keep the remembered spot
            bool compassFresh = lastCompass != null && lastCompass.isActiveAndEnabled && Time.frameCount - lastCompassFrame <= 5;
            if (GyroConfig.AnchorToCompass && compassFresh && !uiHidden)
            {
                Vector2 mn, mx;
                if (ScreenRect(lastCompass.rectTransform, out mn, out mx))
                {
                    float cx = (mn.x + mx.x) * 0.5f;
                    Vector2 edge = GyroConfig.Above ? new Vector2(cx, mx.y) : new Vector2(cx, mn.y);
                    counterLatch = ToLocal(edge) + new Vector2(0f, GyroConfig.Above ? GyroConfig.Gap : -GyroConfig.Gap);
                    if (!hasCounterLatch || !loggedCounterAnchor)
                    {
                        loggedCounterAnchor = true;
                        Debug.Log(ScopeTdcControlsMod.Tag + " counter anchored under bearing tape '" + lastCompass.name + "'");
                    }
                    hasCounterLatch = true;
                    RememberTapeAnchor(edge);
                }
            }

            Vector2 basePos;
            if (GyroConfig.AnchorToCompass && hasCounterLatch)
                basePos = counterLatch;
            else if (GyroConfig.AnchorToCompass && !float.IsNaN(GyroConfig.TapeAnchorX) && !float.IsNaN(GyroConfig.TapeAnchorY))
                // after loading a game the tape may not report until the UI is refreshed:
                // start where it was last seen instead of the fallback spot
                basePos = ToLocal(new Vector2(Screen.width * GyroConfig.TapeAnchorX, Screen.height * GyroConfig.TapeAnchorY))
                          + new Vector2(0f, GyroConfig.Above ? GyroConfig.Gap : -GyroConfig.Gap);
            else
                basePos = ToLocal(new Vector2(Screen.width * GyroConfig.FallbackX, Screen.height * GyroConfig.FallbackY));

            // above the tape: leave room for the rows hanging underneath, always the
            // most there can be, so the TDC row doesn't jump when rows appear/disappear
            float reserve = 0f;
            if (GyroConfig.Above && rcvRowA != null)
            {
                float rowH = rcvRowA.Panel.sizeDelta.y + RowStackGap;
                if (parRow != null) reserve += parRow.Panel.sizeDelta.y + RowStackGap;
                if (GyroConfig.ShowReceiverAngle)
                {
                    int rows = 1;
                    if (!GyroConfig.ActiveReceiverOnly)
                    {
                        TorpedoLauncherGroup b, s;
                        ReceiverGroups(out b, out s);
                        rows = (b != null && s != null) ? 2 : 1;
                    }
                    reserve += rows * rowH;
                }
                reserve *= GyroConfig.Scale;
            }

            counterPanel.pivot = pivot;
            counterPanel.anchoredPosition = basePos + new Vector2(GyroConfig.OffsetX, GyroConfig.OffsetY + reserve);
        }

        void PositionControls(bool uiHidden)
        {
            if (controlsPanel == null) return;

            if (GyroConfig.ControlsAtElevator && !uiHidden)
            {
                Vector2 mn, mx;
                if (ElevatorRect(out mn, out mx))
                {
                    Vector2 lmin = ToLocal(mn), lmax = ToLocal(mx);
                    float panelW = controlsPanel.sizeDelta.x * GyroConfig.Scale;
                    float halfW = canvasRect.rect.width * 0.5f;
                    float cy = (lmin.y + lmax.y) * 0.5f;
                    if (lmax.x + GyroConfig.ControlsGap + panelW <= halfW - 4f)
                    {
                        controlsLatch = new Vector2(lmax.x + GyroConfig.ControlsGap, cy);
                        controlsLatchPivot = new Vector2(0f, 0.5f);
                    }
                    else
                    {
                        // no room on the right edge of the screen: sit on the left of the control
                        controlsLatch = new Vector2(lmin.x - GyroConfig.ControlsGap, cy);
                        controlsLatchPivot = new Vector2(1f, 0.5f);
                    }
                    if (!loggedControlsAnchor)
                    {
                        loggedControlsAnchor = true;
                        Debug.Log(ScopeTdcControlsMod.Tag + " controls anchored " + (controlsLatchPivot.x == 0f ? "right" : "left") + " of the periscope raise/lower control");
                    }
                    hasControlsLatch = true;
                }
            }

            Vector2 basePos;
            Vector2 pivot;
            if (GyroConfig.ControlsAtElevator && hasControlsLatch)
            {
                basePos = controlsLatch;
                pivot = controlsLatchPivot;
            }
            else
            {
                basePos = ToLocal(new Vector2(Screen.width * GyroConfig.ControlsX, Screen.height * GyroConfig.ControlsY));
                pivot = new Vector2(0.5f, 0.5f);
            }
            controlsPanel.pivot = pivot;
            controlsPanel.anchoredPosition = basePos + new Vector2(GyroConfig.ControlsOffsetX, GyroConfig.ControlsOffsetY);

            if (settingsPanel != null)
            {
                // hang the drop-down under the panel, flush with the side that faces away from the elevator
                float px = pivot.x;
                settingsPanel.anchorMin = settingsPanel.anchorMax = new Vector2(px, 0f);
                settingsPanel.pivot = new Vector2(px, 1f);
                settingsPanel.anchoredPosition = new Vector2(0f, -3f);
            }
        }

        /// <summary>Screen bounds of the periscope raise/lower buttons (vanilla PeriscopeUI).</summary>
        bool ElevatorRect(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            ResolveFields();
            foreach (var f in new[] { elevatorUpField, elevatorDownField })
            {
                if (f == null || periscopeUI == null) continue;
                var comp = f.GetValue(periscopeUI) as Component;
                if (comp == null || !comp.gameObject.activeInHierarchy) continue;
                var rt = comp.transform as RectTransform;
                if (rt == null) continue;
                Vector2 a, b;
                if (!ScreenRect(rt, out a, out b)) continue;
                min = Vector2.Min(min, a);
                max = Vector2.Max(max, b);
                any = true;
            }
            return any;
        }
    }
}
