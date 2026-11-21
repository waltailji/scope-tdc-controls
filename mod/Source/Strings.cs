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
        public static string TdcNotManned { get { return T("Rechner nicht besetzt", "TDC not manned"); } }
        public static string ReceiverHeader { get { return T("SCHUSSWINKELEMPFÄNGER", "GYRO ANGLE RECEIVER"); } }
        public static string Off { get { return T("AUS", "OFF"); } }
        public static string Bow { get { return T("BUG", "BOW"); } }
        public static string Stern { get { return T("HECK", "STERN"); } }

        public static string TdcLabel { get { return "TDC"; } }
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
                return T("<b>Peilungsgeber</b>\nLegt fest, welches Sehrohr seine Peilung an den Vorhaltrechner überträgt. Normalerweise wählt das Spiel das Sehrohr, durch das du gerade schaust. Eine Wahl hier hat Vorrang, bis ein neues Ziel aufgeschaltet wird.\n<i>Strg + Ziehen zum Verschieben.</i>",
                         "<b>Bearing source</b>\nSelects which scope sends its bearing to the TDC. Normally the game picks the scope you are looking through. A pick here takes priority until a new target is locked.\n<i>Ctrl + drag to move.</i>");
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
                return T("<b>Parallaxe</b>\nRichtung der Parallaxkorrektur im Vorhaltrechner: vorn für die Bugrohre, achtern für die Heckrohre.\n<i>Strg + Ziehen zum Verschieben.</i>",
                         "<b>Parallax</b>\nDirection of the TDC's parallax correction: forward for the bow tubes, backward for the stern tubes.\n<i>Ctrl + drag to move.</i>") + ParallaxAutoNote;
            }
        }

        public static string TipParallaxCounter
        {
            get
            {
                return T("<b>Parallaxe (TDC)</b>\nKorrektur, die der Vorhaltrechner zum Schusswinkel addiert, weil Sehrohr und Torpedorohre nicht am selben Ort sind. Die Richtung (vorn/achtern) wählst du im Seitenfeld.\n<i>Strg + Ziehen zum Verschieben.</i>",
                         "<b>Parallax (TDC)</b>\nThe correction the TDC adds to the gyro angle, because the periscope and the torpedo tubes are not in the same place. Set the direction (forward/backward) in the side panel.\n<i>Ctrl + drag to move.</i>");
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
        public static string OptShowReceiver { get { return T("Rohrwinkel anzeigen", "Show receiver angle"); } }
        public static string OptActiveReceiverOnly { get { return T("Nur aktiven Empfänger zeigen", "Show active receivers only"); } }
        public static string OptShowParallax { get { return T("Parallaxe anzeigen", "Show parallax"); } }

        public static string TipOptActiveReceiverOnly
        {
            get
            {
                return T("<b>Nur aktiven Empfänger zeigen</b>\nAn: nur der Winkel der gewählten Rohrgruppe, ausgeblendet bei Empfänger aus.\nAus: Bug- und Heckwinkel werden immer gezeigt, auch bei Empfänger aus. Der aktive hat die helle Beschriftung.",
                         "<b>Show active receivers only</b>\nOn: only the selected tube group's angle, hidden while the receiver is off.\nOff: the bow and stern angles are always shown, even while the receiver is off. The active one has the bright label.");
            }
        }
        public static string OptAutoParallax { get { return T("Parallaxe automatisch setzen", "Auto set parallax"); } }
        public static string OptAutoReceiver { get { return T("Empfänger automatisch setzen", "Auto set receiver"); } }

        public static string TipOptAutoReceiver
        {
            get
            {
                return T("<b>Empfänger automatisch setzen</b>\nAn: Der Empfänger folgt den angehakten, gefluteten Rohren. Keines angehakt = Aus. Bug- und Heckrohre angehakt = das Ende, in das das Sehrohr blickt (bei eingeschalteter Peilungsfolge). Eine Auswahl von Hand hat Vorrang, bis du die Rohrauswahl änderst.\nAus: Nur du stellst den Empfänger; die Rohrauswahl ändert ihn nicht.",
                         "<b>Auto set receiver</b>\nOn: the receiver follows the checked, flooded tubes. None checked = Off. Bow and stern both checked = the end the periscope is looking toward (while follow-up is on). A manual pick takes priority until you change which tubes are checked.\nOff: only you set the receiver; checking tubes doesn't change it.");
            }
        }
        public static string OptRealisticTdc { get { return T("Realistischer Rechner-Nachlauf", "Realistic TDC follower"); } }

        public static string TipOptShowParallax
        {
            get
            {
                return T("<b>Parallaxe anzeigen</b>\nZeigt zwischen TDC- und Rohrzähler die Parallaxkorrektur des Rechners mit den Tasten Vorn / Achtern.",
                         "<b>Show parallax</b>\nShows the TDC's parallax correction, with Forward / Backward buttons, between the TDC and tube counters.");
            }
        }

        public static string TipOptAutoParallax
        {
            get
            {
                return T("<b>Parallaxe automatisch setzen</b>\nAn, erste zutreffende Regel:\n1. Angehakte Rohre: Bugrohre = vorn, Heckrohre = achtern.\n2. Gewählter Empfänger: Bug = vorn, Heck = achtern.\n3. Sonst die Zielpeilung des Rechners: vorn, solange das Ziel vor dem Querab steht, achtern, sobald es achterlicher steht.\nAus: Du wählst die Richtung selbst mit Vorn / Achtern, im realistischen wie im schnellen Rechnermodus.",
                         "<b>Auto set parallax</b>\nOn, first rule that applies:\n1. Checked tubes: bow tubes = forward, stern tubes = backward.\n2. Selected receiver: Bow = forward, Stern = backward.\n3. Otherwise the TDC's target bearing: forward while the target is ahead of the beam, backward once it is abaft.\nOff: you choose the direction yourself with Forward / Backward, in both realistic and fast TDC mode.");
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

        public static string TipOptTdcOperator
        {
            get
            {
                return T("<b>Rechnerbediener erforderlich</b>\nAn: Wie im Originalspiel. Ohne Besatzungsmitglied am Vorhaltrechner ist er abgeschaltet: er rechnet nicht, und die Rechner-Taste in der Stationsleiste ist gesperrt.\nAus: Der Rechner rechnet auch unbesetzt und kann über die Stationsleiste geöffnet und bedient werden. Stromausfall und Schäden schalten ihn weiterhin ab.",
                         "<b>TDC operator required</b>\nOn: as in the base game. Without a crew member at the TDC it is switched off: it doesn't calculate, and its button on the station bar is greyed out.\nOff: the TDC calculates unmanned and can be opened and operated from the station bar. Power loss and damage still switch it off.");
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
                return T("<b>Rohrwinkel anzeigen</b>\nZeigt unter dem TDC-Zähler den Winkel, auf den die Kreisel der gewählten Rohrgruppe eingestellt sind.",
                         "<b>Show receiver angle</b>\nShows the angle the selected tube group's gyros are set to, under the TDC counter.");
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
                       + T("\n<i>Strg + Ziehen zum Verschieben.</i>", "\n<i>Ctrl + drag to move.</i>");
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
                       + T("\n<i>Strg + Ziehen zum Verschieben.</i>", "\n<i>Ctrl + drag to move.</i>");
            }
        }

        public static string TipSyncLamp
        {
            get
            {
                return T("<b>Schusswinkel eingestellt</b>\n<color=#6f6>Grün</color>: die Kreisel der gewählten Rohrgruppe stehen auf dem Rechnerwert.\n<color=#f66>Rot</color>: wird synchronisiert.\nDunkel: Empfänger aus.",
                         "<b>Gyro set</b>\n<color=#6f6>Green</color>: the selected tube group's gyros match the TDC angle.\n<color=#f66>Red</color>: synchronizing.\nDark: receiver off.");
            }
        }

        public static string TipBearingHeader
        {
            get
            {
                return T("<b>Zielpeilung</b>\nOb der Vorhaltrechner der Peilung dieses Sehrohrs bzw. der UZO folgt. Die Lampe leuchtet <color=#6af>blau</color>, solange er NICHT folgt, und ist dunkel, solange er folgt.\n<i>Strg + Ziehen zum Verschieben.</i>",
                         "<b>Target bearing</b>\nWhether the TDC follows this periscope's or the UZO's bearing. The lamp is lit <color=#6af>blue</color> while it is NOT following, and dark while it is following.\n<i>Ctrl + drag to move.</i>");
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
                       + T("\n<i>Strg + Ziehen zum Verschieben.</i>", "\n<i>Ctrl + drag to move.</i>");
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
