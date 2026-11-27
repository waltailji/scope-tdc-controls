using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Typing a value into a dial's readout (from the Contact Keeper mod): click the black value box
    /// under a dial and a text box opens over it. Enter sets what was typed, if it is a number in the
    /// dial's range; anything else is refused and the dial stays as it was (Enter: the box stays open
    /// with the range shown in red; clicking away: the value box flashes red). Esc cancels. The game's
    /// keys and our hotkey hold off while typing.
    /// </summary>
    public partial class GyroCounterUI
    {
        static readonly Color EntryBack = new Color(0.16f, 0.13f, 0.07f, 1f);
        static readonly Color EntryHintInk = new Color(0.95f, 0.93f, 0.86f, 0.45f);
        static readonly Color EntryErrorInk = new Color(1.00f, 0.47f, 0.42f, 1f);
        static readonly Color EntryErrorBox = new Color(0.45f, 0.08f, 0.06f, 1f);
        const float EntryFlashTime = 1.2f;

        readonly List<TypedEntry> typedEntries = new List<TypedEntry>();

        /// <summary>True while any value box is being typed into: the mod's hotkey holds off.</summary>
        static bool Typing { get { return TypedEntry.Open > 0; } }

        /// <summary>
        /// A text box over <paramref name="spec"/>'s readout. <paramref name="parse"/> turns the text
        /// into a value for the dial's Set, or NaN when it isn't one (not a number, or out of range).
        /// <paramref name="range"/> is the short range shown in red when a value is refused;
        /// <paramref name="hint"/> the longer description in the readout's pop-up.
        /// </summary>
        void AddTypedEntry(DialSpec spec, TMP_FontAsset font, Func<string, float> parse, Func<string> range, Func<string> hint)
        {
            if (spec == null || spec.Readout == null) return;
            var box = spec.Readout.transform.parent as RectTransform;
            if (box == null) return;
            typedEntries.Add(new TypedEntry(this, spec, box, font, parse, range, hint));
        }

        /// <summary>Every frame: hide boxes after an edit, let go of the keys, drop boxes whose window was rebuilt.</summary>
        void TickTypedEntries()
        {
            bool shown = canvas != null && canvas.enabled;
            for (int i = typedEntries.Count - 1; i >= 0; i--)
                if (!typedEntries[i].Tick(shown)) typedEntries.RemoveAt(i);
        }

        /// <summary>A typed value goes to the TDC as if the dial had been clicked there; false if it couldn't be set.</summary>
        bool ApplyTyped(DialSpec spec, float v)
        {
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return false;
            HideTip();
            spec.Set(tdc, v);
            if (spec.Announce != null) spec.AnnounceAt = Time.unscaledTime; // the crew calls it out, as after a click
            return true;
        }

        sealed class TypedEntry
        {
            public static int Open;   // boxes being typed into (or still holding the keys)

            readonly GyroCounterUI owner;
            readonly DialSpec spec;
            readonly Image boxImage;
            readonly Color boxColor;
            readonly GameObject entry;
            readonly TMP_InputField field;
            readonly TextMeshProUGUI placeholder;
            readonly Func<string, float> parse;
            readonly Func<string> range;
            bool editing, close, reopen, holding;
            int releaseFrame = -1;
            float flashUntil;

            public TypedEntry(GyroCounterUI owner, DialSpec spec, RectTransform box, TMP_FontAsset font,
                              Func<string, float> parse, Func<string> range, Func<string> hint)
            {
                this.owner = owner;
                this.spec = spec;
                this.parse = parse;
                this.range = range;
                boxImage = box.GetComponent<Image>();
                if (boxImage != null) { boxImage.raycastTarget = true; boxColor = boxImage.color; }
                box.gameObject.AddComponent<EntryClick>().Owner = this;
                owner.AddTooltip(box.gameObject, () => string.Format(Strings.TipTypeValue, hint()));

                var r = NewRect("Entry", box);
                entry = r.gameObject;
                entry.SetActive(false); // set up before it first wakes
                Stretch(r);
                var back = entry.AddComponent<Image>();
                back.color = EntryBack;
                RoundValueBox(back); // the same small rounded corners as the value box it covers
                var area = NewRect("Area", r);
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(4f, 1f); area.offsetMax = new Vector2(-4f, -1f);
                area.gameObject.AddComponent<RectMask2D>();
                var text = NewText("Text", area, font, "", CtlFont + 2f, DigitLight);
                text.alignment = TextAlignmentOptions.Center;
                Stretch(text.rectTransform);
                placeholder = NewText("Hint", area, font, "", 10f, EntryHintInk);
                placeholder.fontStyle = FontStyles.Italic;
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.enableAutoSizing = true; // the longer ranges shrink to fit
                placeholder.fontSizeMin = 7f;
                placeholder.fontSizeMax = 10f;
                Stretch(placeholder.rectTransform);
                field = entry.AddComponent<TMP_InputField>();
                field.textViewport = area;
                field.textComponent = text;
                field.placeholder = placeholder;
                if (font != null) field.fontAsset = font;
                field.pointSize = CtlFont + 2f;
                field.characterLimit = 12;
                field.lineType = TMP_InputField.LineType.SingleLine;
                field.caretColor = DigitLight;
                field.onEndEdit.AddListener(EndEdit);
            }

            public void Begin()
            {
                if (editing || entry == null) return;
                var tdc = Tdc();
                if (tdc != null && spec.Locked != null && spec.Locked(tdc)) { Flash(); return; } // AOB under the locking mechanism, no tube group
                owner.HideTip();
                Show(false);
            }

            void Show(bool refused)
            {
                editing = true;
                close = reopen = false;
                Hold(true);
                entry.SetActive(true);
                placeholder.text = refused ? range() : Strings.TypeValuePlaceholder;
                placeholder.color = refused ? EntryErrorInk : EntryHintInk;
                placeholder.fontStyle = refused ? FontStyles.Normal : FontStyles.Italic;
                field.text = "";
                field.Select();
                field.ActivateInputField();
            }

            void EndEdit(string text)
            {
                if (!editing) return;
                editing = false;
                close = true; // hidden next frame, not inside its own callback
                if (field.wasCanceled || Input.GetKey(KeyCode.Escape) || text == null || text.Trim().Length == 0) return;
                float v = parse(text);
                if (!float.IsNaN(v) && owner.ApplyTyped(spec, v)) return;
                // refused: Enter keeps the box open with the range in red; clicking away flashes the value box
                if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) reopen = true;
                else Flash();
            }

            void Flash() { flashUntil = Time.unscaledTime + EntryFlashTime; }

            void Hold(bool on)
            {
                if (on == holding) return;
                holding = on;
                Open += on ? 1 : -1;
                HoldGameKeys(this, on);
            }

            /// <summary>Every frame; false once the box is gone (its window was rebuilt).</summary>
            public bool Tick(bool shown)
            {
                if (entry == null) { editing = false; Hold(false); return false; }
                if (close)
                {
                    close = false;
                    if (reopen && shown && entry.activeInHierarchy) Show(true);
                    else { entry.SetActive(false); releaseFrame = Time.frameCount + 1; } // the keys come back next frame, so Enter doesn't reach the game
                }
                else if (editing && (!shown || !entry.activeInHierarchy))
                {
                    // the window went away mid-edit: drop what was typed
                    editing = false;
                    field.DeactivateInputField();
                    entry.SetActive(false);
                    releaseFrame = Time.frameCount;
                }
                if (!editing && holding && releaseFrame >= 0 && Time.frameCount >= releaseFrame) { Hold(false); releaseFrame = -1; }
                if (boxImage != null)
                {
                    bool flash = Time.unscaledTime < flashUntil;
                    var c = flash ? EntryErrorBox : boxColor;
                    if (boxImage.color != c) boxImage.color = c;
                }
                return true;
            }
        }

        sealed class EntryClick : MonoBehaviour, IPointerClickHandler
        {
            public TypedEntry Owner;
            public void OnPointerClick(PointerEventData e)
            {
                if (e.dragging || e.button != PointerEventData.InputButton.Left || CtrlKey || Owner == null) return;
                Owner.Begin();
            }
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>The game's own keys (its GameInput blockade, as other mods' windows use) hold off while typing.</summary>
        static void HoldGameKeys(object key, bool on)
        {
            try
            {
                var input = Get<UBOAT.Game.Core.Input.GameInput>();
                if (input == null) return;
                if (on) input.AddBlockade(key);
                else input.RemoveBlockade(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " typed entry: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ parsing

        /// <summary>
        /// A number from what was typed, with one of <paramref name="units"/> after it if any (its
        /// factor in <paramref name="factor"/>, else 1, or NaN when none was given), "°" and spaces
        /// ignored, a decimal comma taken as a point.
        /// </summary>
        static bool ParseTyped(string s, string[] units, float[] factors, out float v, out float factor)
        {
            v = float.NaN;
            factor = float.NaN;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().ToLowerInvariant().Replace(" ", "").Replace("°", "");
            if (units != null)
                for (int i = 0; i < units.Length; i++)
                    if (s.Length > units[i].Length && s.EndsWith(units[i], StringComparison.Ordinal))
                    {
                        s = s.Substring(0, s.Length - units[i].Length);
                        factor = factors[i];
                        break;
                    }
            s = s.Replace(",", ".");
            if (s.Length == 0 || s.IndexOf('.') != s.LastIndexOf('.')) return false;
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        static readonly string[] NoUnits = new string[0];
        static readonly float[] NoFactors = new float[0];

        /// <summary>A plain number in min..max (with an optional unit), rounded to <paramref name="step"/>; NaN if not.</summary>
        static Func<string, float> TypedNumber(float min, float max, float step, params string[] units)
        {
            var factors = new float[units.Length];
            for (int i = 0; i < factors.Length; i++) factors[i] = 1f;
            return s =>
            {
                float v, f;
                if (!ParseTyped(s, units, factors, out v, out f) || v < min || v > max) return float.NaN;
                return step > 0f ? Mathf.Clamp(Mathf.Round(v / step) * step, min, max) : v;
            };
        }

        /// <summary>AOB: -180 to 180, starboard positive, port negative.</summary>
        static float ParseTypedAob(string s)
        {
            float v, f;
            if (!ParseTyped(s, NoUnits, NoFactors, out v, out f) || v < -180f || v > 180f) return float.NaN;
            return Mathf.Round(v * 10f) / 10f;
        }

        /// <summary>A compass-style angle (gyro angle, target bearing): 0 to 360, 360 being 000.</summary>
        static float ParseTypedAngle(string s)
        {
            float v, f;
            if (!ParseTyped(s, NoUnits, NoFactors, out v, out f) || v < 0f || v > 360f) return float.NaN;
            return Mathf.Repeat(Mathf.Round(v * 10f) / 10f, 360f);
        }

        /// <summary>
        /// Target distance: "4500", "4500 m", "45 hm", "4.5 km"; a bare number under 300 is hm (the
        /// dial's unit); "4,500" / "4.500" without km is 4500 m. 300 to 10000 m (the dial's 3-100 hm).
        /// </summary>
        static float ParseTypedDistance(string s)
        {
            if (string.IsNullOrEmpty(s)) return float.NaN;
            string t = s.Trim().ToLowerInvariant().Replace(" ", "");
            bool km = t.EndsWith("km", StringComparison.Ordinal);
            bool hm = t.EndsWith("hm", StringComparison.Ordinal);
            string digits = km || hm ? t.Substring(0, t.Length - 2) : t.EndsWith("m", StringComparison.Ordinal) ? t.Substring(0, t.Length - 1) : t;
            // a thousands separator: one or two digits, then a separator and exactly three digits
            if (!km && !hm && digits.Length >= 5 && digits.Length <= 6)
            {
                int sep = digits.Length - 4;
                if ((digits[sep] == ',' || digits[sep] == '.') && IsDigits(digits, 0, sep) && IsDigits(digits, sep + 1, 3))
                    t = digits.Remove(sep, 1) + "m";
            }
            float v, unit;
            if (!ParseTyped(t, new[] { "km", "hm", "m" }, new[] { 1000f, 100f, 1f }, out v, out unit) || v <= 0f) return float.NaN;
            if (float.IsNaN(unit)) unit = v < 300f ? 100f : 1f;
            float m = Mathf.Round(v * unit / 10f) * 10f;
            return m < 300f || m > 10000f ? float.NaN : m;
        }

        static bool IsDigits(string s, int from, int count)
        {
            for (int i = from; i < from + count; i++) if (i >= s.Length || !char.IsDigit(s[i])) return false;
            return true;
        }
    }
}
