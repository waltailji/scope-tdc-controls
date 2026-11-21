using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ScopeTdcControls
{
    public enum LanguageMode { Auto, German, English }

    /// <summary>
    /// Simple key=value config stored next to the other mod configs in
    /// %USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\scope-tdc-controls-config.txt
    /// It is re-read every time you enter the periscope. Settings missing from
    /// the file (e.g. added in a later version) are appended with their defaults.
    /// </summary>
    public static class GyroConfig
    {
        const string FileName = "scope-tdc-controls-config.txt";
        const string PreReleaseFileName = "gyro-angle-display-config.txt"; // name used while testing, before release

        public static bool Enabled = true;

        // gyro counter
        public static bool AnchorToCompass = true;   // Anchor=Compass | Screen
        public static bool Above = false;            // Position=Below | Above
        public static float OffsetX = 0f;
        public static float OffsetY = -28f;
        public static float Gap = 4f;
        public static float FallbackX = 0.5f;
        public static float FallbackY = 0.80f;
        public static float Scale = 1f;
        public static bool LeftRight = false;        // Format=360 | LR
        public static bool ShowTenths = false;
        public static float RollSpeed = 90f;         // deg/sec, 0 = instant
        public static bool ShowSyncLamp = true;
        public static bool ShowReceiverAngle = true;
        public static bool ActiveReceiverOnly = true;
        public static bool ShowParallax = true;
        public static bool AutoParallax = true;
        public static bool AutoReceiver = true;

        // realism (settings drop-down)
        public static bool RealisticTdc = true;           // false = TDC motors run 10x faster
        public static bool RealisticReceiverSync = true;  // false = tube gyro motors run 10x faster
        public static bool SingleTorpedoLaunchFix = true; // fire only the tubes selected when fire was pressed
        public static bool TdcOperatorRequired = true;    // false = TDC works without a crew member at it

        // controls panel
        public static bool ShowControls = true;
        public static bool ControlsAtElevator = true; // ControlsAnchor=Elevator | Screen
        public static float ControlsOffsetX = 0f;
        public static float ControlsOffsetY = 0f;
        public static float ControlsGap = 10f;
        public static float ControlsX = 0.92f;
        public static float ControlsY = 0.5f;

        // general
        public static LanguageMode Language = LanguageMode.Auto;
        public static bool Tooltips = true;
        public static bool HideWithGameUI = false;
        public static int SortingOrder = 500;
        public static KeyCode ToggleKey = KeyCode.None;
        public static bool DebugLog = false;

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        const string DefaultFile =
@"# Scope TDC Controls - settings (re-read each time you enter the periscope)
# Tip: hold Ctrl and drag the counter or the control panel to move them;
# the new position is saved here automatically.
Enabled=true

# ---- Gyro counter ----
# Compass = follow the bearing tape (position is remembered while the game UI
# is hidden with Alt) ; Screen = fixed spot at FallbackX/FallbackY
Anchor=Compass
# Below or Above the bearing tape
Position=Below
# Extra offset in 1080p pixels (+X right, +Y up). Ctrl+drag updates these.
OffsetX=0
OffsetY=-28
Gap=4
# 0..1 of screen width/height from bottom-left (Anchor=Screen, or before the
# bearing tape has been seen)
FallbackX=0.5
FallbackY=0.80
# Size multiplier for the counter and the control panel
Scale=1.0
# 360 = Schusswinkel 000-359 ; LR = L/R 000-180
Format=360
ShowTenths=false
# Drum follow speed in degrees per second (0 = instant)
RollSpeed=90
# Lamp next to the counter: green = tube gyros set, red = synchronizing, dark = receiver off
ShowSyncLamp=true
# Rows under the TDC counter with the tube groups' gyro angles
ShowReceiverAngle=true
# true = only the selected receiver (none while off) ; false = Bow and Stern always
ActiveReceiverOnly=true
# Row with the TDC parallax correction and Forward/Backward buttons
ShowParallax=true

# ---- TDC options (also in the settings drop-down on the control panel) ----
# true = parallax direction follows the receiver (Bug = forward, Heck = backward);
#        with the receiver off, the TDC's target bearing
#        (ahead of the beam = forward, abaft the beam = backward)
# false = set it yourself with the Forward/Backward buttons
AutoParallax=true
# true = receiver follows the checked flooded tubes (none = off; bow and stern
#        both checked = the end the scope looks toward while follow-up is on)
# false = only you change the receiver; the torpedo panel leaves it alone
AutoReceiver=true
# false = the TDC's motors run 10x faster
RealisticTdc=true
# false = the selected tubes' gyro setting motors run 10x faster
RealisticReceiverSync=true
# true = a fire command fires only the tubes selected when it was given;
#        tubes checked afterwards wait for the next fire command
SingleTorpedoLaunchFix=true
# true = the TDC only works with a crew member at it (base game)
# false = the TDC calculates and can be opened from the station bar unmanned
TdcOperatorRequired=true

# ---- Follow-up / gyro receiver panel ----
ShowControls=true
# Elevator = beside the periscope raise/lower control ; Screen = ControlsX/ControlsY
ControlsAnchor=Elevator
# Extra offset in 1080p pixels. Ctrl+drag updates these.
ControlsOffsetX=0
ControlsOffsetY=0
ControlsGap=10
ControlsX=0.92
ControlsY=0.5

# ---- General ----
# Auto = follow the game's language ; DE ; EN
Language=Auto
# Pop-up descriptions when hovering an element
Tooltips=true
# Also hide this mod's elements when the game UI is hidden (Alt)
HideWithGameUI=false
# Canvas draw order (raise if hidden behind other UI)
SortingOrder=500
# Optional show/hide hotkey (Unity KeyCode name, e.g. F9). Blank = none
ToggleKey=
# Log every torpedo tube check-box change (and what changed it) to output_log.txt
DebugLog=false
";

        public static void Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                {
                    string old = Path.Combine(Application.persistentDataPath, PreReleaseFileName);
                    if (File.Exists(old))
                    {
                        // carry over settings from the pre-release test build
                        File.Move(old, path);
                        Debug.Log(ScopeTdcControlsMod.Tag + " moved settings from " + PreReleaseFileName + " to " + FileName);
                    }
                    else
                    {
                        File.WriteAllText(path, DefaultFile);
                    }
                }

                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    present.Add(key);
                    Apply(key.ToLowerInvariant(), line.Substring(eq + 1).Trim());
                }

                AppendMissingDefaults(path, present);
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " config read failed, using defaults: " + e.Message);
            }
        }

        /// <summary>
        /// Appends any setting the file doesn't have yet (with its comment lines),
        /// so new options show up in older files without touching the user's values.
        /// </summary>
        static void AppendMissingDefaults(string path, HashSet<string> present)
        {
            var missing = new List<string>();
            var comments = new List<string>();
            foreach (string raw in DefaultFile.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                string t = line.Trim();
                if (t.Length == 0) { comments.Clear(); continue; }
                if (t[0] == '#') { comments.Add(line); continue; }
                int eq = t.IndexOf('=');
                if (eq > 0 && !present.Contains(t.Substring(0, eq).Trim()))
                {
                    missing.AddRange(comments);
                    missing.Add(line);
                }
                comments.Clear();
            }
            if (missing.Count == 0) return;
            try
            {
                File.AppendAllText(path, Environment.NewLine + "# ---- added by Scope TDC Controls ----" + Environment.NewLine
                                         + string.Join(Environment.NewLine, missing.ToArray()) + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not add new settings to the config file: " + e.Message);
            }
        }

        public static void Save(string key, float value)
        {
            SaveText(key, value.ToString("0.#", CultureInfo.InvariantCulture));
        }

        public static void Save(string key, bool value)
        {
            SaveText(key, value ? "true" : "false");
        }

        /// <summary>Writes key=value back into the file, replacing an existing line or appending.</summary>
        static void SaveText(string key, string text)
        {
            try
            {
                string path = FilePath;
                var lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
                bool done = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + text;
                        done = true;
                        break;
                    }
                }
                if (!done) lines.Add(key + "=" + text);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not save " + key + ": " + e.Message);
            }
        }

        static void Apply(string key, string v)
        {
            switch (key)
            {
                case "enabled": Enabled = Bool(v, Enabled); break;
                case "anchor": AnchorToCompass = !v.Equals("screen", StringComparison.OrdinalIgnoreCase); break;
                case "position": Above = v.Equals("above", StringComparison.OrdinalIgnoreCase); break;
                case "offsetx": OffsetX = Num(v, OffsetX); break;
                case "offsety": OffsetY = Num(v, OffsetY); break;
                case "gap": Gap = Num(v, Gap); break;
                case "fallbackx": FallbackX = Mathf.Clamp01(Num(v, FallbackX)); break;
                case "fallbacky": FallbackY = Mathf.Clamp01(Num(v, FallbackY)); break;
                case "scale": Scale = Mathf.Clamp(Num(v, Scale), 0.25f, 5f); break;
                case "format": LeftRight = v.Equals("lr", StringComparison.OrdinalIgnoreCase); break;
                case "showtenths": ShowTenths = Bool(v, ShowTenths); break;
                case "rollspeed": RollSpeed = Mathf.Max(0f, Num(v, RollSpeed)); break;
                case "showsynclamp": ShowSyncLamp = Bool(v, ShowSyncLamp); break;
                case "showreceiverangle": ShowReceiverAngle = Bool(v, ShowReceiverAngle); break;
                case "activereceiveronly": ActiveReceiverOnly = Bool(v, ActiveReceiverOnly); break;
                case "showparallax": ShowParallax = Bool(v, ShowParallax); break;
                case "autoparallax": AutoParallax = Bool(v, AutoParallax); break;
                case "autoreceiver": AutoReceiver = Bool(v, AutoReceiver); break;
                case "realistictdc": RealisticTdc = Bool(v, RealisticTdc); break;
                case "realisticreceiversync": RealisticReceiverSync = Bool(v, RealisticReceiverSync); break;
                case "singletorpedolaunchfix": SingleTorpedoLaunchFix = Bool(v, SingleTorpedoLaunchFix); break;
                case "tdcoperatorrequired": TdcOperatorRequired = Bool(v, TdcOperatorRequired); break;
                case "showcontrols": ShowControls = Bool(v, ShowControls); break;
                case "controlsanchor": ControlsAtElevator = !v.Equals("screen", StringComparison.OrdinalIgnoreCase); break;
                case "controlsoffsetx": ControlsOffsetX = Num(v, ControlsOffsetX); break;
                case "controlsoffsety": ControlsOffsetY = Num(v, ControlsOffsetY); break;
                case "controlsgap": ControlsGap = Num(v, ControlsGap); break;
                case "controlsx": ControlsX = Mathf.Clamp01(Num(v, ControlsX)); break;
                case "controlsy": ControlsY = Mathf.Clamp01(Num(v, ControlsY)); break;
                case "language":
                    Language = v.Equals("de", StringComparison.OrdinalIgnoreCase) ? LanguageMode.German
                             : v.Equals("en", StringComparison.OrdinalIgnoreCase) ? LanguageMode.English
                             : LanguageMode.Auto;
                    break;
                case "tooltips": Tooltips = Bool(v, Tooltips); break;
                case "hidewithgameui": HideWithGameUI = Bool(v, HideWithGameUI); break;
                case "sortingorder": SortingOrder = (int)Num(v, SortingOrder); break;
                case "debuglog": DebugLog = Bool(v, DebugLog); break;
                case "togglekey":
                    KeyCode k;
                    ToggleKey = (v.Length > 0 && Enum.TryParse(v, true, out k)) ? k : KeyCode.None;
                    break;
            }
        }

        static bool Bool(string v, bool def)
        {
            bool b;
            if (bool.TryParse(v, out b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return def;
        }

        static float Num(string v, float def)
        {
            float f;
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : def;
        }
    }
}
