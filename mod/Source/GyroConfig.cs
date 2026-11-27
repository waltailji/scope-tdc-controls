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
        // last seen bearing-tape anchor point, as a fraction of the screen (NaN = not seen yet)
        public static float TapeAnchorX = float.NaN;
        public static float TapeAnchorY = float.NaN;
        public static float Scale = 1f;
        public static bool LeftRight = false;        // Format=360 | LR
        public static bool ShowTenths = false;
        public static float RollSpeed = 90f;         // deg/sec, 0 = instant
        public static float ImpactTimeWeight = -0.3f; // the time-to-impact digits' stroke weight: -1 (thin) .. 1 (bold), 0 = as the font is
        public static string ImpactTimeFont = "ROBOTO-BLACK"; // the time-to-impact drums' font (part of a loaded game font's name; empty = the panel font)
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
        public static bool AutoSelectFloodedTube = false; // true = the game checks a tube when it floods (base game)
        public static bool TorpedoTimers = true;          // a countdown per torpedo launched, near the top of the screen
        public static float TimersX = 0.5f, TimersY = 0.9f; // top centre of the timer stack, 0..1 of the screen from bottom-left
        public static bool EnhancedTubeHandling = true;   // tube outer doors by hand, fire only when open, tube stays flooded after a shot
        public static bool TdcOperatorRequired = true;    // false = TDC works without a crew member at it
        public static bool CtrlDragToMove = false;        // false = plain drag on a window background moves it (default for new installs)
        public const float WindowScaleMin = 0.6f, WindowScaleMax = 2.0f;
        public static float DialsScale = 1f, SwitchesScale = 1f, DirectionsScale = 1f, LeverScale = 1f; // per-window size (corner grip)
        public const float TorpedoScaleDefault = 0.8f;   // smaller than the TDC dials
        public static float TorpedoScale = TorpedoScaleDefault;
        public static bool ShowTorpedo = true;            // torpedo settings window (shown while a checked tube holds a torpedo)
        public static float TorpedoX = 0.62f, TorpedoY = 0.11f; // bottom, right of centre
        public static float TorpedoOffsetX = 0f, TorpedoOffsetY = 0f;
        public static bool ShowSpread = false;            // salvo spread window (target length + spread angle) when not shown automatically
        public static bool AutoSpread = true;             // show the spread window only while Einzelschuß is on a spread position
        public static bool ShowAngularSpeed = false;      // own angular speed knob in its own window beside the TDC dials
        public static bool TintPanels = true;             // the off-white windows take on the boat's interior light colour
        public static bool KeepScopeBearing = true;       // a target change or a ship click away from the scope doesn't turn the scope
        public static float SpreadScale = 1f;
        public static float SpreadOffsetX = 0f, SpreadOffsetY = 0f; // from its place right of the TDC dials
        public static float TurnRateScale = 1f;
        public static float TurnRateOffsetX = 0f, TurnRateOffsetY = 0f; // from its place right of the TDC dials, bottom level with theirs
        // anchoring (the anchor icon on the window): anchored windows follow another window,
        // using their *Offset; free windows use their *Free position (canvas units)
        public static bool SpreadAnchored = true, TurnRateAnchored = true, SwitchesAnchored = false;
        public static float SpreadFreeX = 0f, SpreadFreeY = 0f, TurnRateFreeX = 0f, TurnRateFreeY = 0f;
        public static float SwitchesAnchorOffsetX = 0f, SwitchesAnchorOffsetY = 0f; // from its place left of the direction selector
        public static bool ShowTubes = true;              // SH3-style torpedo tube indicator
        public static float TubesScale = 1f;
        public static float TubesX = 0.5f, TubesY = 0.24f; // above the bottom windows, centred
        public static float TubesOffsetX = 0f, TubesOffsetY = 0f;
        public static bool ShowTrueBearing = true;        // TRUE bearing counter (the scope's compass bearing), above the TDC counter
        public static float TrueOffsetX = 0f, TrueOffsetY = 0f; // from its place above the TDC counter
        public static bool TrueBearingOnBinoculars = false; // the TRUE bearing counter also on the bridge binoculars (where the rest is hidden)
        public static float TrueBinoOffsetX = 0f, TrueBinoOffsetY = 0f; // binoculars: from the gyro angle counter's place
        public static bool ShowGyroSet = false;           // bow torpedo room gyro setting panel
        public const float GyroSetScaleDefault = 0.8f;    // a large window: smaller by default
        public static float GyroSetScale = GyroSetScaleDefault;
        public static float GyroSetX = 0.62f, GyroSetY = 0.55f;
        public static float GyroSetOffsetX = 0f, GyroSetOffsetY = 0f;
        public static bool ShowSternGyroSet = false;      // stern torpedo room gyro setting panel (coarse / fine dials, hand crank)
        public static float SternSetScale = GyroSetScaleDefault;
        public static float SternSetX = 0.42f, SternSetY = 0.40f;
        public static float SternSetOffsetX = 0f, SternSetOffsetY = 0f;
        public static bool ShowBearing = false;           // TDC target bearing window (Zielrichtung: fine and coarse dials)
        public static float BearingScale = 1f;
        public static float BearingX = 0.30f, BearingY = 0.62f; // right of the TDC dials and their side windows
        public static float BearingOffsetX = 0f, BearingOffsetY = 0f;
        public static bool VoiceCallouts = true;          // dial window: the officer at the scope announces the values

        // controls panel
        public static bool ShowControls = true;
        public static bool ControlsVisible = true;    // shown/hidden with the sidebar button
        public static bool PanelCollapsed = false;    // the panel rolled up to its title bar (caret)
        public static bool ElevatorButtons = true;    // raise fully / lower fully buttons
        public static bool HideStockElevator = false; // hide the game's periscope raise/lower control (and the buttons above)
        public static bool ControlsAtElevator = true; // ControlsAnchor=Elevator | Screen
        public static float ControlsOffsetX = 0f;
        public static float ControlsOffsetY = 0f;
        public static float ControlsGap = 10f;
        public static float ControlsX = 0.92f;
        public static float ControlsY = 0.5f;

        // TDC dial window (AOB / target speed / distance), shown with the control panel
        public static bool ShowGyros = true;  // the gyro angle counters under the bearing tape
        public static bool ShowDials = true;
        public static float DialsX = 0.12f;
        public static float DialsY = 0.6f;   // above the switch box
        public static float DialsOffsetX = 0f;
        public static float DialsOffsetY = 0f;

        // fire-control switch box window (single shot / salvo / launch station), shown with the control panel
        public static bool ShowSwitches = true;
        public static float SwitchesX = 0.12f;  // bottom left, under the TDC dials
        public static float SwitchesY = 0.11f;
        public static float SwitchesOffsetX = 0f;
        public static float SwitchesOffsetY = 0f;

        // launch lever window (Aus / Gesichert / Schuß), shown with the control panel
        public static bool ShowLever = true;
        public static float LeverX = 0.80f;
        public static float LeverY = 0.17f;
        public static float LeverOffsetX = 0f;
        public static float LeverOffsetY = 0f;

        // direction selector window (bearing transmitter + gyro receiver knobs), shown with the control panel
        public static bool ShowDirections = true;
        public static float DirectionsX = 0.285f;  // bottom, right of the firing selector
        public static float DirectionsY = 0.11f;
        public static float DirectionsOffsetX = 0f;
        public static float DirectionsOffsetY = 0f;


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
# Compass = placed under the bearing tape the first time it is seen, then kept
# there (Ctrl+drag to move) ; Screen = fixed spot at FallbackX/FallbackY
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
# Where the bearing tape was first seen (set automatically): the counters'
# fixed spot. Clear both to place them under the tape again (e.g. after
# changing Position)
TapeAnchorX=
TapeAnchorY=
# Size multiplier for the counter and the control panel
Scale=1.0
# 360 = Schusswinkel 000-359 ; LR = L/R 000-180
Format=360
ShowTenths=false
# Drum follow speed in degrees per second (0 = instant)
RollSpeed=90
# Font of the time-to-impact drums: part of the name of one of the game's fonts
# (ROBOTO-BLACK, berylium bd, berylium rg, FrankRuhlLibre-Bold, Vollkorn-Regular, LiberationSans; empty = the panel font)
ImpactTimeFont=ROBOTO-BLACK
# Stroke weight of the time-to-impact digits: 0 = as the font is, up to 1 bolder, down to -1 thinner (default -0.3, a little lighter than the font)
ImpactTimeWeight=-0.3
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
# true = a tube is checked (selected for firing) when it starts flooding, and the
#        firing selector switches follow (base game)
# false = flooding leaves the tube selection and the switches alone
AutoSelectFloodedTube=false
# true = the tubes' outer doors are worked by hand (right-click a flooded tube): a tube
#        fires only with its door fully open, stays flooded and empty after the shot,
#        and is drained (door shut) before the crew reloads it
# false = no outer doors (base game)
EnhancedTubeHandling=true
# true = each torpedo launched gets a run timer near the top of the screen, counting down
#        from the time-to-impact counter (drag the timers to move them)
TorpedoTimers=true
# Top centre of the torpedo timers, 0..1 of screen width/height from bottom-left
TimersX=0.5
TimersY=0.9
# true = the TDC only works with a crew member at it (base game)
# false = the TDC calculates and can be opened from the station bar unmanned
TdcOperatorRequired=true
# true = the officer at the scope announces values set with the TDC dial window,
#        with the game's own voice lines (AOB is set once he has said it)
# true = hold Ctrl and drag to move the mod's windows ; false = drag a window's background to move it
CtrlDragToMove=false
# Window sizes (drag the corner grip; the panel button Reset window sizes sets them back to 1)
DialsScale=1
SwitchesScale=1
DirectionsScale=1
LeverScale=1
TorpedoScale=0.8
# ---- Torpedo settings window (depth, speed, pistol; shown while a checked tube holds a torpedo) ----
ShowTorpedo=true
TorpedoX=0.62
TorpedoY=0.11
TorpedoOffsetX=0
TorpedoOffsetY=0
# ---- Salvo spread window (target length, spread angle; right of the TDC dials) ----
# ShowSpread=true: always shown; false: shown only while the Einzelschuss switch is on
# a spread position, and only with AutoSpread=true
AutoSpread=true
# true = an own angular speed knob (-3..3 deg/s) in its own window beside the TDC dials
ShowAngularSpeed=false
# true = the off-white windows take on the colour of the boat's interior lights (red / blue)
TintPanels=true
# true = a periscope keeps the direction you left it in when its target changes, and clicking
#        a ship or contact away from the scope doesn't snap it round (at the scope it still does)
KeepScopeBearing=true
ShowSpread=false
SpreadScale=1
SpreadOffsetX=0
SpreadOffsetY=0
# ---- Own angular speed window (right of the TDC dials, bottom level with them) ----
TurnRateScale=1
TurnRateOffsetX=0
TurnRateOffsetY=0
# ---- Anchoring (the anchor icon at the top right of these windows) ----
# true = the salvo spread / angular speed windows follow the TDC dials, the firing
# selector follows the direction selector (sitting left of it)
SpreadAnchored=true
TurnRateAnchored=true
SwitchesAnchored=false
SpreadFreeX=0
SpreadFreeY=0
TurnRateFreeX=0
TurnRateFreeY=0
SwitchesAnchorOffsetX=0
SwitchesAnchorOffsetY=0
# ---- Torpedo tube indicator (one circle per tube: bow row, stern row) ----
ShowTubes=true
TubesScale=1
TubesX=0.5
TubesY=0.24
TubesOffsetX=0
TubesOffsetY=0
# ---- TRUE bearing counter (where the scope looks, as a compass bearing), above the TDC counter ----
ShowTrueBearing=true
TrueOffsetX=0
TrueOffsetY=0
# true = the TRUE bearing counter also shows on the bridge binoculars (every other window is hidden there)
TrueBearingOnBinoculars=false
TrueBinoOffsetX=0
TrueBinoOffsetY=0
# ---- Bow torpedo room gyro setting panel (gyro / spread flags, hand wheel, switches, fast sync lever) ----
ShowGyroSet=false
GyroSetScale=0.8
GyroSetX=0.62
GyroSetY=0.55
GyroSetOffsetX=0
GyroSetOffsetY=0
# ---- Stern torpedo room gyro setting panel (coarse / fine gyro dials, hand crank) ----
ShowSternGyroSet=false
SternSetScale=0.8
SternSetX=0.42
SternSetY=0.4
SternSetOffsetX=0
SternSetOffsetY=0
# ---- TDC target bearing window (Zielrichtung: fine dial 10 degrees a turn, coarse dial 360) ----
ShowBearing=false
BearingScale=1
BearingX=0.3
BearingY=0.62
BearingOffsetX=0
BearingOffsetY=0
VoiceCallouts=true

