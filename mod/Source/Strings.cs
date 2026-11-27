using System;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>German / English labels and pop-up descriptions.</summary>
    public static class Strings
    {
        static bool german;
        static bool logged;

        /// <summary>Resolve the language once per periscope entry (Language=Auto follows the game).</summary>
        public static void Resolve()
        {
            if (GyroConfig.Language == LanguageMode.German) { german = true; return; }
            if (GyroConfig.Language == LanguageMode.English) { german = false; return; }

            string lang = null;
            try
            {
                Locale locale = null;
                var fw = InjectionFramework.Instance;
                if (fw != null) locale = fw.GetInstance<Locale>();
                if (locale == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<Locale>();
                    if (all != null && all.Length > 0) locale = all[0];
                }
                if (locale != null) lang = locale.Language;
            }
            catch { }

            string l = (lang ?? "").Trim().ToLowerInvariant();
            german = l.StartsWith("de") || l.Contains("german") || l.Contains("deutsch");
            if (!logged)
            {
                logged = true;
                Debug.Log(ScopeTdcControlsMod.Tag + " game language '" + (lang ?? "?") + "' -> " + (german ? "German" : "English") + " labels");
            }
        }

        static string T(string de, string en) { return german ? de : en; }

        // ---- labels ----
        public static string BearingHeader { get { return T("ZIELPEILUNG", "TARGET BEARING"); } }
        public static string Follow { get { return T("FOLGEN", "FOLLOWING"); } }
        public static string NoFollow { get { return T("NICHT FOLGEN", "NOT FOLLOWING"); } }
        public static string FollowBtn { get { return T("FOLGEN", "FOLLOW"); } }
        public static string NoFollowBtn { get { return T("NICHT FOLGEN", "NOT FOLLOW"); } }
        public static string TdcNotManned { get { return T("Rechner nicht besetzt", "TDC not manned"); } }
        public static string ReceiverHeader { get { return T("SCHUSSWINKELEMPFÄNGER", "GYRO ANGLE RECEIVER"); } }
        public static string Off { get { return T("AUS", "OFF"); } }
        public static string Bow { get { return T("BUG", "BOW"); } }
        public static string Stern { get { return T("HECK", "STERN"); } }

        public static string TdcLabel { get { return "TDC Gyro"; } }
        public static string ParallaxLabel { get { return T("Parallaxe", "Parallax"); } }
        public static string ParallaxHeader { get { return T("PARALLAXENMODUS", "PARALLAX MODE"); } }
        public static string ParForward { get { return T("VORN", "FORWARD"); } }
        public static string ParBackward { get { return T("ACHTERN", "BACKWARD"); } }

        // ---- bearing source (transmitter) ----
        public static string SourceHeader { get { return T("PEILUNGSGEBER", "BEARING SOURCE"); } }
        public static string TxNone { get { return T("KEINER", "NONE"); } }
        public static string TxUzo { get { return "UZO"; } }
        public static string TxObservation { get { return T("BEOBACHTUNG", "OBSERVATION"); } }
        public static string TxAttack { get { return T("ANGRIFF", "ATTACK"); } }

        static string ScopeNote
        {
            get
            {
                return T("\n<i>Deine Wahl gilt, bis ein neues Ziel aufgeschaltet wird. Gesperrt, wenn das Boot dieses Sehrohr nicht hat.</i>",
                         "\n<i>Your pick holds until a new target is locked. Unavailable if the boat doesn't have this scope.</i>");
            }
        }

        public static string TipSourceHeader
        {
            get
            {
                return T("<b>Peilungsgeber</b>\nLegt fest, welches Sehrohr seine Peilung an den Vorhaltrechner überträgt. Normalerweise wählt das Spiel das Sehrohr, durch das du gerade schaust. Eine Wahl hier hat Vorrang, bis ein neues Ziel aufgeschaltet wird.",
                         "<b>Bearing source</b>\nSelects which scope sends its bearing to the TDC. Normally the game picks the scope you are looking through. A pick here takes priority until a new target is locked.");
            }
        }

        public static string TipTxNone
        {
            get { return T("<b>Keiner</b>\nKein Sehrohr überträgt eine Peilung; der Rechner behält die letzte.", "<b>None</b>\nNo scope sends a bearing; the TDC keeps the last one.") + ScopeNote; }
        }

        public static string TipTxUzo
        {
            get { return T("<b>UZO</b>\nDie Überwasser-Zieloptik auf der Brücke überträgt ihre Peilung.", "<b>UZO</b>\nThe surface targeting sight on the bridge sends its bearing.") + ScopeNote; }
        }

        public static string TipTxObservation
        {
            get { return T("<b>Beobachtungssehrohr</b>\nDas Beobachtungssehrohr in der Zentrale überträgt seine Peilung.", "<b>Observation periscope</b>\nThe observation periscope in the control room sends its bearing.") + ScopeNote; }
        }

        public static string TipTxAttack
        {
            get { return T("<b>Angriffssehrohr</b>\nDas Angriffssehrohr im Turm überträgt seine Peilung.", "<b>Attack periscope</b>\nThe attack periscope in the conning tower sends its bearing.") + ScopeNote; }
        }

        public static string TipParallaxHeader
        {
            get
            {
                return T("<b>Parallaxe</b>\nRichtung der Parallaxkorrektur im Vorhaltrechner: vorn für die Bugrohre, achtern für die Heckrohre.",
                         "<b>Parallax</b>\nDirection of the TDC's parallax correction: forward for the bow tubes, backward for the stern tubes.") + ParallaxAutoNote;
            }
        }

        public static string TipParallaxCounter
        {
            get
            {
                return T("<b>Parallaxe (TDC)</b>\nKorrektur, die der Vorhaltrechner zum Schusswinkel addiert, weil Sehrohr und Torpedorohre nicht am selben Ort sind. Die Richtung (vorn/achtern) wählst du im Seitenfeld.",
                         "<b>Parallax (TDC)</b>\nThe correction the TDC adds to the gyro angle, because the periscope and the torpedo tubes are not in the same place. Set the direction (forward/backward) in the side panel.");
            }
        }

        static string ParallaxAutoNote
        {
            get
            {
                return GyroConfig.AutoParallax
                    ? T("\n<i>Automatische Parallaxe ist an: folgt den angehakten Rohren, sonst dem Empfänger, sonst der Zielpeilung. Zum manuellen Wählen in den Einstellungen abschalten.</i>",
                        "\n<i>Auto set parallax is on: follows the checked tubes, else the receiver, else the target bearing. Turn it off in Settings to choose manually.</i>")
                    : "";
            }
        }

        public static string TipParForward
        {
            get { return T("<b>Parallaxe vorn</b>\nFür Schüsse aus den Bugrohren.", "<b>Parallax forward</b>\nFor shots from the bow tubes.") + ParallaxAutoNote; }
        }

        public static string TipParBackward
        {
            get { return T("<b>Parallaxe achtern</b>\nFür Schüsse aus den Heckrohren.", "<b>Parallax backward</b>\nFor shots from the stern tubes.") + ParallaxAutoNote; }
        }

        // ---- settings drop-down ----
        public static string SettingsHeader { get { return T("EINSTELLUNGEN", "SETTINGS"); } }
        public static string OptShowReceiver { get { return T("Empfängerwinkel anzeigen", "Show gyro receiver angles"); } }
        public static string OptActiveReceiverOnly { get { return T("Nur aktiven Empfänger zeigen", "Show active receivers only"); } }
        public static string OptShowParallax { get { return T("Parallaxenwinkel anzeigen", "Show parallax angle"); } }

        public static string TipOptActiveReceiverOnly
        {
            get
            {
                return T("<b>Nur aktiven Empfänger zeigen</b>\nAn: nur der Winkel der gewählten Rohrgruppe, ausgeblendet bei Empfänger aus.\nAus: Bug- und Heckwinkel werden immer gezeigt, auch bei Empfänger aus. Der aktive hat die helle Beschriftung.",
                         "<b>Show active receivers only</b>\nOn: only the selected tube group's angle, hidden while the receiver is off.\nOff: the bow and stern angles are always shown, even while the receiver is off. The active one has the bright label.");
            }
        }
        public static string OptAutoParallax { get { return T("Parallaxenmodus automatisch setzen", "Auto set parallax mode"); } }
        public static string OptAutoReceiver { get { return T("Empfänger automatisch setzen", "Auto set gyro receiver"); } }
        public static string OptAutoSelectTube { get { return T("Rohr beim Fluten anwählen", "Auto select tube when flooding"); } }

        public static string TipOptAutoSelectTube
        {
            get
            {
                return T("<b>Rohr beim Fluten anwählen</b>\nAn: Wie im Spiel wird ein Rohr angehakt, sobald es geflutet wird, und die Schusswahl-Schalter folgen.\nAus: Das Fluten ändert weder die Rohrauswahl noch die Schalter; du wählst die Rohre selbst.",
                         "<b>Auto select tube when flooding</b>\nOn: as in the base game, a tube is checked as soon as it starts flooding, and the firing selector switches follow.\nOff: flooding changes neither the tube selection nor the switches; you select the tubes yourself.");
            }
        }

        public static string OptEnhancedTubes { get { return T("Erweiterte Torpedorohr-Bedienung", "Enhanced torpedo tube handling"); } }
        public static string OptTorpedoTimers { get { return T("Torpedo-Laufzeituhren", "Torpedo run timers"); } }

        public static string TipOptTorpedoTimers
        {
            get
            {
                return T("<b>Torpedo-Laufzeituhren</b>\nAn: Für jeden abgefeuerten Torpedo erscheint oben am Bildschirm ein Torpedo mit einer Uhr, die von der Laufzeit herunterzählt, die beim Schuss angezeigt wurde (in Spielzeit, auch in allen anderen Ansichten). Mehrere Torpedos stehen untereinander. Bei 0:00 verblasst der Eintrag über eine Minute. Zum Verschieben ziehen. Doppelklick auf einen Eintrag: die Orbit-Kamera auf diesem Torpedo, wie die Kamera-Taste auf der Karte (nochmal Doppelklick: zurück).",
                         "<b>Torpedo run timers</b>\nOn: each torpedo launched gets a torpedo and a clock near the top of the screen, counting down from the time to impact shown at launch (in game time, and in every view). Several torpedoes stack. At 0:00 the entry fades out over a minute. Drag to move them. Double-click an entry: the orbit camera on that torpedo, as with the map's camera button (double-click again to go back).");
            }
        }

        public static string TipOptEnhancedTubes
        {
            get
            {
                return T("<b>Erweiterte Torpedorohr-Bedienung</b>\nAn: Die Mündungsklappe eines gefluteten Rohrs wird von Hand geöffnet (Rechtsklick auf das Rohr, 3 s). Ein Rohr feuert nur mit ganz geöffneter Klappe (auch die Feuertaste des Spiels überspringt es sonst). Nach dem Schuss bleibt das Rohr geflutet und leer, die Klappe offen; die Besatzung lädt es erst nach, wenn du die Klappe schließt und das Rohr lenzt. Mit offener Klappe kann nicht gelenzt werden. Nach dem Feuerbefehl läuft der erste Torpedo nach 0,25 s statt 1,5 s (die Klappen sind ja schon offen). Der Klappenzustand wird nicht mit dem Spielstand gespeichert.\nAus: keine Mündungsklappen (wie im Spiel).",
                         "<b>Enhanced torpedo tube handling</b>\nOn: a flooded tube's outer door is opened by hand (right-click the tube, 3 s). A tube fires only with its door fully open (the game's fire key skips it otherwise too). After the shot the tube stays flooded and empty with its door open; the crew reloads it only once you shut the door and drain it. A tube can't be drained with its door open. After a fire command the first torpedo goes after 0.25 s instead of 1.5 s (the doors are already open). The door state isn't saved with the game.\nOff: no outer doors (base game).");
            }
        }

        public static string TipOptAutoReceiver
        {
            get
            {
                return T("<b>Empfänger automatisch setzen</b>\nAn: Der Empfänger folgt den angehakten, gefluteten Rohren. Keines angehakt = Aus. Bug- und Heckrohre angehakt = das Ende, in das das Sehrohr blickt (bei eingeschalteter Peilungsfolge). Eine Auswahl von Hand hat Vorrang, bis du die Rohrauswahl änderst.\nAus: Nur du stellst den Empfänger; die Rohrauswahl ändert ihn nicht.",
                         "<b>Auto set gyro receiver</b>\nOn: the receiver follows the checked, flooded tubes. None checked = Off. Bow and stern both checked = the end the periscope is looking toward (while follow-up is on). A manual pick takes priority until you change which tubes are checked.\nOff: only you set the receiver; checking tubes doesn't change it.");
            }
        }
        public static string OptRealisticTdc { get { return T("Realistischer Rechner-Nachlauf", "Realistic TDC follower"); } }

        public static string TipOptShowParallax
        {
            get
            {
                return T("<b>Parallaxenwinkel anzeigen</b>\nZeigt zwischen TDC- und Rohrzähler die Parallaxkorrektur des Rechners mit den Tasten Vorn / Achtern.",
                         "<b>Show parallax angle</b>\nShows the TDC's parallax correction, with Forward / Backward buttons, between the TDC and tube counters.");
            }
        }

        public static string TipOptAutoParallax
        {
            get
            {
                return T("<b>Parallaxenmodus automatisch setzen</b>\nAn, erste zutreffende Regel:\n1. Angehakte Rohre: Bugrohre = vorn, Heckrohre = achtern.\n2. Gewählter Empfänger: Bug = vorn, Heck = achtern.\n3. Sonst die Zielpeilung des Rechners: vorn, solange das Ziel vor dem Querab steht, achtern, sobald es achterlicher steht.\nAus: Du wählst die Richtung selbst mit Vorn / Achtern, im realistischen wie im schnellen Rechnermodus.",
                         "<b>Auto set parallax mode</b>\nOn, first rule that applies:\n1. Checked tubes: bow tubes = forward, stern tubes = backward.\n2. Selected receiver: Bow = forward, Stern = backward.\n3. Otherwise the TDC's target bearing: forward while the target is ahead of the beam, backward once it is abaft.\nOff: you choose the direction yourself with Forward / Backward, in both realistic and fast TDC mode.");
            }
        }
        public static string OptRealisticSync { get { return T("Realistischer Empfängerabgleich", "Realistic receiver sync"); } }
        public static string OptRealisticFire { get { return T("Einzelschuss-Korrektur", "Single torpedo launch fix"); } }

        public static string TipOptRealisticFire
        {
            get
            {
                return T("<b>Einzelschuss-Korrektur</b>\nAn: Ein Schussbefehl feuert nur die Rohre, die beim Drücken angehakt waren, wie der Abfeuerungsknopf an Bord, der nur die vorgewählten Rohre auslöste. Danach angehakte Rohre warten auf den nächsten Befehl. Ein Rohr während der Schussfolge abzuhaken hält es weiterhin zurück.\nAus: Wie im Originalspiel; ein während der Schussfolge angehaktes Rohr wird mit abgefeuert.",
                         "<b>Single torpedo launch fix</b>\nOn: a fire command fires only the tubes that were checked when you pressed fire, like the real firing key, which fired only the pre-selected tubes. Tubes checked afterwards wait for the next command. Unchecking a tube during the sequence still holds it back.\nOff: as in the base game; a tube checked during the firing sequence is fired as well.");
            }
        }

        public static string OptTdcOperator { get { return T("Rechnerbediener erforderlich", "TDC operator required"); } }

        public static string OptVoiceCallouts { get { return T("Sprachmeldungen", "Voice callouts"); } }

        public static string OptCtrlDrag { get { return T("Strg+Ziehen zum Verschieben", "Ctrl+drag to move"); } }

        public static string TipOptCtrlDrag
        {
            get
            {
                return T("<b>Strg+Ziehen zum Verschieben</b>\nAn: Fenster, Zähler und Bedienfeld werden mit gedrückter Strg-Taste verschoben.\nAus: einfach den Hintergrund eines Fensters ziehen. Scheiben, Schalter und Hebel werden dabei weiter wie gewohnt bedient.",
                         "<b>Ctrl+drag to move</b>\nOn: hold Ctrl and drag to move the windows, counters and panel.\nOff: just drag a window's background. Dials, switches and levers still work as usual.");
            }
        }

        public static string ResetSizes { get { return T("FENSTERGRÖSSEN ZURÜCK", "RESET WINDOW SIZES"); } }

        public static string TipResetSizes
        {
            get
            {
                return T("<b>Fenstergrößen zurücksetzen</b>\nSetzt Rechnerscheiben, Schusswahl, Richtungswahl und Abfeuerhebel wieder auf ihre normale Größe (die linke obere Ecke bleibt, wo sie ist).",
                         "<b>Reset window sizes</b>\nPuts the TDC dials, firing selector, direction selector and launch lever back to their normal size (each window's top-left corner stays put).");
            }
        }

        public static string TipResize
        {
            get
            {
                return T("<b>Größe ändern</b>\nZiehen, um das Fenster zu vergrößern oder zu verkleinern. Die linke obere Ecke bleibt stehen; die Größe wird gespeichert.",
                         "<b>Resize</b>\nDrag to make the window bigger or smaller. Its top-left corner stays put; the size is remembered.");
            }
        }

        public static string TipOptVoiceCallouts
        {
            get
            {
                return T("<b>Sprachmeldungen</b>\nAn: Werte, die mit den Rechner-Scheiben links eingestellt werden, meldet der Mann am Sehrohr mit den Sprachmeldungen des Spiels, wie beim Übergeben eines aufgeschalteten Ziels an den Rechner. Die Werte gelten sofort beim Drehen und werden nach dem Loslassen gemeldet.\nAus: Werte gelten ohne Meldung.",
                         "<b>Voice callouts</b>\nOn: values set with the TDC dials on the left are announced by the crew member at the scope, with the game's own voice lines, as when sending a locked target to the TDC. Values take effect as you turn the dials and are announced after you let go.\nOff: values are applied without callouts.");
            }
        }

        public static string TipOptTdcOperator
        {
            get
            {
                return T("<b>Rechnerbediener erforderlich</b>\nAn: Wie im Originalspiel. Ohne Besatzungsmitglied am Vorhaltrechner ist er abgeschaltet: er rechnet nicht, und die Rechner-Taste in der Stationsleiste ist gesperrt.\nAus: Der Rechner rechnet auch unbesetzt und kann über die Stationsleiste geöffnet und bedient werden. Stromausfall und Schäden schalten ihn weiterhin ab.",
                         "<b>TDC operator required</b>\nOn: as in the base game. Without a crew member at the TDC it is switched off: it doesn't calculate, and its button on the station bar is greyed out.\nOff: the TDC calculates unmanned and can be opened and operated from the station bar. Power loss and damage still switch it off.");
            }
        }

        public static string TipRaiseFully
        {
            get
            {
                return T("<b>Sehrohr ganz ausfahren</b>\nFährt das Sehrohr bis zum Anschlag aus, so als hieltest du die Ausfahrtaste gedrückt. Erneut klicken oder eine der Hebetasten benutzen, um anzuhalten.",
                         "<b>Raise fully</b>\nRaises the periscope all the way, as if you held the raise button. Click again, or use the stock raise/lower buttons, to stop.");
            }
        }

        public static string TipLowerFully
        {
            get
            {
                return T("<b>Sehrohr ganz einfahren</b>\nFährt das Sehrohr ganz ein, so als hieltest du die Einfahrtaste gedrückt. Erneut klicken oder eine der Hebetasten benutzen, um anzuhalten.",
                         "<b>Lower fully</b>\nLowers the periscope all the way, as if you held the lower button. Click again, or use the stock raise/lower buttons, to stop.");
            }
        }

        public static string TipSidebarButton
        {
            get
            {
                return T("<b>Scope TDC Controls</b>\nBedienfeld und die eingeschalteten Fenster (Rechnerscheiben, Schusswahl, Richtungswahl, Abfeuerhebel) ein- oder ausblenden, mit den Schusswinkel-Zählern.",
                         "<b>Scope TDC Controls</b>\nShow or hide the control panel and the windows switched on there (TDC dials, firing selector, direction selector, launch lever), together with the gyro angle counters.");
            }
        }

        // ---- window toggles on the control panel ----
        public static string WindowsHeader { get { return T("FENSTER", "WINDOWS"); } }
        public static string WinGyros { get { return T("Schusswinkel", "Gyro angles"); } }
        public static string TipWinGyros
        {
            get
            {
                return T("<b>Schusswinkel</b>\nDie Schusswinkelzähler unter dem Peilungsband (Rechner, Parallaxe, Bug/Heck) ein- oder ausblenden.",
                         "<b>Gyro angles</b>\nShow or hide the gyro angle counters under the bearing tape (TDC, parallax, bow/stern).");
            }
        }
        public static string WinDials { get { return T("Rechnerscheiben", "TDC dials"); } }
        public static string WinSwitches { get { return T("Schusswahl", "Firing selector"); } }
        public static string WinDirections { get { return T("Richtungswahl", "Direction selector"); } }

        public static string ModTitle { get { return "Scope TDC Controls"; } }
        public static string ByAuthor { get { return T("von CrzyDmnd", "by CrzyDmnd"); } }
        public static string TipCollapse
        {
            get
            {
                return T("<b>Ein-/ausklappen</b>\nBedienfeld auf die Titelleiste einrollen oder wieder ausklappen.",
                         "<b>Collapse / expand</b>\nRoll the panel up to its title bar, or out again.");
            }
        }

        public static string TipWinDirections
        {
            get
            {
                return T("<b>Richtungswahl</b>\nDas Fenster mit den Schaltern Zielrichtung (Peilungsgeber) und Schußrichtung (Schusswinkelempfänger) ein- oder ausblenden.",
                         "<b>Direction selector</b>\nShow or hide the window with the bearing source (Zielrichtung) and gyro angle receiver (Schußrichtung) switches.");
            }
        }

        // ---- direction selector ----
        public static string DirTarget { get { return T("Zielrichtung", "Bearing source"); } }
        public static string DirShot { get { return T("Schußrichtung", "Gyro receiver"); } }
        public static string DirAttack { get { return T("Angriffssehrohr", "Attack periscope"); } }
        public static string DirAttackShort { get { return T("Zieloptik", "Attack scope"); } }
        public static string DirObs { get { return T("Beobachtungssehrohr", "Observation periscope"); } }
        public static string DirObsShort { get { return T("Beob.-Sehrohr", "Obs. scope"); } }
        public static string DirBow { get { return T("Bugtorpedoraum", "Bow tubes"); } }
        public static string DirBowShort { get { return T("Bug", "Bow"); } }
        public static string DirStern { get { return T("Hecktorpedoraum", "Stern tubes"); } }
        public static string DirSternShort { get { return T("Heck", "Stern"); } }

        public static string TipDirTarget
        {
            get
            {
                return T("<b>Zielrichtung</b> (Peilungsgeber)\nWählt, welches Sehrohr seine Peilung an den Rechner gibt: Angriffssehrohr, UZO oder Beobachtungssehrohr, oder Aus. Normalerweise wählt das Spiel das Sehrohr, durch das du schaust; eine Wahl hier gilt, bis ein neues Ziel aufgeschaltet wird. Sehrohre, die das Boot nicht hat, fehlen." + SwitchUsageDe,
                         "<b>Bearing source</b> (Zielrichtung)\nSelects which scope sends its bearing to the TDC: attack periscope, UZO or observation periscope, or off. Normally the game picks the scope you are looking through; a pick here holds until a new target is locked. Scopes the boat doesn't have are left off." + SwitchUsageEn);
            }
        }

        public static string TipDirShot
        {
            get
            {
                return T("<b>Schußrichtung</b> (Schusswinkelempfänger)\nWählt, welche Rohrgruppe den Schusswinkel des Rechners bekommt: Bug, Heck oder Aus. Mit „Empfänger automatisch“ folgt er den angehakten Rohren; eine Wahl hier gilt, bis sich die Rohrauswahl ändert." + SwitchUsageDe,
                         "<b>Gyro receiver</b> (Schußrichtung)\nSelects which tube group receives the TDC's gyro angle: bow, stern or off. With \"Auto set gyro receiver\" on it follows the checked tubes; a pick here holds until the tube selection changes." + SwitchUsageEn);
            }
        }

        public static string TipWindowsHeader
        {
            get
            {
                return T("<b>Fenster</b>\nZusätzliche Fenster dieses Mods ein- oder ausblenden. Angehakt = sichtbar. Wird gespeichert.",
                         "<b>Windows</b>\nShow or hide this mod's extra windows. Checked = shown. Remembered between sessions.");
            }
        }

        public static string TipWinDials
        {
            get
            {
                return T("<b>Rechnerscheiben</b>\nDas Fenster mit den Scheiben für Lage, Gegnerfahrt und Entfernung ein- oder ausblenden.",
                         "<b>TDC dials</b>\nShow or hide the window with the AOB, target speed and target distance dials.");
            }
        }

        public static string TipWinSwitches
        {
            get
            {
                return T("<b>Schusswahl</b>\nDas Fenster mit den Schaltern Einzelschuß, Fächerschuß und T.Abf. Schalter ein- oder ausblenden.",
                         "<b>Firing selector</b>\nShow or hide the window with the launch mode, salvo mode and firing station switches.");
            }
        }

        public static string WinLever { get { return T("Abfeuerhebel", "Launch lever"); } }

        public static string TipWinLever
        {
            get
            {
                return T("<b>Abfeuerhebel</b>\nDas Fenster mit dem Abfeuerhebel (Aus / Gesichert / Schuß) ein- oder ausblenden.",
                         "<b>Launch lever</b>\nShow or hide the window with the launch lever (Off / Safe / Fire).");
            }
        }

        // ---- launch lever ----
        public static string LeverTitle { get { return T("T-ABF.-\nSCHALTER", "LAUNCH\nLEVER"); } }
        public static string LeverOff { get { return T("Aus", "Off"); } }
        public static string LeverFire { get { return T("Schuß", "Fire"); } }
        public static string LeverWrongReceiver { get { return T("Empfänger nicht auf {0}", "Receiver not on {0}"); } }
        public static string LeverOutOfRange { get { return T("Außer Reichweite (max. {0} m)", "Out of range (max {0} m)"); } }
        public static string LeverGyroStop { get { return T("Schusswinkel {0}° über Anschlag ±{1}°", "Gyro angle {0}° past ±{1}° stop"); } }
        public static string LeverNoPower { get { return T("Kein Strom – T.Abf. Schalter Aus", "No power – firing station Off"); } }
        public static string LeverFiring { get { return T("Abfeuern …", "Firing …"); } }
        public static string LeverNothing { get { return T("Kein Rohr gewählt", "No tube selected"); } }
        public static string LeverNoBoat { get { return T("Kein Boot", "No boat"); } }
        public static string LeverNotFlooded { get { return T("Rohr {0} nicht bewässert", "Tube {0} not flooded"); } }
        public static string LeverReady { get { return T("Bereit: Rohr {0}", "Ready: tube {0}"); } }
        public static string LeverDoorShut { get { return T("Mündungsklappe Rohr {0} nicht ganz offen", "Tube {0} outer door not fully open"); } }

        public static string TipLever
        {
            get
            {
                return T("<b>Abfeuerhebel</b>\nDer Hebel ist gefedert und ruht auf Aus. Zum Abfeuern auf den Hebel drücken: er geht auf Schuß und feuert die gewählten (angehakten) Rohre, wie der Hebel im Boot, bleibt unten, solange die Maustaste gedrückt ist, und federt beim Loslassen zurück. Feuert nur bei grüner Lampe (Maus über die Lampe für Einzelheiten).",
                         "<b>Launch lever</b>\nThe lever is spring-loaded and rests at Off. To fire, press on the lever: it goes to Fire and fires the selected (checked) tubes, like the lever in the boat, stays down while the mouse button is held, and springs back when you let go. Fires only while the lamp is green (hover the lamp for details).");
            }
        }

        public static string LeverLampTitle { get { return T("Feuerbereitschaft", "Ready to fire"); } }

        public static string LeverLampLegend
        {
            get
            {
                return T("<b>grün</b> = bereit\n<b>rot</b> = gesichert, der Hebel feuert nicht:\n – kein Rohr gewählt, ein gewähltes Rohr nicht bewässert oder seine Mündungsklappe nicht ganz offen\n – Schusswinkelempfänger nicht auf Bug/Heck der gewählten Rohre\n – Entfernung über der Laufstrecke der gewählten Torpedos bei ihrer eingestellten Fahrstufe (der kürzesten, mit dem Geometriefaktor der Trommel „größte Zielentfernung“) – so hat der WO die Anlage gesichert\n – Schusswinkel über dem Anschlag von ±135° (von der Rohrachse: 0° Bug, 180° Heck)\n<b>gelb</b> = feuert\n<b>dunkel</b> = kein Strom: T.Abf. Schalter auf Aus (Brücke oder Turm schaltet ein)",
                         "<b>green</b> = ready\n<b>red</b> = safe, the lever does not fire:\n – no tube selected, or a selected tube not flooded or its outer door not fully open\n – gyro angle receiver not on the selected tubes' end (bow/stern)\n – distance beyond the selected torpedoes' run at their set speed (the shortest one, with the geometry ratio of the TDC's “max distance to target” drum) – how the watch officer made the system safe\n – gyro angle past the ±135° stop (from the tube axis: 0° bow, 180° stern)\n<b>amber</b> = firing\n<b>dark</b> = no power: firing station switch on Off (Bridge or Tower powers it)");
            }
        }


        // ---- torpedo settings window ----
        public static string WinTorpedo { get { return T("Torpedoeinstellung", "Torpedo settings"); } }

        // ---- bow torpedo room gyro setting panel ----
        public static string WinGyroSet { get { return T("Schusswinkel Bugraum", "Bow gyro setting"); } }
        public static string TipWinGyroSet
        {
            get
            {
                return T("<b>Schusswinkeleinstellung Bugraum</b>\nDie Einstellanlage im Bugtorpedoraum: Schusswinkel (fein und grob) und Streuwinkel, Handrad, Fächerschalter, Einstellart und Schnellgang. Auch über das Skalen-Symbol neben dem Bug-Zählwerk oder in der Rohranzeige zu öffnen.\n<i>Ausgegraut, solange das Boot keine Bugrohre hat.</i>",
                         "<b>Bow gyro setting</b>\nThe gyro setting panel in the bow torpedo room: gyro angle (fine and coarse) and spread angle, hand wheel, salvo size and setting mode switches, and the fast synchronization lever. Also opened with the dial icon beside the bow receiver counter or in the torpedo tube window.\n<i>Greyed out while the boat has no bow tubes.</i>");
            }
        }
        public static string TipGyroSetIcon
        {
            get { return T("<b>Schusswinkel Bugraum</b>\nKlicken öffnet oder schließt die Einstellanlage des Bugtorpedoraums.", "<b>Bow gyro setting</b>\nClick to open or close the bow torpedo room's gyro setting panel."); }
        }
        public static string GyroSetTitle { get { return T("BUGRAUM – SCHUSSWINKEL", "BOW ROOM – GYRO SETTING"); } }
        public static string TipGyroSetWindow
        {
            get
            {
                return T("<b>Schusswinkeleinstellung Bugraum</b>\nAuf jeder Scheibe zeigt der Messingzeiger den vom Rechner empfangenen Wert, die rote Fahne den an den Rohren eingestellten. Auf „Fächer von Hand, SW automatisch“ fährt die Anlage jede Fahne selbst auf ihren Zeiger.",
                         "<b>Bow gyro setting</b>\nOn each dial the brass needle is the value received from the TDC, the red flag the value set on the tubes. On “manual salvo, auto gyro” the panel drives every flag onto its needle by itself.");
            }
        }
        const string GyroFlagDe = "\nMessingzeiger: der vom Rechner empfangene Schusswinkel. Rote Fahne: der an den Rohren eingestellte. Auf „Fächer von Hand, SW automatisch“ fährt die Anlage die Fahne selbst auf den Zeiger.";
        const string GyroFlagEn = "\nBrass needle: the gyro angle received from the TDC. Red flag: the angle set on the tubes. On “manual salvo, auto gyro” the panel drives the flag onto the needle by itself.";
        public static string DialGyroFine { get { return T("SW FEIN", "GYRO FINE"); } }
        public static string DialGyroCoarse { get { return T("SW GROB", "GYRO COARSE"); } }
        public static string TipDialGyroFine
        {
            get { return T("<b>Schusswinkel fein</b>\nEine Umdrehung = 10°. Mausrad: 0,1°." + GyroFlagDe + DialUsageDe, "<b>Gyro angle, fine</b>\nOne turn = 10°. Mouse wheel: 0.1°." + GyroFlagEn + DialUsageEn); }
        }
        public static string TipDialGyroCoarse
        {
            get { return T("<b>Schusswinkel grob</b>\nEine Umdrehung = 360°. Mausrad: 1°." + GyroFlagDe + DialUsageDe + TypeUsageDe, "<b>Gyro angle, coarse</b>\nOne turn = 360°. Mouse wheel: 1°." + GyroFlagEn + DialUsageEn + TypeUsageEn); }
        }
        public static string DialGroupSpread { get { return T("STREUWINKEL", "SPREAD ANGLE"); } }
        public static string TipDialGroupSpread
        {
            get
            {
                return T("<b>Streuwinkel</b>\nMessingzeiger: der vom Rechner empfangene Streuwinkel (0 bis 20 Grad). Rote Fahne: der an den Rohren eingestellte. Mausrad: 0,5 Grad." + DialUsageDe + TypeUsageDe,
                         "<b>Spread angle</b>\nBrass needle: the spread angle received from the TDC (0 to 20 degrees). Red flag: the angle set on the tubes. Mouse wheel: 0.5°." + DialUsageEn + TypeUsageEn);
            }
        }
        public static string DialHandWheel { get { return T("HANDRAD", "HAND WHEEL"); } }
        public static string WheelGyro { get { return T("SW", "Gyro"); } }
        public static string WheelSpread { get { return T("Streu", "Spread"); } }
        public static string TipDialHandWheel
        {
            get
            {
                return T("<b>Handrad</b>\nAuf „Schusswinkel von Hand“ stellt es die Schusswinkel-Fahne (eine Umdrehung = 180°, mit Umschalt 18°; Mausrad 0,5°), sonst die Streuwinkel-Fahne (eine Umdrehung = 20°); die Anzeige darunter zeigt, welche. Drehen, Umschalt = 10x feiner, Mausrad = in Schritten.",
                         "<b>Hand wheel</b>\nOn “manual gyro” it sets the gyro angle flag (one turn = 180°, 18° with Shift; mouse wheel 0.5°), otherwise the spread angle flag (one turn = 20°); the readout below shows which. Turn it; Shift = 10x finer, mouse wheel = steps.");
            }
        }
        public static string SwitchSalvoSize { get { return T("FÄCHER", "SALVO SIZE"); } }
        public static string TipSwitchSalvoSize
        {
            get { return T("<b>Fächergröße</b>\nWie viele Torpedos der Fächer hat (1 bis 4); die Anlage verteilt den Streuwinkel darauf.", "<b>Salvo size</b>\nHow many torpedoes the salvo has (1 to 4); the panel spreads the spread angle over them."); }
        }
        public static string SwitchAngleMode { get { return T("EINSTELLART", "SETTING MODE"); } }
        public static string TipSwitchAngleMode
        {
            get
            {
                return T("<b>Einstellart</b>\n<b>Auto</b> – Fächer von Hand, Schusswinkel automatisch: die Anlage fährt alle Fahnen auf ihre Zeiger.\n<b>Fächer</b> – Fächer von Hand: das Handrad stellt den Streuwinkel.\n<b>SW</b> – Schusswinkel von Hand: das Handrad stellt den Schusswinkel.\nKlicken, ziehen oder Mausrad.",
                         "<b>Setting mode</b>\n<b>Auto</b> – manual salvo, auto gyro: the panel drives every flag onto its needle.\n<b>Salvo</b> – manual salvo: the hand wheel sets the spread angle.\n<b>Gyro</b> – manual gyro: the hand wheel sets the gyro angle.\nClick, drag or use the mouse wheel.");
            }
        }
        public static string[] AngleModeLabels { get { return german ? new[] { "AUTO", "FÄCHER", "SW" } : new[] { "AUTO", "SALVO", "GYRO" }; } }
        public static string[] AngleModeNames
        {
            get
            {
                return german ? new[] { "Fächer Hand, SW auto", "Fächer von Hand", "SW von Hand" }
                              : new[] { "Man. salvo, auto gyro", "Manual salvo", "Manual gyro" };
            }
        }
        public static string FastSync { get { return T("SCHNELLGANG", "FAST SYNC"); } }
        public static string TipFastSync
        {
            get
            {
                return T("<b>Schnellgang</b>\nGedrückt halten (links = -, rechts = +), um die automatische Schusswinkel-Einstellung in diese Richtung zu beschleunigen. Die Lampe über einer Seite leuchtet, wenn die Fahne mehr als 3,5° in diese Richtung nachläuft.",
                         "<b>Fast synchronization</b>\nHold it down (left = -, right = +) to speed the automatic gyro setting that way. The lamp over a side lights when the flag lags more than 3.5° that way.");
            }
        }

        // ---- stern torpedo room gyro setting panel ----
        public static string WinSternSet { get { return T("Schusswinkel Heckraum", "Stern gyro setting"); } }
        public static string TipWinSternSet
        {
            get
            {
                return T("<b>Schusswinkeleinstellung Heckraum</b>\nDer Schusswinkelempfänger im Hecktorpedoraum: Schusswinkel grob und fein und die Handkurbel. Auch über das Skalen-Symbol neben dem Heck-Zählwerk oder am Ende der Heck-Reihe in der Rohranzeige zu öffnen.\n<i>Ausgegraut, solange das Boot keine Heckrohre hat.</i>",
                         "<b>Stern gyro setting</b>\nThe gyro angle receiver in the stern torpedo room: gyro angle coarse and fine and the hand crank. Also opened with the dial icon beside the stern receiver counter or at the end of the Aft row in the torpedo tube window.\n<i>Greyed out while the boat has no stern tubes.</i>");
            }
        }
        public static string TipSternSetIcon
        {
            get { return T("<b>Schusswinkel Heckraum</b>\nKlicken öffnet oder schließt den Schusswinkelempfänger des Hecktorpedoraums.", "<b>Stern gyro setting</b>\nClick to open or close the stern torpedo room's gyro angle receiver."); }
        }
        public static string SternSetTitle { get { return T("HECKRAUM – SCHUSSWINKEL", "STERN ROOM – GYRO SETTING"); } }
        public static string TipSternSetWindow
        {
            get
            {
                return T("<b>Schusswinkeleinstellung Heckraum</b>\nMessingzeiger: der vom Rechner empfangene Schusswinkel. Rote Fahne: der an den Heckrohren eingestellte. Die Handkurbel stellt die Fahne von Hand.",
                         "<b>Stern gyro setting</b>\nBrass needle: the gyro angle received from the TDC. Red flag: the angle set on the stern tubes. The hand crank sets the flag by hand.");
            }
        }
        public static string DialCrank { get { return T("KURBEL", "HAND CRANK"); } }
        public static string TipDialCrank
        {
            get
            {
                return T("<b>Handkurbel</b>\nStellt die Schusswinkel-Fahne der Heckrohre: eine Umdrehung = 180°, mit Umschalt 18°; Mausrad 0,5°. Läuft frei und bleibt stehen, wo sie gedreht wurde.",
                         "<b>Hand crank</b>\nSets the stern tubes' gyro angle flag: one turn = 180°, 18° with Shift; mouse wheel 0.5°. It turns freely and stays where it was turned to.");
            }
        }

        // ---- TRUE bearing counter ----
        public static string TrueLabel { get { return T("RW", "TRUE"); } }
        public static string WinTrueBearing { get { return T("Rechtweisende Peilung", "True bearing"); } }
        public static string TipWinTrueBearing
        {
            get
            {
                return T("<b>Rechtweisende Peilung</b>\nEin Zählwerk über dem Schusswinkel-Zählwerk: wohin das Sehrohr (oder die UZO) blickt, als Kompasspeilung 000–359°.",
                         "<b>True bearing</b>\nA counter above the gyro angle counter: where the scope (or UZO) is looking, as a compass bearing 000–359°.");
            }
        }
        public static string TipTrueBearing
        {
            get
            {
                return T("<b>Rechtweisende Peilung (RW)</b>\nWohin das Sehrohr blickt, bezogen auf Nord: Peilung vom Bug plus Kurs des Boots. Zum Eintragen von Kontakten in die Karte.",
                         "<b>True bearing</b>\nWhere the scope is looking, from north: the bearing from the bow plus the boat's heading. For plotting contacts on the map.");
            }
        }

        // ---- TDC target bearing window (Zielrichtung) ----
        public static string WinBearing { get { return T("Zielrichtung", "TDC target bearing"); } }
        public static string TipWinBearing
        {
            get
            {
                return T("<b>Zielrichtung</b>\nDie Zielrichtung-Anzeige des Vorhaltrechners zum Einstellen von Hand: oben die Feinscheibe (eine Umdrehung = 10°), unten die Grobscheibe (360°). Auch über das kleine Skalen-Symbol links vom Schusswinkel-Zählwerk zu öffnen.",
                         "<b>TDC target bearing</b>\nThe TDC's target bearing (Zielrichtung) dials, to set the bearing by hand: the fine dial on top (one turn = 10°), the coarse dial below (360°). Also opened with the small dial icon left of the gyro angle counter.");
            }
        }
        public static string BearingTitle { get { return T("ZIELRICHTUNG", "TDC TARGET BEARING"); } }
        public static string DialBearingFine { get { return T("FEIN", "FINE"); } }
        public static string DialBearingCoarse { get { return T("GROB", "COARSE"); } }
        const string BearingNeedleDe = "\nMessingzeiger: die Zielrichtung von der Peilquelle (z. B. Angriffssehrohr). Rote Fahne: die im Rechner eingestellte Zielrichtung, aus der der Schusswinkel errechnet wird.\nBei eingeschalteter Nachführung (FOLGEN) fährt der Rechner die Fahne wieder auf den Zeiger; für eine Zielrichtung von Hand auf NICHT FOLGEN schalten.";
        const string BearingNeedleEn = "\nBrass needle: the bearing coming from the bearing source (e.g. the attack periscope). Red flag: the bearing set in the TDC, which the gyro angle is worked out from.\nWith follow-up on (FOLLOW), the TDC drives the flag back onto the needle; switch it to NOT FOLLOW to keep a bearing set by hand.";
        public static string TipBearingWindow
        {
            get { return T("<b>Zielrichtung</b>" + BearingNeedleDe, "<b>TDC target bearing</b>" + BearingNeedleEn); }
        }
        public static string TipDialBearingFine
        {
            get
            {
                return T("<b>Zielrichtung fein</b>\nEine Umdrehung = 10°, Teilung 0,1°. Mausrad: 0,1°." + BearingNeedleDe + DialUsageDe,
                         "<b>Target bearing, fine</b>\nOne turn = 10°, marks every 0.1°. Mouse wheel: 0.1°." + BearingNeedleEn + DialUsageEn);
            }
        }
        public static string TipDialBearingCoarse
        {
            get
            {
                return T("<b>Zielrichtung grob</b>\nEine Umdrehung = 360°, vom Bug rechts herum. Mausrad: 1°." + BearingNeedleDe + DialUsageDe + TypeUsageDe,
                         "<b>Target bearing, coarse</b>\nOne turn = 360°, clockwise from the bow. Mouse wheel: 1°." + BearingNeedleEn + DialUsageEn + TypeUsageEn);
            }
        }
        public static string TipBearingIcon
        {
            get { return T("<b>Zielrichtung</b>\nKlicken öffnet oder schließt das Fenster zum Einstellen der Zielrichtung am Rechner.", "<b>TDC target bearing</b>\nClick to open or close the window for setting the TDC's target bearing."); }
        }
        public static string TipClose { get { return T("Schließen", "Close"); } }

        // ---- torpedo tube indicator ----
        public static string WinTubes { get { return T("Rohranzeige", "Torpedo tubes"); } }
        public static string TipWinTubes
        {
            get
            {
                return T("<b>Rohranzeige</b>\nEin kleines Fenster mit einem Kreis je Rohr (oben Bug, unten Heck): schwarz = leer, grau = geladen, blau = geflutet, grün = geflutet und gewählt, rot = Schuss (einige Sekunden nach dem Ausstoßen). Graues Kreuz (+) = Mündungsklappe zu (wird beim Öffnen weggewischt), graues X = Rohr leer, aber noch geflutet (nach dem Schuss). Klicken wählt ein geflutetes Rohr, Doppelklick flutet oder lenzt, Strg+Klick stößt mit dem Abfeuerhebel des Rohrs aus, Rechtsklick öffnet die Aktionen (auch Mündungsklappe öffnen und schließen).",
                         "<b>Torpedo tubes</b>\nA small window with one circle per tube (bow on top, stern below): black = empty, grey = loaded, blue = flooded, green = flooded and selected, red = firing (a few seconds after the launch). Grey cross (+) = outer door shut (swept away as it opens), grey X = tube empty but still flooded (after a shot). Click selects a flooded tube, double-click floods or drains it, Ctrl+click launches it with its own launch lever, right-click opens its actions (including opening and shutting the outer door).");
            }
        }
        public static string TubesBowShort { get { return T("Bug", "Fore"); } }
        public static string TubesSternShort { get { return T("Heck", "Aft"); } }
        public static string TubeEmpty { get { return T("Leer", "Empty"); } }
        public static string TubeLoading { get { return T("Wird geladen", "Loading"); } }
        public static string TubeLoaded { get { return T("Geladen, nicht geflutet", "Loaded, not flooded"); } }
        public static string TubeFlooding { get { return T("Wird geflutet", "Flooding"); } }
        public static string TubeDraining { get { return T("Wird gelenzt", "Draining"); } }
        public static string TubeLaunching { get { return T("Schuss", "Launching"); } }
        public static string TubeMaintenance { get { return T("In Wartung (Torpedo wird gewartet)", "In maintenance (torpedo being serviced)"); } }
        public static string TubeFlooded { get { return T("Geladen und geflutet", "Loaded and flooded"); } }
        public static string TubeFloodedSelected { get { return T("Geladen, geflutet und gewählt", "Loaded, flooded and selected"); } }
        public static string TubeFired { get { return T("Schuss – Torpedo läuft", "Fired – torpedo running"); } }
        public static string TubeWetEmpty { get { return T("Leer, noch geflutet (lenzen vor dem Nachladen)", "Empty, still flooded (drain before reloading)"); } }
        public static string TubeDoorLabel { get { return T("Mündungsklappe", "Outer door"); } }
        public static string DoorShut { get { return T("zu", "shut"); } }
        public static string DoorOpening { get { return T("öffnet …", "opening …"); } }
        public static string DoorOpen { get { return T("offen", "open"); } }
        public static string DoorClosing { get { return T("schließt …", "closing …"); } }
        public static string TubeDrainDoorOpen { get { return T("Lenzen erst bei geschlossener Mündungsklappe", "Drain only with the outer door shut"); } }
        public static string TubeClickSelect { get { return T("Klicken: dieses Rohr für einen Einzelschuss wählen (Schusswahl folgt)", "Click: select this tube for a single shot (the firing selector follows)"); } }
        public static string TubeClickDeselect { get { return T("Klicken: Rohr abwählen (Einzelschuß auf Aus)", "Click: deselect this tube (launch mode switch to Off)"); } }
        public static string TubeClickNeedsFlood { get { return T("Klicken: wählbar, sobald das Rohr geflutet ist", "Click: selectable once the tube is flooded"); } }
        public static string TubeDoubleFlood { get { return T("Doppelklick: Rohr fluten", "Double-click: flood the tube"); } }
        public static string TubeDoubleDrain { get { return T("Doppelklick: Rohr lenzen", "Double-click: drain the tube"); } }
        public static string TubeCtrlLaunch { get { return T("Strg+Klick: mit dem Abfeuerhebel des Rohrs ausstoßen", "Ctrl+click: launch with the tube's own launch lever"); } }
        public static string TubeRightMenu { get { return T("Rechtsklick: Aktionen", "Right-click: actions"); } }

        // tube actions menu (right-click on a tube)
        public static string MenuFlood { get { return T("Fluten", "Flood"); } }
        public static string MenuDrain { get { return T("Lenzen", "Drain"); } }
        public static string MenuSelect { get { return T("Wählen", "Select"); } }
        public static string MenuLaunch { get { return T("Ausstoßen", "Launch"); } }
        public static string MenuDoorOpen { get { return T("Mündungsklappe auf", "Open outer door"); } }
        public static string MenuDoorClose { get { return T("Mündungsklappe zu", "Shut outer door"); } }
        public static string TipMenuDoorOpen
        {
            get
            {
                return T("Die Mündungsklappe öffnen (3 s). Nur bei geflutetem Rohr. Gefeuert wird nur mit ganz geöffneter Klappe.",
                         "Open the outer door (3 s). Flooded tubes only. The tube fires only with its door fully open.");
            }
        }
        public static string TipMenuDoorClose
        {
            get
            {
                return T("Die Mündungsklappe schließen (3 s). Erst dann kann das Rohr gelenzt werden.",
                         "Shut the outer door (3 s). Only then can the tube be drained.");
            }
        }
        public static string TipMenuFlood { get { return T("Das Rohr fluten (wie ein Doppelklick).", "Flood the tube (as a double-click does)."); } }
        public static string TipMenuDrain { get { return T("Das Rohr lenzen (wie ein Doppelklick).", "Drain the tube (as a double-click does)."); } }
        public static string TipMenuSelect
        {
            get
            {
                return T("Das Rohr für einen Einzelschuss wählen oder abwählen (wie ein Klick). Nur geflutete Rohre, nicht während die Abfeueranlage unter Strom steht.",
                         "Select or deselect the tube for a single shot (as a click does). Flooded tubes only, not while the firing circuit is live.");
            }
        }
        public static string TipMenuLaunch
        {
            get
            {
                return T("Den Abfeuerhebel des Rohrs im Torpedoraum ziehen (auch Strg+Klick): der Torpedo läuft mit dem an der Rohrgruppe eingestellten Schusswinkel, ohne die Abfeueranlage. Nur bei geflutetem Rohr mit geschlossenem Rohrverschluss und ganz geöffneter Mündungsklappe.",
                         "Pull the tube's own launch lever in the torpedo room (also Ctrl+click): the torpedo runs on the gyro angle set on its tube group, without the bridge firing circuit. Only with the tube flooded, its breech door shut and its outer door fully open.");
            }
        }
        public static string TorpDepth { get { return T("TIEFE", "DEPTH"); } }
        public static string TorpSpeed { get { return T("FAHRSTUFE", "SPEED"); } }
        public static string TorpPistol { get { return T("PISTOLE", "PISTOL"); } }
        public static string SpeedSlow { get { return T("L", "S"); } }
        public static string SpeedMedium { get { return "M"; } }
        public static string SpeedFast { get { return T("S", "F"); } }
        public static string PistolImpactShort { get { return T("AZ", "AZ"); } }
        public static string PistolMagneticShort { get { return T("MZ", "MZ"); } }
        public static string PistolImpact { get { return T("Aufschlag (AZ)", "AZ"); } }
        public static string PistolMagnetic { get { return T("Magnet (MZ)", "MZ"); } }

        public static string TipWinTorpedo
        {
            get
            {
                return T("<b>Torpedoeinstellung</b>\nDas Fenster mit Tiefe, Fahrstufe und Pistole ein- oder ausblenden. Es erscheint nur, solange ein angehaktes Rohr einen Torpedo hat.",
                         "<b>Torpedo settings</b>\nShow or hide the window with depth, speed and pistol. It only appears while a checked tube holds a torpedo.");
            }
        }

        public static string TipTorpDepth
        {
            get
            {
                return T("<b>Tiefe</b>\nLauftiefe des Torpedos, 1 bis 14 m (rot: nicht einstellbar). Gilt für den nächsten Schuss, wie der Tiefenregler im Spiel. Scheibe drehen, auf der Skala anklicken oder Mausrad; in Schritten von 0,25 m." + TypeUsageDe + TorpRoomDe,
                         "<b>Depth</b>\nThe torpedo's running depth, 1 to 14 m (red: can't be set). Applies to the next shot, like the game's depth control. Turn the dial, click the scale or use the mouse wheel; in 0.25 m steps." + TypeUsageEn + TorpRoomEn);
            }
        }

        public static string TipTorpSpeed
        {
            get
            {
                return T("<b>Fahrstufe</b>\nLangsam / Mittel / Schnell, nur die Stufen, die alle angehakten Torpedos haben. Darunter die Geschwindigkeit in Knoten. Wird auch an den Vorhaltrechner gegeben." + TorpRoomDe,
                         "<b>Speed</b>\nSlow / medium / fast, only the speeds all checked torpedoes have. The readout shows the speed in knots. Also passed to the TDC." + TorpRoomEn);
            }
        }

        public static string TipTorpPistol
        {
            get
            {
                return T("<b>Pistole</b>\nAufschlagzündung (AZ) oder Magnetzündung (MZ) für die Torpedos in den angehakten Rohren." + TorpRoomDe,
                         "<b>Pistol</b>\nAZ (impact) or MZ (magnetic) pistol for the torpedoes in the checked tubes." + TorpRoomEn);
            }
        }

        // added to the torpedo tips when the Torpedo Room mod is installed
        static string TorpRoomDe { get { return TorpedoRoomLink.Present ? "\n<i>Torpedo Room: wird für jeden angehakten Torpedo einzeln gespeichert.</i>" : ""; } }
        static string TorpRoomEn { get { return TorpedoRoomLink.Present ? "\n<i>Torpedo Room: stored on each checked torpedo.</i>" : ""; } }
        // ---- fire-control switch box ----
        public static string SwitchSingle { get { return T("Einzelschuß", "Launch mode"); } }
        public static string SwitchSalvo { get { return T("Fächerschuß", "Salvo mode"); } }
        public static string SwitchLever { get { return T("T.Abf. Schalter", "Firing station"); } }
        public static string TubeWord { get { return T("Rohr", "Tube"); } }
        public static string SpreadCourse { get { return T("Fächer auf geradem Kurs", "Salvo on straight course"); } }
        public static string SpreadTurning { get { return T("Fächer im Drehen", "Salvo in a turn"); } }
        public static string SpreadCourseShort { get { return T("Fächer gerade", "Salvo on straight course"); } }
        public static string SpreadPlain { get { return T("Fächer", "Salvo"); } } // Type II: its only salvo position
        public static string SpreadTurningShort { get { return T("Fächer Drehen", "Salvo in a turn"); } }
        public static string SwitchOff { get { return T("Aus", "Off"); } }
        public static string LeverBridge { get { return T("Brücke", "Bridge"); } }
        public static string LeverTower { get { return T("Turm", "Tower"); } }

        const string SwitchUsageDe = "\nBeschriftung anklicken, um dorthin zu schalten; Griff anklicken (links = weiter, rechts = zurück), Mausrad oder Ziehen zum Weiterschalten. Wie der Schalter am Schaltkasten T-Abf.-Anl.; es werden nur die Stellungen deines Bootes angeboten.";
        const string SwitchUsageEn = "\nClick a label to switch there; click the handle (left = on, right = back), use the mouse wheel or drag to turn it. Works like the knob on the fire-control switch box; only your boat's positions are offered.";

        public static string TipSwitchSingle
        {
            get
            {
                return T("<b>Einzelschuß</b>\nWählt das Rohr für einen Einzelschuß oder einen Fächer (auf geradem Kurs / im Drehen). Setzt die Rohrauswahl wie der Schalter im Boot." + SwitchUsageDe,
                         "<b>Launch mode</b>\nSelects the tube for a single shot, or a salvo on a straight course or in a turn. Sets the tube selection like the knob in the boat." + SwitchUsageEn);
            }
        }

        public static string TipSwitchSalvo
        {
            get
            {
                return T("<b>Fächerschuß</b>\nWählt die Rohre, die zusammen als Fächer geschossen werden (Aus = keiner)." + SwitchUsageDe,
                         "<b>Salvo mode</b>\nSelects which tubes fire together as a salvo (Off = none)." + SwitchUsageEn);
            }
        }

        public static string TipFiringLampOn
        {
            get
            {
                return T("<b>Abfeuerkreis</b>\nSchalter können nicht betätigt werden, solange der Abfeuerkreis unter Spannung steht.",
                         "<b>Firing circuit</b>\nCannot use switches while firing circuit is energized.");
            }
        }

        public static string TipFiringLampOff
        {
            get
            {
                return T("<b>Abfeuerkreis</b>\nSchalter können gefahrlos betätigt werden.",
                         "<b>Firing circuit</b>\nSwitches can be safely changed.");
            }
        }

        public static string TipSwitchLever
        {
            get
            {
                return T("<b>T.Abf. Schalter</b>\nTorpedo-Abfeuerungsschalter: von wo abgefeuert wird – Brücke (UZO) oder Turm (Angriffssehrohr) – oder Aus." + SwitchUsageDe,
                         "<b>Firing station</b>\nTorpedo firing switch: where torpedoes are fired from – bridge (UZO) or tower (attack periscope) – or off." + SwitchUsageEn);
            }
        }

        // ---- TDC dial window ----
        public static string DialAob { get { return T("LAGENWINKEL", "AOB"); } }
        public static string AobDegrees { get { return T("Grad", "Degrees"); } }
        public static string AobBowLeft { get { return T("Bug Links", "Port Bow"); } }
        public static string AobBowRight { get { return T("Bug Rechts", "Stbd Bow"); } }
        public static string DialSpeed { get { return T("GEGNERFAHRT", "TARGET SPEED"); } }
        public static string DialDistance { get { return T("ENTFERNUNG", "TARGET DISTANCE"); } }
        public static string DialImpactTime { get { return T("LAUFZEIT", "TIME TO IMPACT"); } }
        public static string DialLength { get { return T("STREUBREITE", "TARGET LENGTH"); } }

        // ---- typing a value into a dial's readout ----
        public static string TypeValuePlaceholder { get { return T("Wert eintippen", "type a value"); } }
        /// <summary>The readout's pop-up; {0} = what may be typed.</summary>
        public static string TipTypeValue
        {
            get
            {
                return T("<b>Wert eintippen</b>\nKlicken und {0} eintippen.\nEnter = einstellen, Esc = abbrechen. Werte außerhalb des Bereichs werden abgewiesen; die Scheibe bleibt dann, wie sie war.",
                         "<b>Type a value</b>\nClick and type {0}.\nEnter = set, Esc = cancel. A value out of range is refused, and the dial stays as it was.");
            }
        }
        public static string RangeAob { get { return T("-180..180 (+ Stb, - Bb)", "-180..180 (+ stbd, - port)"); } }
        public static string TypeAob { get { return T("einen Lagewinkel von -180 bis 180: positiv = Steuerbord, negativ = Backbord (30 = Stb 030°, -30 = Bb 030°)", "an angle on bow from -180 to 180: positive = starboard, negative = port (30 = Stbd 030°, -30 = Port 030°)"); } }
        public static string RangeSpeed { get { return T("0..55 kn", "0..55 kn"); } }
        public static string TypeSpeed { get { return T("eine Gegnerfahrt von 0 bis 55 kn", "a target speed from 0 to 55 kn"); } }
        public static string RangeDistance { get { return T("3..100 hm", "3..100 hm"); } }
        public static string TypeDistance { get { return T("eine Entfernung von 300 bis 10000 m: 4500, 45 hm oder 4,5 km (eine Zahl ohne Einheit unter 300 gilt als hm)", "a distance from 300 to 10000 m: 4500, 45 hm or 4.5 km (a bare number under 300 is hm)"); } }
        public static string RangeAngle360 { get { return T("0..360°", "0..360°"); } }
        public static string TypeGyro { get { return T("einen Schusswinkel von 0 bis 360°", "a gyro angle from 0 to 360°"); } }
        public static string TypeBearing { get { return T("eine Zielpeilung von 0 bis 360°", "a target bearing from 0 to 360°"); } }
        public static string RangeSpread { get { return T("0..20°", "0..20°"); } }
        public static string TypeSpread { get { return T("einen Streuwinkel von 0 bis 20°", "a spread angle from 0 to 20°"); } }
        public static string RangeLength { get { return T("100..200 m", "100..200 m"); } }
        public static string TypeLength { get { return T("eine Streubreite (Ziellänge) von 100 bis 200 m", "a target length from 100 to 200 m"); } }
        public static string RangeDepth { get { return T("1..14 m", "1..14 m"); } }
        public static string TypeDepth { get { return T("eine Lauftiefe von 1 bis 14 m (auf Viertelmeter gerundet)", "a running depth from 1 to 14 m (rounded to quarter metres)"); } }

        public static string TipDialLength
        {
            get
            {
                return T("<b>Streubreite</b>\nLänge des Ziels (100 bis 200 m), wie die Streubreite-Scheibe am Vorhaltrechner. Daraus und aus der Entfernung errechnet der Rechner den Streuwinkel für einen Fächer. Mausrad: 1 m." + DialUsageDe + TypeUsageDe,
                         "<b>Target length</b>\nThe target's length (100 to 200 m), as on the TDC's target length (Streubreite) dial. With the distance, the TDC works out the spread angle for a salvo from it. Mouse wheel: 1 m." + DialUsageEn + TypeUsageEn);
            }
        }

        public static string DialSpreadAngle { get { return T("STREUWINKEL", "SPREAD ANGLE"); } }
        public static string SpreadUnit { get { return "Grad"; } }

        public static string TipDialSpreadAngle
        {
            get
            {
                return T("<b>Streuwinkel</b>\nMessingzeiger: der Fächerwinkel, den der Rechner aus Streubreite und Entfernung errechnet (0 bis 20 Grad). Rote Fahne: der eingestellte Winkel, der an die Rohre geht; die Anzeige darunter.\nScheibe drehen, auf der Skala anklicken oder Mausrad (0,5 Grad), um die Fahne zu stellen. Bei eingeschaltetem Streuwinkelmotor fährt der Rechner die Fahne selbst auf den errechneten Winkel." + TypeUsageDe,
                         "<b>Spread angle</b>\nBrass needle: the salvo spread angle the TDC works out from the target length and distance (0 to 20 degrees). Red flag: the angle set, which goes to the tubes; the readout shows it.\nTurn the dial, click the scale or use the mouse wheel (0.5°) to move the flag. With the spread motor on, the TDC drives the flag to the computed angle itself." + TypeUsageEn);
            }
        }

        public static string SwitchOn { get { return T("Ein", "On"); } }
        public static string SpreadMotor { get { return T("STREUMOTOR", "SPREAD MOTOR"); } }
        public static string AobMotor { get { return T("LAGEMOTOR", "AOB MOTOR"); } }

        public static string TipAobMotor
        {
            get
            {
                return T("<b>Lagenwinkelmotor</b>\nKlicken zum Umlegen.\nEin: Der Rechner führt die Lage nach, wenn sich Peilung oder eigener Kurs ändern (Lagenwinkel-Nachführung).\nAus: Die Lage bleibt stehen, wie eingestellt. Die Lage-Scheibe ändert die Schalterstellung nicht.",
                         "<b>AOB motor</b>\nClick to flip.\nOn: the TDC keeps the AOB in step as the bearing or your own course changes (AOB tracking).\nOff: the AOB stays as set. The AOB dial doesn't change the switch.");
            }
        }

        public static string TipSpreadMotor
        {
            get
            {
                return T("<b>Streuwinkelmotor</b>\nKlicken zum Umlegen.\nEin: Der Rechner fährt die rote Fahne auf den errechneten Streuwinkel; eingestellt wird über die Streubreite.\nAus: Die Fahne bleibt, wo du sie mit der Streuwinkel-Scheibe hinstellst.",
                         "<b>Spread motor</b>\nClick to flip.\nOn: the TDC drives the red flag to the computed spread angle; set it with the target length.\nOff: the flag stays where you put it with the spread angle dial.");
            }
        }

        public static string WinSpread { get { return T("Fächer", "Salvo spread"); } }

        public static string TipWinSpread
        {
            get
            {
                return T("<b>Fächer</b>\nAngehakt: Das Fenster mit Streubreite und Streuwinkel ist immer zu sehen.\nNicht angehakt: Es erscheint nur, solange der Einzelschuß-Schalter auf Fächer steht und \"Fächereinstellung automatisch anzeigen\" an ist.",
                         "<b>Salvo spread</b>\nChecked: the window with the target length and spread angle is always shown.\nUnchecked: it appears only while the launch mode switch is on a salvo / spread position and \"Auto display spread settings\" is on.");
            }
        }

        public static string HotkeyPrefix { get { return T("Tastenkürzel: ", "Hotkey: "); } }
        public static string HotkeyNone { get { return T("keins", "none"); } }
        public static string HotkeyPress { get { return T("Taste drücken ... (Esc = abbrechen)", "Press a key ... (Esc = cancel)"); } }
        public static string TipHotkey
        {
            get
            {
                return T("<b>Tastenkürzel</b>\nBlendet die Mod-Anzeige ein und aus, wie der Knopf in der Seitenleiste.\nKlicken, dann die gewünschte Taste drücken. Rücktaste / Entf = kein Tastenkürzel, Esc = abbrechen.",
                         "<b>Hotkey</b>\nShows and hides the mod's display, like the sidebar button.\nClick, then press the key you want. Backspace / Delete = no hotkey, Esc = cancel.");
            }
        }

        public static string OptKeepScopeBearing { get { return T("Sehrohrrichtung beibehalten", "Keep my scope bearing"); } }
        public static string OptTrueOnBinoculars { get { return T("Rechtw. Peilung im Fernglas", "Show True Bearing on binoculars"); } }
        public static string TipOptTrueOnBinoculars
        {
            get
            {
                return T("<b>Rechtweisende Peilung im Fernglas</b>\nIm Fernglas der Brücke sind alle Fenster dieser Mod ausgeblendet; eingeschaltet zeigt es dort trotzdem das Zählwerk der rechtweisenden Peilung (wohin das Glas blickt, 000–359°), an der Stelle des Schusswinkel-Zählwerks.",
                         "<b>Show True Bearing on binoculars</b>\nOn the bridge binoculars every window of this mod is hidden; with this on, the TRUE bearing counter still shows there (where the binoculars are looking, 000–359°), in the gyro angle counter's place.");
            }
        }

        public static string OptHideStockElevator { get { return T("Sehrohr-Höhenanzeige ausblenden", "Hide periscope raise/lower control"); } }
        public static string TipOptHideStockElevator
        {
            get
            {
                return T("<b>Sehrohr-Höhenanzeige ausblenden</b>\nBlendet die Aus-/Einfahr-Anzeige des Spiels und die Knöpfe für ganz aus- und ganz einfahren aus. Mit den Tasten lässt sich das Sehrohr weiter aus- und einfahren.",
                         "<b>Hide periscope raise/lower control</b>\nHides the game's raise/lower control and the raise fully / lower fully buttons. The keys still raise and lower the periscope.");
            }
        }

        public static string TipOptKeepScopeBearing
        {
            get
            {
                return T("<b>Sehrohrrichtung beibehalten</b>\nAn: Ein Sehrohr bleibt in der Richtung, in der du es verlassen hast, auch wenn sich sein Ziel ändert (anderer Kontakt gewählt, von der Besatzung erfasst). Ein Klick auf ein Schiff oder einen Kontakt in einer anderen Ansicht dreht es nicht herum; am Sehrohr selbst schon.\nAus: wie im Spiel.",
                         "<b>Keep my scope bearing</b>\nOn: a periscope stays pointed where you left it when its target changes (another contact selected, or picked by the crew), and clicking a ship or contact in another view doesn't swing it round; at the scope itself it still does.\nOff: as in the base game.");
            }
        }

        public static string OptTintPanels { get { return T("Fenster nach Bootsbeleuchtung tönen", "Tint panels with sub lighting"); } }

        public static string TipOptTintPanels
        {
            get
            {
                return T("<b>Fenster nach Bootsbeleuchtung tönen</b>\nAn: Die hellen Fenster (Rechnerscheiben, Schalter, Hebel ...) nehmen die Farbe der Innenbeleuchtung an: rötlich unter Rotlicht, bläulich unter Blaulicht.\nAus: Sie bleiben immer wie unter normalem Licht.",
                         "<b>Tint panels with sub lighting</b>\nOn: the off-white windows (TDC dials, switches, lever ...) take on the colour of the boat's interior lights: reddish under the red lights, bluish under the blue.\nOff: they always look as under the normal lights.");
            }
        }

        public static string OptAutoSpread { get { return T("Fächereinstellung automatisch anzeigen", "Auto display spread settings"); } }

        public static string TipOptAutoSpread
        {
            get
            {
                return T("<b>Fächereinstellung automatisch anzeigen</b>\nAn: Das Fächer-Fenster erscheint, sobald der Einzelschuß-Schalter auf \"Fächer Kurs\" oder \"Fächer Drehen\" gestellt wird, und verschwindet bei jeder anderen Stellung. Bei \"Fächer Drehen\" erscheint auch das Drehgeschwindigkeits-Fenster.\nAus: Das Fächer-Fenster erscheint nur, wenn sein Häkchen unter FENSTER gesetzt ist (dann immer).",
                         "<b>Auto display spread settings</b>\nOn: the salvo spread window pops up when the launch mode switch is set to salvo on straight course or salvo in a turn, and hides at any other position. On salvo in a turn the angular speed window pops up too.\nOff: the salvo spread window appears only while its check box under WINDOWS is checked (then always).");
            }
        }
        public static string UnitKnots { get { return T("sm/Std", "kn"); } }
        public static string Port { get { return T("Bb", "P"); } }
        public static string Starboard { get { return T("Stb", "S"); } }
        // printed on the TDC's angular speed knob (German on the real one)
        public static string PortLong { get { return "Bb"; } }
        public static string StarboardLong { get { return "Stb"; } }

        public static string DialTurnRate { get { return T("DREHGESCHW.", "OWN ANGULAR SPEED"); } }

        public static string TipDialTurnRate
        {
            get
            {
                return T("<b>Eigene Drehgeschwindigkeit</b>\nWie schnell das eigene Boot dreht (0 bis 3 Grad je Sekunde nach Bb oder Stb), wie der Drehgeschwindigkeits-Knopf am Vorhaltrechner. Der Rechner hält den Schusswinkel damit vor, wenn im Drehen geschossen wird. Mausrad: 0,1 °/s." + DialUsageDe,
                         "<b>Own angular speed</b>\nHow fast your own boat is turning (0 to 3 degrees per second to port or starboard), as on the TDC's angular speed knob. The TDC leads the gyro angle by it when firing while turning. Mouse wheel: 0.1 °/s." + DialUsageEn);
            }
        }

        public static string WinTurnRate { get { return T("Drehgeschwindigkeit", "Angular speed"); } }
        public static string TurnRateUnavailable { get { return T("\n<i>Der Vorhaltrechner dieses Boots hat keinen Drehgeschwindigkeits-Knopf.</i>", "\n<i>This boat's TDC has no angular speed knob.</i>"); } }

        public static string TipWinTurnRate
        {
            get
            {
                return T("<b>Drehgeschwindigkeit</b>\nAngehakt: ein eigenes Fenster neben den Rechnerscheiben mit einem Knopf für die eigene Drehgeschwindigkeit (-3 bis 3 Grad je Sekunde, Bb/Stb), die der Rechner beim Schuss im Drehen einrechnet.\nNicht angehakt: Fenster ausgeblendet (der eingestellte Wert bleibt im Rechner), außer bei \"Fächer Drehen\" mit \"Fächereinstellung automatisch anzeigen\".",
                         "<b>Angular speed</b>\nChecked: a window beside the TDC dials with an own angular speed knob (-3 to 3 degrees per second, port/starboard), which the TDC allows for when firing while turning.\nUnchecked: window hidden (the value set stays in the TDC), except on salvo in a turn with \"Auto display spread settings\" on.");
            }
        }

        public static string AnchorToDials { get { return T("den Rechnerscheiben", "the TDC dials"); } }
        public static string AnchorToDirections { get { return T("der Richtungswahl", "the direction selector"); } }
        public static string TipAnchorOn { get { return T("<b>Verankert</b>\nDas Fenster folgt {0}. Ziehen verschiebt es gegen sie.\nKlicken: lösen (es bleibt, wo es ist).", "<b>Anchored</b>\nThis window follows {0}. Dragging moves it relative to them.\nClick: detach (it stays where it is)."); } }
        public static string TipAnchorOff { get { return T("<b>Nicht verankert</b>\nDas Fenster steht frei.\nKlicken: an {0} verankern (es bleibt, wo es ist, und folgt dann).", "<b>Not anchored</b>\nThis window stands on its own.\nClick: anchor it to {0} (it stays where it is, then follows them)."); } }

        const string DialUsageDe = "\nZum Einstellen die Scheibe greifen und um ihre Mitte drehen, wie am Rechner, oder einen Wert auf der Skala anklicken. Umschalt = 10x feiner, Mausrad = in Schritten.";
        const string DialUsageEn = "\nGrab the dial and turn it around its centre, like on the TDC, or click a value on the scale. Shift = 10x finer, mouse wheel = steps.";
        const string TypeUsageDe = "\nOder das Wertfeld darunter anklicken und den Wert eintippen.";
        const string TypeUsageEn = "\nOr click the value box below it and type the value.";

        public static string TipDialAob
        {
            get
            {
                return T("<b>Lage</b>\nLagewinkel des Ziels, wie die Lage-Scheibe am Vorhaltrechner: 0 = Bug auf uns zu, 180 = Heck. Steuerbordhälfte grün, Backbordhälfte rot; im Fenster unten die Einer (grün Steuerbord, rot Backbord). Der Rechner folgt der Scheibe sofort beim Drehen. Gesperrt, solange die Sperre ausgelöst ist. Mausrad: 1°." + DialUsageDe + TypeUsageDe,
                         "<b>AOB</b>\nThe target's angle on the bow, as on the TDC's AOB dial: 0 = bow on, 180 = stern on. Starboard half green, port half red; the window at the bottom shows the ones digit (green starboard, red port). The TDC follows the dial as you turn it. Locked while the locking mechanism is triggered. Mouse wheel: 1°." + DialUsageEn + TypeUsageEn);
            }
        }

        public static string TipDialSpeed
        {
            get
            {
                return T("<b>Gegnerfahrt</b>\nFahrt des Ziels in Knoten (0 bis 55), wie die Gegnerfahrt-Scheibe am Vorhaltrechner. Mausrad: 0,5 kn." + DialUsageDe + TypeUsageDe,
                         "<b>Target speed</b>\nThe target's speed in knots (0 to 55), as on the TDC's speed dial. Mouse wheel: 0.5 kn." + DialUsageEn + TypeUsageEn);
            }
        }

        public static string TipDialImpactTime
        {
            get
            {
                return T("<b>Laufzeit</b>\nSekunden vom Schuss bis zum Treffpunkt, aus den Werten des Vorhaltrechners: Entfernung, Lage, Zielfahrt, Peilung, eingestellte Torpedofahrt und Schusswinkel (der Torpedo läuft erst 9,5 m geradeaus und dreht dann mit 95 m Radius auf seinen Kurs). Gerechnet mit der Fahrt der gewählten Torpedos (bei einem Fächer mit der langsamsten).\nOhne gewähltes Rohr bleibt die letzte Laufzeit stehen.",
                         "<b>Time to impact</b>\nSeconds from launch to the aim point, worked out from the TDC's entries: distance, AOB, target speed, bearing, the torpedo speed it is set for and the gyro angle (the torpedo runs 9.5 m straight, then turns onto its course on a 95 m radius). Timed at the selected torpedoes' speed (the slowest, for a salvo).\nWith no tube selected the last time stays shown.");
            }
        }

        public static string TipDialDistance
        {
            get
            {
                return T("<b>Entfernung</b>\nEntfernung zum Ziel (3 bis 100 hm, also 300 m bis 10 km), wie die Entfernungs-Scheibe am Vorhaltrechner. Mausrad: 100 m." + DialUsageDe + TypeUsageDe,
                         "<b>Target distance</b>\nRange to the target (3 to 100 hm, i.e. 300 m to 10 km), as on the TDC's range dial. Mouse wheel: 100 m." + DialUsageEn + TypeUsageEn);
            }
        }

        public static string TipSettings
        {
            get
            {
                return T("<b>Einstellungen</b>\nWeitere Optionen ein- oder ausklappen. Änderungen werden sofort gespeichert und gelten auch in späteren Spielsitzungen.",
                         "<b>Settings</b>\nShow or hide more options. Changes are saved immediately and carry over to later game sessions.");
            }
        }

        public static string TipOptShowReceiver
        {
            get
            {
                return T("<b>Empfängerwinkel anzeigen</b>\nZeigt unter dem TDC-Zähler den Winkel, auf den die Kreisel der gewählten Rohrgruppe eingestellt sind.",
                         "<b>Show gyro receiver angles</b>\nShows the angle the selected tube group's gyros are set to, under the TDC counter.");
            }
        }

        public static string TipOptRealisticTdc
        {
            get
            {
                return T("<b>Realistischer Rechner-Nachlauf</b>\nAn: Die Motoren des Vorhaltrechners führen Peilung, Auftreffwinkel und Parallaxe in Echtzeit nach.\nAus: Die Rechnermotoren laufen 10× schneller.",
                         "<b>Realistic TDC follower</b>\nOn: the TDC's motors drive bearing, impact angle and parallax to the new values at their real speed.\nOff: the TDC's motors run 10× faster.");
            }
        }

        public static string TipOptRealisticSync
        {
            get
            {
                return T("<b>Realistischer Empfängerabgleich</b>\nAn: Die Kreisel der Rohre laufen mit ihrem Stellmotor in Echtzeit dem Rechnerwert nach (Lampe rot, dann grün).\nAus: Die Stellmotoren laufen 10× schneller. Manuelle Kreiseleinstellung an den Rohren bleibt unberührt.",
                         "<b>Realistic receiver sync</b>\nOn: the tube gyros turn to the TDC value with their setting motor at real speed (lamp red, then green).\nOff: the setting motors run 10× faster. Manual gyro setting at the tubes is not affected.");
            }
        }

        // ---- pop-up descriptions ----
        public static string TipReceiverCounter
        {
            get
            {
                string dbl = GyroConfig.ActiveReceiverOnly
                    ? T("\n<i>Doppelklick: zum anderen Empfänger wechseln und das Sehrohr so drehen, dass der TDC-Winkel dessen eingestelltem Winkel entspricht.</i>",
                        "\n<i>Double-click: switch to the other receiver and turn the scope so the TDC angle matches the angle its gyros are set to.</i>")
                    : T("\n<i>Doppelklick: diesen Empfänger wählen und das Sehrohr so drehen, dass der TDC-Winkel diesem Winkel entspricht.</i>",
                        "\n<i>Double-click: select this receiver and turn the scope so the TDC angle matches this angle.</i>");
                return T("<b>Schusswinkel der Rohre</b>\nWinkel, auf den die Kreisel dieser Rohrgruppe (Bug oder Heck) tatsächlich eingestellt sind. Die empfangende Gruppe läuft dem TDC-Wert nach, bis die Lampe grün ist.",
                         "<b>Tube gyro angle</b>\nThe angle this tube group's (bow or stern) gyros are actually set to. The receiving group catches up with the TDC value until the lamp turns green.")
                       + dbl
                      ;
            }
        }

        public static string TipCounter
        {
            get
            {
                string range = GyroConfig.LeftRight
                    ? T("L/R 0-180° (L = Backbord, R = Steuerbord).", "L/R 0-180° (L = port, R = starboard).")
                    : T("0-359°, im Uhrzeigersinn ab Bug voraus.", "0-359°, clockwise from dead ahead.");
                return T("<b>Schusswinkel (TDC)</b>\nKreiselwinkel, den der Vorhaltrechner für den Torpedo berechnet hat. ",
                         "<b>Gyro angle (TDC)</b>\nThe torpedo gyro angle computed by the TDC. ")
                       + range
                       + T(" Ausgegraut, solange niemand am Rechner sitzt; die übrigen Zähler sind dann ausgeblendet.",
                           " Greyed out while nobody is at the TDC; the other counters are hidden then.")
                      ;
            }
        }

        public static string TipSyncLamp
        {
            get
            {
                return T("<b>Schusswinkel eingestellt</b>\n<color=#6f6>Grün</color>: die Kreisel dieser Rohrgruppe stehen auf dem Rechnerwert.\n<color=#f66>Rot</color>: wird synchronisiert.\nNur an der Zeile des gewählten Empfängers.",
                         "<b>Gyro set</b>\n<color=#6f6>Green</color>: this tube group's gyros match the TDC angle.\n<color=#f66>Red</color>: synchronizing.\nShown on the selected receiver's row only.");
            }
        }

        public static string TipFollowLamp
        {
            get
            {
                return T("<b>Zielpeilung</b>\n<color=#6af>Blau</color>: der Rechner folgt der Peilung NICHT.\nDunkel: er folgt.\nAnklicken zum Umschalten (Folgen / Nicht folgen).",
                         "<b>Target bearing</b>\n<color=#6af>Blue</color>: the TDC is NOT following the bearing.\nDark: it is following.\nClick to switch (Follow / Not follow).");
            }
        }

        public static string TipBearingHeader
        {
            get
            {
                return T("<b>Zielpeilung</b>\nOb der Vorhaltrechner der Peilung dieses Sehrohrs bzw. der UZO folgt. Die Lampe leuchtet <color=#6af>blau</color>, solange er NICHT folgt, und ist dunkel, solange er folgt.",
                         "<b>Target bearing</b>\nWhether the TDC follows this periscope's or the UZO's bearing. The lamp is lit <color=#6af>blue</color> while it is NOT following, and dark while it is following.");
            }
        }

        public static string TipFollow
        {
            get
            {
                return T("<b>Folgen</b>\nDer Rechner übernimmt laufend die Peilung dieses Sehrohrs bzw. der UZO.",
                         "<b>Following</b>\nThe TDC continuously takes the bearing from this periscope or the UZO.");
            }
        }

        public static string TipNoFollow
        {
            get
            {
                return T("<b>Nicht folgen</b>\nDie Peilung im Rechner wird festgehalten. Gilt, bis du \"Folgen\" drückst oder ein neues Ziel aufschaltest.",
                         "<b>Not following</b>\nThe bearing in the TDC is held. Stays in effect until you press \"Following\" or lock a new target.");
            }
        }

        public static string TipReceiverHeader
        {
            get
            {
                return T("<b>Schusswinkelempfänger</b>\nLegt fest, welche Rohrgruppe den Schusswinkel vom Vorhaltrechner erhält.", "<b>Gyro angle receiver</b>\nSelects which tube group receives the gyro angle from the TDC.")
                       + (GyroConfig.AutoReceiver
                           ? T(" Folgt automatisch den angehakten, gefluteten Rohren (keines = Aus); eine Auswahl hier hat Vorrang, bis du die Rohrauswahl änderst.",
                               " Follows the checked, flooded tubes automatically (none = Off); a pick here takes priority until you change which tubes are checked.")
                           : T(" Automatik aus: nur du stellst ihn.", " Auto set receiver is off: only you set it."))
                      ;
            }
        }

        public static string TipOff
        {
            get { return T("<b>Aus</b>\nKeine Rohrgruppe erhält den Schusswinkel.", "<b>Off</b>\nNo tube group receives the gyro angle."); }
        }

        public static string TipBow
        {
            get { return T("<b>Bug</b>\nDie Bugrohre erhalten den Schusswinkel.", "<b>Bow</b>\nThe bow tubes receive the gyro angle."); }
        }

        public static string TipStern
        {
            get
            {
                return T("<b>Heck</b>\nDie Heckrohre erhalten den Schusswinkel. Gesperrt auf Booten ohne Heckrohr.",
                         "<b>Stern</b>\nThe stern tubes receive the gyro angle. Unavailable on boats without a stern tube.");
            }
        }
    }
}
