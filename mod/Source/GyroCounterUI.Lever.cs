using System;
using System.Text;
using TMPro;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Launch lever window (T-Abf.-Schalter): Aus / Schuß, spring-loaded.
    /// The lever rests at Aus; pull it down to Schuß to fire and it springs
    /// back to Aus.
    ///
    /// Firing goes through the boat's own launch lever
    /// (TorpedoLaunchLever.Launch, the same call the crew's "use launch lever"
    /// action makes; it also animates the lever in the boat), with that
    /// action's conditions: not while torpedoes are already being launched, and
    /// every checked tube must be flooded. Without a lever in the scene it
    /// calls PlayerShip.LaunchAllTorpedoes with the lever's parameters.
    ///
    /// The ready lamp also checks what the lever's own tooltip warns about:
    /// the gyro angle receiver must be on the checked tubes' end (bow tubes =
    /// Bug, stern tubes = Heck), unless that group's gyros are set by hand.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float LeverW = 76f;
        const float LeverSlotH = 84f;
        const float LeverSpring = 0.45f;   // seconds at Schuß before springing back

        enum LeverPos { Off = 0, Fire = 1 }

        static readonly Color LeverRed = new Color(0.92f, 0.32f, 0.26f, 1f);

        RectTransform leverPanel, leverHandle, leverTrack;
        string leverState;              // what the ready lamp means right now (its pop-up)
        Lamp leverLamp;
        LeverPos leverPos = LeverPos.Off;
        float leverShown;               // animated handle position, 0 = Aus .. 1 = Schuß
        bool leverHeld;                 // mouse button down on the lever
        float leverSpringAt = float.NaN;
        float leverTopY, leverSlotX;
        string leverSound;              // the boat's firing station knob click, used for the lever
        LeverPos leverSoundPos = LeverPos.Off;

        // ------------------------------------------------------------------ build

        void BuildLever(TMP_FontAsset font)
        {
            leverPanel = NewRect("FireLever", transform);
            leverPanel.anchorMin = leverPanel.anchorMax = leverPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(leverPanel);
            AddDrag(leverPanel.gameObject, false).Lever = true;

            float y = -DialPad;
            var title = NewText("Title", leverPanel, font, Strings.LeverTitle, TitleFont, BoxInk);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 1f;
            title.lineSpacing = -12f;
            title.alignment = TextAlignmentOptions.Center;
            title.enableWordWrapping = true;
            PlaceV(title.rectTransform, DialPad - 4f, y, LeverW + 8f, 30f);
            y -= 30f + 4f;

            // slot on the right, the two plates on the left at its ends
            float slotX = DialPad + LeverW - 14f;
            leverTrack = NewRect("Track", leverPanel);
            PlaceV(leverTrack, slotX - 4f, y, 8f, LeverSlotH);
            var trackImg = leverTrack.gameObject.AddComponent<Image>();
            trackImg.color = DrumBlack;
            trackImg.raycastTarget = true; // pressing the slot pulls the lever too
            leverTrack.gameObject.AddComponent<LeverHold>().Owner = this;
            var trackEdge = leverTrack.gameObject.AddComponent<Outline>();
            trackEdge.effectColor = FrameEdge;
            trackEdge.effectDistance = new Vector2(1f, -1f);

            string[] names = { Strings.LeverOff, Strings.LeverFire };
            for (int i = 0; i < 2; i++)
            {
                float top = y - (LeverSlotH - 18f) * i;
                var plate = NewRect("Plate" + i, leverPanel);
                PlaceV(plate, DialPad, top, 42f, 18f);
                var img = plate.gameObject.AddComponent<Image>();
                img.color = DrumBlack;
                var t = NewText("Text", plate, font, names[i], CtlFont, i == 1 ? LeverRed : DialScale);
                t.alignment = TextAlignmentOptions.Center;
                var tr = t.rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = tr.offsetMax = Vector2.zero;
                plate.gameObject.AddComponent<LeverHold>().Owner = this;
                AddTooltip(plate.gameObject, () => Strings.TipLever);
            }

            // the handle: a dark grip lying across the slot
            leverHandle = NewRect("Handle", leverPanel);
            leverHandle.anchorMin = leverHandle.anchorMax = new Vector2(0f, 1f);
            leverHandle.pivot = new Vector2(0.5f, 0.5f);
            leverHandle.sizeDelta = new Vector2(14f, 38f);
            leverHandle.localEulerAngles = new Vector3(0f, 0f, 90f);
            var grip = leverHandle.gameObject.AddComponent<Image>();
            grip.sprite = HandleSprite();
            grip.color = KnobHandle;
            var gripEdge = leverHandle.gameObject.AddComponent<Outline>();
            gripEdge.effectColor = KnobHandleEdge;
            gripEdge.effectDistance = new Vector2(0.8f, -0.8f);
            leverHandle.gameObject.AddComponent<LeverHold>().Owner = this;
            AddTooltip(leverHandle.gameObject, () => Strings.TipLever);
            leverTopY = y - 9f;
            leverSlotX = slotX;
            y -= LeverSlotH + 8f;

            // ready lamp, a little below the slot; hover it for the status and what it means
            y -= 8f;
            float lampY = y - 9f;
            leverLamp = MakeLamp(leverPanel, new Vector2(0f, 1f), new Vector2(DialPad + LeverW * 0.5f, lampY), 12f, LampGreen);
            var lampHover = NewRect("LampHover", leverPanel);
            PlaceV(lampHover, DialPad + LeverW * 0.5f - 14f, lampY + 14f, 28f, 28f);
            var hoverImg = lampHover.gameObject.AddComponent<Image>();
            hoverImg.color = Color.clear;   // invisible, only catches the mouse for the pop-up
            AddTooltip(lampHover.gameObject, LeverLampTip);
            y -= 22f;

            leverPanel.sizeDelta = new Vector2(LeverW + DialPad * 2f, -y + DialPad);
            AddResizeGrip(leverPanel, ResizableWindow.Lever);
            leverSound = null;          // looked up again for this boat
            leverSoundPos = LeverPos.Off;
            leverPos = LeverPos.Off;
            leverShown = 0f;
            leverHeld = false;
            leverSpringAt = float.NaN;
        }

        float LeverY(float pos) { return leverTopY - (LeverSlotH - 18f) * pos; }

        // ------------------------------------------------------------------ state

        PlayerShip Ship() { try { return Get<PlayerShip>(); } catch { return null; } }

        /// <summary>
        /// The checks of the crew's "use launch lever" action, plus the
        /// receiver warning of its tooltip. Null = ready (ready = tube list).
        /// </summary>
        string LeverProblem(PlayerShip ship, out string ready)
        {
            bool blocking;
            return LeverProblem(ship, out ready, out blocking);
        }

        /// <summary>
        /// Every red-lamp reason holds the lever (the system is "safe"): no tube, an
        /// unflooded tube, the receiver on the wrong end, the target beyond the
        /// torpedo's run, or the gyro angle past its stop. <paramref name="blocking"/>
        /// is kept for callers; it is true whenever a problem is returned.
        /// </summary>
        string LeverProblem(PlayerShip ship, out string ready, out bool blocking, bool evenWhileLaunching = false)
        {
            ready = null;
            blocking = true;
            if (ship == null) return Strings.LeverNoBoat;
            if (!LeverEnergized()) return Strings.LeverNoPower;
            if (ship.IsPreparingToLaunchTorpedoes && !evenWhileLaunching) return null;
            var tubes = ship.TorpedoLaunchers;
            if (tubes == null) return Strings.LeverNothing;
            var boxes = Boxes();
            var ok = new StringBuilder();
            var dry = new StringBuilder();
            string wrongEnd = null;
            bool sternTubes = false;
            for (int i = 0; i < tubes.Length; i++)
            {
                var t = tubes[i];
                if (t == null || !t.IsMarkedForLaunch) continue;
                string n = i + 1 < Roman.Length ? Roman[i + 1] : (i + 1).ToString();
                var sb = t.IsFlooded ? ok : dry;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(n);

                var group = t.Group;
                bool stern = group != null && ReceiverOverride.IsStern(boxes, group);
                if (stern) sternTubes = true;

                // the receiver must feed this tube's end, unless its gyros are set by hand
                if (boxes != null && group != null && wrongEnd == null && (int)group.AngleSettingMode == 0)
                {
                    var need = stern ? GyroAngleReceiverSetting.Stern : GyroAngleReceiverSetting.Bow;
                    if (boxes.GyroAngleReceiver != need) wrongEnd = stern ? Strings.Stern : Strings.Bow;
                }
            }
            if (dry.Length > 0) return string.Format(Strings.LeverNotFlooded, dry);
            if (ok.Length == 0) return Strings.LeverNothing;
            ready = ok.ToString();
            if (wrongEnd != null) return string.Format(Strings.LeverWrongReceiver, wrongEnd);

            // target beyond the torpedo's run: the TDC's "max distance to target" drum
            // (MaxDistanceToTargetRatio x the torpedo range set on its range knob)
            var tdc = Tdc();
            if (tdc != null && tdc.TorpedoRange > 0)
            {
                float max = tdc.MaxDistanceToTargetRatio * tdc.TorpedoRange;
                if (max > 0f && tdc.DistanceToTarget > max)
                    return string.Format(Strings.LeverOutOfRange, Mathf.RoundToInt(max));
            }

            // gyro angle past the gyro setting gear's mechanical stop, measured from the
            // tube's own axis: 0° for bow tubes, 180° (straight astern) for stern tubes
            if (tdc != null)
            {
                float gyro = Mathf.DeltaAngle(sternTubes ? 180f : 0f, tdc.GyroAngle);
                if (Mathf.Abs(gyro) > MaxGyroAngle)
                    return string.Format(Strings.LeverGyroStop, Mathf.RoundToInt(Mathf.Abs(gyro)), (int)MaxGyroAngle);
            }
            blocking = false;
            return null;
        }

        const float MaxGyroAngle = 135f; // the gyro setting gear stops at ±135° from the tube axis

        /// <summary>
        /// The firing station switch (T.Abf. Schalter) powers the lever: on Brücke or
        /// Turm it is live, on either Aus it is dead. (On the boat only the station the
        /// switch names is live: the bridge for the UZO, the tower for the attack
        /// periscope; for now either one powers this lever.) No switch box: live.
        /// </summary>
        static bool LeverEnergized()
        {
            var boxes = Boxes();
            if (boxes == null) return true;
            var s = boxes.ActiveLaunchLever;
            return s == ActiveLaunchLever.UZO || s == ActiveLaunchLever.ConningTowerInterior;
        }

        void FireFromLever()
        {
            var ship = Ship();
            string ready;
            // red lamp = safe: the lever does nothing (stricter than the boat's own lever,
            // which only refuses while launching or with an unflooded tube)
            if (ship == null || ship.IsPreparingToLaunchTorpedoes) return;
            bool blocking;
            string problem = LeverProblem(ship, out ready, out blocking);
            if (problem != null)
            {
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " launch lever held: " + problem);
                return;
            }
            try
            {
                // the same launch the boat's lever performs (TorpedoLaunchLever.Launch), without
                // pressing that 3D lever: it springs back only from its own update while visible,
                // so pressing it from the scope could leave it stuck at Schuß (found fully down later)
                var s = ship.TorpedoAttackSettings;
                ship.LaunchAllTorpedoes(s.TorpedoDepth, s.TorpedoSpeed, 100f, 0.3f);
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " launch lever: launch");
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " launch lever failed: " + e.Message);
            }
        }

        /// <summary>Pull to Schuß: fires once; held down while the button is, then springs back to Aus.</summary>
        void SetLever(LeverPos pos)
        {
            if (pos == LeverPos.Fire)
            {
                if (leverPos == LeverPos.Fire) return;
                leverPos = LeverPos.Fire;
                // a quick click still shows the throw: stays down at least this long
                leverSpringAt = Time.unscaledTime + LeverSpring;
                FireFromLever();
                return;
            }
            // let go: spring back now, or as soon as the minimum throw time is over
            if (float.IsNaN(leverSpringAt) || Time.unscaledTime >= leverSpringAt) leverPos = LeverPos.Off;
        }

        void UpdateLever()
        {
            if (leverPanel == null) return;
            if (!float.IsNaN(leverSpringAt) && Time.unscaledTime >= leverSpringAt && !leverHeld)
            {
                leverSpringAt = float.NaN;
                leverPos = LeverPos.Off; // spring back once the button is up
            }
            float target = leverPos == LeverPos.Fire ? 1f : 0f;
            leverShown = Mathf.MoveTowards(leverShown, target, Time.unscaledDeltaTime * 6f);
            leverHandle.anchoredPosition = new Vector2(leverSlotX, LeverY(leverShown));

            // the firing station knob's click when the lever is thrown and when it springs back
            if (leverPos != leverSoundPos)
            {
                leverSoundPos = leverPos;
                if (leverSound == null) leverSound = KnobSound(SceneKnob<TorpedoFireControlBoxesActiveLaunchLeverKnob>(Boxes())) ?? "";
                PlayGameSound(leverSound);
            }

            var ship = Ship();
            string ready;
            string problem = LeverProblem(ship, out ready);
            if (!LeverEnergized()) { leverLamp.Set(false, LampGreen); leverState = Strings.LeverNoPower; } // dead: lamp dark
            else if (ship != null && ship.IsPreparingToLaunchTorpedoes) { leverLamp.Set(true, LampAmber); leverState = Strings.LeverFiring; }
            else if (problem != null) { leverLamp.Set(true, LampRed); leverState = problem; }
            else { leverLamp.Set(true, LampGreen); leverState = string.Format(Strings.LeverReady, ready); }
        }

        /// <summary>The lamp's pop-up: what it shows right now, then what each colour means.</summary>
        string LeverLampTip()
        {
            return "<b>" + Strings.LeverLampTitle + "</b>\n" + (leverState ?? "") + "\n\n" + Strings.LeverLampLegend;
        }

        void PositionLever()
        {
            if (leverPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.LeverX, Screen.height * GyroConfig.LeverY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                leverPanel.anchoredPosition = local + new Vector2(GyroConfig.LeverOffsetX, GyroConfig.LeverOffsetY);
        }

        /// <summary>
        /// Press on the lever (grip, slot or plates) to pull it to Schuß: it fires
        /// once and stays down while the button is held; let go and it springs
        /// back to Aus. Ctrl+drag moves the window instead.
        /// </summary>
        sealed class LeverHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public GyroCounterUI Owner;
            DragRelay windowDrag;
            bool movingWindow, holding;

            DragRelay WindowDrag
            {
                get
                {
                    if (windowDrag == null && transform.parent != null) windowDrag = transform.parent.GetComponentInParent<DragRelay>();
                    return windowDrag;
                }
            }

            public void OnPointerDown(PointerEventData e)
            {
                if (e.button != PointerEventData.InputButton.Left || CtrlKey) return;
                Owner.HideTip();
                holding = true;
                Owner.leverHeld = true;
                Owner.SetLever(LeverPos.Fire);
            }

            public void OnPointerUp(PointerEventData e)
            {
                if (!holding) return;
                holding = false;
                Owner.leverHeld = false;
                Owner.SetLever(LeverPos.Off);
            }

            void OnDisable()
            {
                if (!holding || Owner == null) return;
                holding = false;
                Owner.leverHeld = false;
                Owner.SetLever(LeverPos.Off);
            }

            // dragging only matters for moving the window with Ctrl
            public void OnBeginDrag(PointerEventData e)
            {
                movingWindow = !holding && CtrlKey && WindowDrag != null;
                if (movingWindow) WindowDrag.OnBeginDrag(e);
            }

            public void OnDrag(PointerEventData e) { if (movingWindow) WindowDrag.OnDrag(e); }

            public void OnEndDrag(PointerEventData e)
            {
                if (movingWindow) WindowDrag.OnEndDrag(e);
                movingWindow = false;
            }
        }
    }
}