# ---- Follow-up / gyro receiver panel ----
ShowControls=true
# Panel shown or hidden (the button in the periscope's left sidebar sets this)
ControlsVisible=true
# Panel rolled up to its title bar (the caret next to the title sets this)
PanelCollapsed=false
# Raise fully / lower fully buttons above and below the periscope raise/lower control
ElevatorButtons=true
# true = hide the game's periscope raise/lower control and the buttons above (the keys still raise and lower it)
HideStockElevator=false
# Elevator = beside the periscope raise/lower control ; Screen = ControlsX/ControlsY
ControlsAnchor=Elevator
# Extra offset in 1080p pixels. Ctrl+drag updates these.
ControlsOffsetX=0
ControlsOffsetY=0
ControlsGap=10
ControlsX=0.92
ControlsY=0.5

# ---- TDC dial window (AOB, target speed, target distance) ----
# Shown and hidden together with the control panel (sidebar button)
# Gyro angle counters under the bearing tape shown (also the WINDOWS check box)
ShowGyros=true
ShowDials=true
# Centre of the window, 0..1 of screen width/height from bottom-left
DialsX=0.12
DialsY=0.6
# Extra offset in 1080p pixels. Ctrl+drag updates these.
DialsOffsetX=0
DialsOffsetY=0

# ---- Fire-control switch box (single shot, salvo, launch station) ----
# Shown and hidden together with the control panel (sidebar button)
ShowSwitches=true
# Centre of the window, 0..1 of screen width/height from bottom-left
SwitchesX=0.12
SwitchesY=0.11
# Extra offset in 1080p pixels. Ctrl+drag updates these.
SwitchesOffsetX=0
SwitchesOffsetY=0

