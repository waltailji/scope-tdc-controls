using System;
using System.Reflection;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Spoken TDC settings for the dial window, through the same system the
    /// game uses for its "send to TDC" commands while a ship is locked
    /// (Periscope.PassCourse/Velocity/DistanceToTorpedoCalculator):
    ///  - the crew member at the scope speaks, with the game's own voice lines
    ///    and subtitles ("Update - Angle On Bow X" + digits + left/right,
    ///    "Update - Target Speed X" + knots, "Update - Distance To Target X" in
    ///    metres), led in with "Attack Parameters Update" when nothing else is
    ///    being reported;
    ///  - the dials have already set the value on the TDC; the crew only
    ///    announces it after you let go.
    /// Changing a value while its line is still being spoken queues one more
    /// line with the latest value. Nobody at the scope (or the speech helper
    /// missing after a game update): nothing is said.
    /// </summary>
    public static class VoiceCallouts
    {
        const float LineTimeout = 15f; // a line that never reports its end

        sealed class Channel
        {
            public Func<InteractiveCharacter, float, IStatementTicket> Say;
            public IStatementTicket Ticket;
            public float Started;
            public float Spoken;
            public bool Queued;
            public float QueuedValue;
            public TorpedoCalculator Tdc;
        }

        static bool resolved;
        static MethodInfo passShort, passLong;   // Periscope.PassNumericValueToTorpedoCalculator (5 / 6 args)
        static string[] meterKeys;

        static readonly Channel aob = new Channel { Say = SayAob }; // the dial has already set it
        static readonly Channel speed = new Channel { Say = SaySpeed };
        static readonly Channel distance = new Channel { Say = SayDistance };

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                foreach (var m in typeof(Periscope).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (m.Name != "PassNumericValueToTorpedoCalculator") continue;
                    int n = m.GetParameters().Length;
                    if (n == 5) passShort = m;
                    else if (n == 6) passLong = m;
                }
                var f = AccessTools.Field(typeof(Periscope), "meterKeys");
                meterKeys = f != null ? f.GetValue(null) as string[] : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " voice callouts unavailable: " + e.Message);
            }
            if (passShort == null || passLong == null || meterKeys == null)
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " voice callouts unavailable (game speech helper not found); dial values are applied silently");
        }

        /// <summary>The crew member at the scope / UZO being looked through, as the game picks him.</summary>
        static InteractiveCharacter Speaker()
        {
            try
            {
                var ctl = ScopeAim.CurrentController();
                var scope = ctl != null ? ctl.Periscope : null;
                if (scope == null) return null;
                var inter = scope.GetComponent<Interactable>();
                var list = inter != null ? inter.ReservingCharacters : null;
                return list != null && list.Count > 0 ? list[0] : null;
            }
            catch { return null; }
        }

        // ---- the stock lines

        static IStatementTicket SayAob(InteractiveCharacter who, float value)
        {
            int v = Mathf.RoundToInt(Mathf.DeltaAngle(0f, value));
            if (v < -180) v += 360;
            if (v > 180) v -= 360;
            return passShort.Invoke(null, new object[]
            {
                "Update - Angle On Bow X", v >= 0 ? "Right Angle Suffix" : "Left Angle Suffix",
                "Statements/Voice 1305", Mathf.Abs(v), who
            }) as IStatementTicket;
        }

        static IStatementTicket SaySpeed(InteractiveCharacter who, float value)
        {
            return passShort.Invoke(null, new object[]
            {
                "Update - Target Speed X", "Knots Suffix", "Statements/Voice 1316",
                Mathf.Clamp(Mathf.RoundToInt(value), 0, 999), who
            }) as IStatementTicket;
        }

        static IStatementTicket SayDistance(InteractiveCharacter who, float value)
        {
            // the game's helper reads up to four digits
            return passLong.Invoke(null, new object[]
            {
                "Update - Distance To Target X", null, meterKeys, "Statements/Voice 1312",
                Mathf.Clamp(Mathf.RoundToInt(value), 0, 9999), who
            }) as IStatementTicket;
        }

        // ---- requests from the dials

        /// <summary>Announce a speed already set on the TDC.</summary>
        /// <summary>Announce an AOB already set on the TDC.</summary>
        public static void AnnounceAob(TorpedoCalculator tdc, float value) { Request(aob, tdc, value); }

        public static void AnnounceSpeed(TorpedoCalculator tdc, float value) { Request(speed, tdc, value); }

        /// <summary>Announce a distance already set on the TDC.</summary>
        public static void AnnounceDistance(TorpedoCalculator tdc, float value) { Request(distance, tdc, value); }

        public static bool Busy(bool aobOnly) { return aob.Ticket != null || (!aobOnly && (speed.Ticket != null || distance.Ticket != null)); }

        static bool Request(Channel c, TorpedoCalculator tdc, float value)
        {
            if (!GyroConfig.VoiceCallouts || tdc == null) return false;
            if (c.Ticket != null)
            {
                // still speaking the previous value: one more line with the latest
                c.Queued = true;
                c.QueuedValue = value;
                c.Tdc = tdc;
                return true;
            }
            return Start(c, tdc, value);
        }

        static bool Start(Channel c, TorpedoCalculator tdc, float value)
        {
            Resolve();
            if (passShort == null || passLong == null || meterKeys == null) return false;
            var who = Speaker();
            if (who == null) return false;
            try
            {
                if (aob.Ticket == null && speed.Ticket == null && distance.Ticket == null)
                    who.Say("Attack Parameters Update");
                var ticket = c.Say(who, value);
                if (ticket == null) return false;
                c.Ticket = ticket;
                c.Started = Time.unscaledTime;
                c.Spoken = value;
                c.Tdc = tdc;
                ticket.Stopped += t => Finished(c, t);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " voice callout failed: " + e.Message);
                return false;
            }
        }

        static void Finished(Channel c, IStatementTicket t)
        {
            if (!ReferenceEquals(c.Ticket, t)) return;
            c.Ticket = null;
            if (!c.Queued) return;
            c.Queued = false;
            Start(c, c.Tdc, c.QueuedValue); // one more line with the latest value
        }

        /// <summary>Safety net for a line whose end is never reported.</summary>
        public static void Tick()
        {
            Check(aob); Check(speed); Check(distance);
        }

        static void Check(Channel c)
        {
            if (c.Ticket != null && Time.unscaledTime - c.Started > LineTimeout) Finished(c, c.Ticket);
        }
    }
}
