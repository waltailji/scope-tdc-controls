using System;
using System.Collections.Generic;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Direction selector window: the two rotary switches of the boat's box,
    /// Zielrichtung (bearing transmitter: which scope sends the target bearing to
    /// the TDC) and Schußrichtung (gyro angle receiver: which tube group gets the
    /// TDC's gyro angle). They replace the bearing source and receiver buttons
    /// that used to be on the control panel and go through the same code
    /// (TransmitterOverride / ReceiverOverride, so a pick here takes priority as
    /// before). Positions the boat doesn't have (a missing scope, no stern
    /// receiver) are left off the dial.
    /// </summary>
    public partial class GyroCounterUI
    {
        RectTransform directionsPanel;
        TorpedoFireControlBoxes directionsBoxes;
        float directionsRetryAt;
        readonly List<KnobSpec> dirKnobs = new List<KnobSpec>();

        void BuildDirections(TMP_FontAsset font)
        {
            dirKnobs.Clear();
            directionsRetryAt = Time.unscaledTime + SwitchRetry;
            var boxes = Boxes();
            if (boxes == null) return; // LateUpdate retries
            ResolveFields();
            directionsBoxes = boxes;

            // Zielrichtung: off at the bottom, UZO at the top, attack periscope (Zieloptik) on the left,
            // observation periscope on the right
            var tv = new List<int>();
            var tr = new List<float>();
            var tl = new List<string>();
            var tn = new List<string>();
            Action<BearingTransmitter, float, string, string> addTx = (v, rot, label, name) =>
            {
                tv.Add((int)v); tr.Add(rot); tl.Add(label); tn.Add(name);
            };
            // only the positions the boat's own knob has (the Type II's box has no UZO position,
            // though the boat has a UZO); without the knob, the scopes the boat has
            var txKnob = SceneKnob<TorpedoFireControlBoxesBearingTransmitterKnob>(boxes);
            int[] txPresets = KnobValues(txKnob);
            Func<BearingTransmitter, HarmonyLib.AccessTools.FieldRef<TorpedoFireControlBoxes, Periscope>, bool> offered = (v, scope) =>
                txPresets != null ? Array.IndexOf(txPresets, (int)v) >= 0 : HasScope(boxes, scope);
            // Type II box: off at the top, attack periscope on the left, observation periscope on the right
            bool typeII = IsTypeII(boxes);
            addTx(BearingTransmitter.None, typeII ? 0f : 180f, Strings.SwitchOff, Strings.SwitchOff);
            if (offered(BearingTransmitter.AttackPeriscope, attackScopeField)) addTx(BearingTransmitter.AttackPeriscope, 270f, Strings.DirAttackShort, Strings.DirAttack);
            if (offered(BearingTransmitter.UZO, uzoField)) addTx(BearingTransmitter.UZO, typeII ? 180f : 0f, "UZO", "UZO");
            if (offered(BearingTransmitter.ObservationPeriscope, obsScopeField)) addTx(BearingTransmitter.ObservationPeriscope, 90f, Strings.DirObsShort, Strings.DirObs);
            if (GyroConfig.DebugLog && txPresets != null)
                Debug.Log(ScopeTdcControlsMod.Tag + " bearing transmitter knob positions: " + string.Join(", ", Array.ConvertAll(txPresets, p => ((BearingTransmitter)p).ToString())));
            var tx = new KnobSpec
            {
                Values = tv.ToArray(), Rots = tr.ToArray(), Labels = tl.ToArray(), Names = tn.ToArray(),
                Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesBearingTransmitterKnob>(boxes)),
                Get = b => (int)b.BearingTransmitter,
                Set = (b, v) => SetTransmitter((BearingTransmitter)v)
            };

            // Schußrichtung: off at the top and bottom (as on the box), bow tubes on the right,
            // stern tubes on the left
            bool hasBow = bowReceiverField == null || bowReceiverField(boxes) != null;
            bool hasStern = sternReceiverField == null || sternReceiverField(boxes) != null;
            var rv = new List<int>();
            var rr = new List<float>();
            var rl = new List<string>();
            var rn = new List<string>();
            Action<GyroAngleReceiverSetting, float, string, string> addRx = (v, rot, label, name) =>
            {
                rv.Add((int)v); rr.Add(rot); rl.Add(label); rn.Add(name);
            };
            if (typeII)
            {
                // Type II box: off at the top, bow tubes at the bottom
                addRx(GyroAngleReceiverSetting.Off, 0f, Strings.SwitchOff, Strings.SwitchOff);
                if (hasBow) addRx(GyroAngleReceiverSetting.Bow, 180f, Strings.DirBowShort, Strings.DirBow);
                if (hasStern) addRx(GyroAngleReceiverSetting.Stern, 90f, Strings.DirSternShort, Strings.DirStern);
            }
            else
            {
                addRx(GyroAngleReceiverSetting.Off, 0f, Strings.SwitchOff, Strings.SwitchOff);
                if (hasStern) addRx(GyroAngleReceiverSetting.Stern, 270f, Strings.DirSternShort, Strings.DirStern);
                addRx(GyroAngleReceiverSetting.Off2, 180f, Strings.SwitchOff, Strings.SwitchOff);
                if (hasBow) addRx(GyroAngleReceiverSetting.Bow, 90f, Strings.DirBowShort, Strings.DirBow);
            }
            var rx = new KnobSpec
            {
                Values = rv.ToArray(), Rots = rr.ToArray(), Labels = rl.ToArray(), Names = rn.ToArray(),
                Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesGyroAngleReceiverKnob>(boxes)),
                Get = b => (int)b.GyroAngleReceiver,
                Set = (b, v) => SetReceiver((GyroAngleReceiverSetting)v)
            };

            directionsPanel = NewRect("TdcDirections", transform);
            directionsPanel.anchorMin = directionsPanel.anchorMax = directionsPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(directionsPanel);
            directionsPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(directionsPanel.gameObject, false).Directions = true;

            float x = DialPad + KnobRim;
            AddKnob(font, directionsPanel, dirKnobs, x, Strings.DirTarget, () => Strings.TipDirTarget, tx);
            x += KnobSize + KnobGap;
            AddKnob(font, directionsPanel, dirKnobs, x, Strings.DirShot, () => Strings.TipDirShot, rx);
            x += KnobSize + KnobGap;

            float h = DialPad + ControlColumnH + DialPad;
            directionsPanel.sizeDelta = new Vector2(x - KnobGap + KnobRim + DialPad, h);
            AddResizeGrip(directionsPanel, ResizableWindow.Directions);
            RaiseOverlays(); // panel and pop-ups stay on top
        }

        void UpdateDirections()
        {
            if (directionsPanel == null) return;
            UpdateKnobs(dirKnobs, directionsBoxes);
        }

        void PositionDirections()
        {
            if (directionsPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.DirectionsX, Screen.height * GyroConfig.DirectionsY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                directionsPanel.anchoredPosition = local + new Vector2(GyroConfig.DirectionsOffsetX, GyroConfig.DirectionsOffsetY);
        }

        /// <summary>Boat changed, or the box wasn't there when the scope opened: (re)build.</summary>
        void ValidateDirections()
        {
            if (!GyroConfig.ShowDirections) return;
            var boxes = Boxes();
            if (directionsPanel != null && ReferenceEquals(boxes, directionsBoxes)) return;
            if (directionsPanel == null && Time.unscaledTime < directionsRetryAt) return;
            if (directionsPanel != null) { Destroy(directionsPanel.gameObject); directionsPanel = null; }
            directionsBoxes = null;
            BuildDirections(ResolveFont());
        }
    }
}