# ---- Launch lever (Aus / Gesichert / Schuß) ----
# Shown and hidden together with the control panel (sidebar button)
ShowLever=true
# Centre of the window, 0..1 of screen width/height from bottom-left
LeverX=0.80
LeverY=0.17
# Extra offset in 1080p pixels. Ctrl+drag updates these.
LeverOffsetX=0
LeverOffsetY=0

# ---- Direction selector (Zielrichtung / Schußrichtung: bearing source, gyro receiver) ----
ShowDirections=true
DirectionsX=0.285
DirectionsY=0.11
DirectionsOffsetX=0
DirectionsOffsetY=0


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

                // 1.2 test builds put the switch box at the bottom centre; move an untouched one to its new home
                if (SwitchesX == 0.5f && SwitchesY == 0.13f && SwitchesOffsetX == 0f && SwitchesOffsetY == 0f)
                {
                    SwitchesX = 0.12f;
                    SwitchesY = 0.11f;
                    SavePrecise("SwitchesX", SwitchesX);
                    SavePrecise("SwitchesY", SwitchesY);
                }
                // four stacked dials are taller: raise an untouched dial window clear of the switch box
                if (DialsY == 0.5f && DialsOffsetX == 0f && DialsOffsetY == 0f)
                {
                    DialsY = 0.6f;
                    SavePrecise("DialsY", DialsY);
                }
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

        /// <summary>Save a value with more decimals (screen fractions).</summary>
        public static void SavePrecise(string key, float value)
        {
            SaveText(key, value.ToString("0.#####", CultureInfo.InvariantCulture));
        }

        /// <summary>The show / hide hotkey (KeyCode.None = no hotkey: an empty entry).</summary>
        public static void SaveToggleKey(KeyCode key)
        {
            ToggleKey = key;
            SaveText("ToggleKey", key == KeyCode.None ? "" : key.ToString());
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
                case "tapeanchorx": TapeAnchorX = v.Length == 0 ? float.NaN : Num(v, float.NaN); break;
                case "tapeanchory": TapeAnchorY = v.Length == 0 ? float.NaN : Num(v, float.NaN); break;
                case "scale": Scale = Mathf.Clamp(Num(v, Scale), 0.25f, 5f); break;
                case "format": LeftRight = v.Equals("lr", StringComparison.OrdinalIgnoreCase); break;
                case "showtenths": ShowTenths = Bool(v, ShowTenths); break;
                case "rollspeed": RollSpeed = Mathf.Max(0f, Num(v, RollSpeed)); break;
                case "impacttimefont": ImpactTimeFont = v.Trim(); break;
                case "impacttimeweight": ImpactTimeWeight = Mathf.Clamp(Num(v, ImpactTimeWeight), -1f, 1f); break;
                case "showsynclamp": ShowSyncLamp = Bool(v, ShowSyncLamp); break;
                case "showreceiverangle": ShowReceiverAngle = Bool(v, ShowReceiverAngle); break;
                case "activereceiveronly": ActiveReceiverOnly = Bool(v, ActiveReceiverOnly); break;
                case "showparallax": ShowParallax = Bool(v, ShowParallax); break;
                case "autoparallax": AutoParallax = Bool(v, AutoParallax); break;
                case "autoreceiver": AutoReceiver = Bool(v, AutoReceiver); break;
                case "realistictdc": RealisticTdc = Bool(v, RealisticTdc); break;
                case "realisticreceiversync": RealisticReceiverSync = Bool(v, RealisticReceiverSync); break;
                case "singletorpedolaunchfix": SingleTorpedoLaunchFix = Bool(v, SingleTorpedoLaunchFix); break;
                case "autoselectfloodedtube": AutoSelectFloodedTube = Bool(v, AutoSelectFloodedTube); break;
                case "enhancedtubehandling": EnhancedTubeHandling = Bool(v, EnhancedTubeHandling); break;
                case "torpedotimers": TorpedoTimers = Bool(v, TorpedoTimers); break;
                case "timersx": TimersX = Mathf.Clamp01(Num(v, TimersX)); break;
                case "timersy": TimersY = Mathf.Clamp01(Num(v, TimersY)); break;
                case "tdcoperatorrequired": TdcOperatorRequired = Bool(v, TdcOperatorRequired); break;
                case "ctrldragtomove": CtrlDragToMove = Bool(v, CtrlDragToMove); break;
                case "dialsscale": DialsScale = Mathf.Clamp(Num(v, DialsScale), WindowScaleMin, WindowScaleMax); break;
                case "switchesscale": SwitchesScale = Mathf.Clamp(Num(v, SwitchesScale), WindowScaleMin, WindowScaleMax); break;
                case "directionsscale": DirectionsScale = Mathf.Clamp(Num(v, DirectionsScale), WindowScaleMin, WindowScaleMax); break;
                case "leverscale": LeverScale = Mathf.Clamp(Num(v, LeverScale), WindowScaleMin, WindowScaleMax); break;
                case "torpedoscale": TorpedoScale = Mathf.Clamp(Num(v, TorpedoScale), WindowScaleMin, WindowScaleMax); break;
                case "showtorpedo": ShowTorpedo = Bool(v, ShowTorpedo); break;
                case "torpedox": TorpedoX = Mathf.Clamp01(Num(v, TorpedoX)); break;
                case "torpedoy": TorpedoY = Mathf.Clamp01(Num(v, TorpedoY)); break;
                case "torpedooffsetx": TorpedoOffsetX = Num(v, TorpedoOffsetX); break;
                case "torpedooffsety": TorpedoOffsetY = Num(v, TorpedoOffsetY); break;
                case "showspread": ShowSpread = Bool(v, ShowSpread); break;
                case "autospread": AutoSpread = Bool(v, AutoSpread); break;
                case "showangularspeed": ShowAngularSpeed = Bool(v, ShowAngularSpeed); break;
                case "tintpanels": TintPanels = Bool(v, TintPanels); break;
                case "keepscopebearing": KeepScopeBearing = Bool(v, KeepScopeBearing); break;
                case "spreadscale": SpreadScale = Mathf.Clamp(Num(v, SpreadScale), WindowScaleMin, WindowScaleMax); break;
                case "spreadoffsetx": SpreadOffsetX = Num(v, SpreadOffsetX); break;
                case "spreadoffsety": SpreadOffsetY = Num(v, SpreadOffsetY); break;
                case "turnratescale": TurnRateScale = Mathf.Clamp(Num(v, TurnRateScale), WindowScaleMin, WindowScaleMax); break;
                case "turnrateoffsetx": TurnRateOffsetX = Num(v, TurnRateOffsetX); break;
                case "turnrateoffsety": TurnRateOffsetY = Num(v, TurnRateOffsetY); break;
                case "spreadanchored": SpreadAnchored = Bool(v, SpreadAnchored); break;
                case "turnrateanchored": TurnRateAnchored = Bool(v, TurnRateAnchored); break;
                case "switchesanchored": SwitchesAnchored = Bool(v, SwitchesAnchored); break;
                case "spreadfreex": SpreadFreeX = Num(v, SpreadFreeX); break;
                case "spreadfreey": SpreadFreeY = Num(v, SpreadFreeY); break;
                case "turnratefreex": TurnRateFreeX = Num(v, TurnRateFreeX); break;
                case "turnratefreey": TurnRateFreeY = Num(v, TurnRateFreeY); break;
                case "switchesanchoroffsetx": SwitchesAnchorOffsetX = Num(v, SwitchesAnchorOffsetX); break;
                case "switchesanchoroffsety": SwitchesAnchorOffsetY = Num(v, SwitchesAnchorOffsetY); break;
                case "showtubes": ShowTubes = Bool(v, ShowTubes); break;
                case "tubesscale": TubesScale = Mathf.Clamp(Num(v, TubesScale), WindowScaleMin, WindowScaleMax); break;
                case "tubesx": TubesX = Mathf.Clamp01(Num(v, TubesX)); break;
                case "tubesy": TubesY = Mathf.Clamp01(Num(v, TubesY)); break;
                case "tubesoffsetx": TubesOffsetX = Num(v, TubesOffsetX); break;
                case "tubesoffsety": TubesOffsetY = Num(v, TubesOffsetY); break;
                case "showtruebearing": ShowTrueBearing = Bool(v, ShowTrueBearing); break;
                case "trueoffsetx": TrueOffsetX = Num(v, TrueOffsetX); break;
                case "trueoffsety": TrueOffsetY = Num(v, TrueOffsetY); break;
                case "truebearingonbinoculars": TrueBearingOnBinoculars = Bool(v, TrueBearingOnBinoculars); break;
                case "truebinooffsetx": TrueBinoOffsetX = Num(v, TrueBinoOffsetX); break;
                case "truebinooffsety": TrueBinoOffsetY = Num(v, TrueBinoOffsetY); break;
                case "showgyroset": ShowGyroSet = Bool(v, ShowGyroSet); break;
                case "gyrosetscale": GyroSetScale = Mathf.Clamp(Num(v, GyroSetScale), WindowScaleMin, WindowScaleMax); break;
                case "gyrosetx": GyroSetX = Mathf.Clamp01(Num(v, GyroSetX)); break;
                case "gyrosety": GyroSetY = Mathf.Clamp01(Num(v, GyroSetY)); break;
                case "gyrosetoffsetx": GyroSetOffsetX = Num(v, GyroSetOffsetX); break;
                case "gyrosetoffsety": GyroSetOffsetY = Num(v, GyroSetOffsetY); break;
                case "showsterngyroset": ShowSternGyroSet = Bool(v, ShowSternGyroSet); break;
                case "sternsetscale": SternSetScale = Mathf.Clamp(Num(v, SternSetScale), WindowScaleMin, WindowScaleMax); break;
                case "sternsetx": SternSetX = Mathf.Clamp01(Num(v, SternSetX)); break;
                case "sternsety": SternSetY = Mathf.Clamp01(Num(v, SternSetY)); break;
                case "sternsetoffsetx": SternSetOffsetX = Num(v, SternSetOffsetX); break;
                case "sternsetoffsety": SternSetOffsetY = Num(v, SternSetOffsetY); break;
                case "showbearing": ShowBearing = Bool(v, ShowBearing); break;
                case "bearingscale": BearingScale = Mathf.Clamp(Num(v, BearingScale), WindowScaleMin, WindowScaleMax); break;
                case "bearingx": BearingX = Mathf.Clamp01(Num(v, BearingX)); break;
                case "bearingy": BearingY = Mathf.Clamp01(Num(v, BearingY)); break;
                case "bearingoffsetx": BearingOffsetX = Num(v, BearingOffsetX); break;
                case "bearingoffsety": BearingOffsetY = Num(v, BearingOffsetY); break;
                case "voicecallouts": VoiceCallouts = Bool(v, VoiceCallouts); break;
                case "showcontrols": ShowControls = Bool(v, ShowControls); break;
                case "controlsanchor": ControlsAtElevator = !v.Equals("screen", StringComparison.OrdinalIgnoreCase); break;
                case "controlsvisible": ControlsVisible = Bool(v, ControlsVisible); break;
                case "panelcollapsed": PanelCollapsed = Bool(v, PanelCollapsed); break;
                case "elevatorbuttons": ElevatorButtons = Bool(v, ElevatorButtons); break;
                case "hidestockelevator": HideStockElevator = Bool(v, HideStockElevator); break;
                case "controlsoffsetx": ControlsOffsetX = Num(v, ControlsOffsetX); break;
                case "controlsoffsety": ControlsOffsetY = Num(v, ControlsOffsetY); break;
                case "controlsgap": ControlsGap = Num(v, ControlsGap); break;
                case "controlsx": ControlsX = Mathf.Clamp01(Num(v, ControlsX)); break;
                case "controlsy": ControlsY = Mathf.Clamp01(Num(v, ControlsY)); break;
                case "showgyros": ShowGyros = Bool(v, ShowGyros); break;
                case "showdials": ShowDials = Bool(v, ShowDials); break;
                case "dialsx": DialsX = Mathf.Clamp01(Num(v, DialsX)); break;
                case "dialsy": DialsY = Mathf.Clamp01(Num(v, DialsY)); break;
                case "dialsoffsetx": DialsOffsetX = Num(v, DialsOffsetX); break;
                case "dialsoffsety": DialsOffsetY = Num(v, DialsOffsetY); break;
                case "showswitches": ShowSwitches = Bool(v, ShowSwitches); break;
                case "showlever": ShowLever = Bool(v, ShowLever); break;
                case "leverx": LeverX = Mathf.Clamp01(Num(v, LeverX)); break;
                case "levery": LeverY = Mathf.Clamp01(Num(v, LeverY)); break;
                case "leveroffsetx": LeverOffsetX = Num(v, LeverOffsetX); break;
                case "leveroffsety": LeverOffsetY = Num(v, LeverOffsetY); break;
                case "showdirections": ShowDirections = Bool(v, ShowDirections); break;
                case "directionsx": DirectionsX = Mathf.Clamp01(Num(v, DirectionsX)); break;
                case "directionsy": DirectionsY = Mathf.Clamp01(Num(v, DirectionsY)); break;
                case "directionsoffsetx": DirectionsOffsetX = Num(v, DirectionsOffsetX); break;
                case "directionsoffsety": DirectionsOffsetY = Num(v, DirectionsOffsetY); break;
                case "switchesx": SwitchesX = Mathf.Clamp01(Num(v, SwitchesX)); break;
                case "switchesy": SwitchesY = Mathf.Clamp01(Num(v, SwitchesY)); break;
                case "switchesoffsetx": SwitchesOffsetX = Num(v, SwitchesOffsetX); break;
                case "switchesoffsety": SwitchesOffsetY = Num(v, SwitchesOffsetY); break;
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
