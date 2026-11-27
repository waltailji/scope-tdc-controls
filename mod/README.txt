Scope TDC Controls  v1.6.0 (UBOAT 2026.1 patch 21)
===================================================

SH3-style TDC readouts and controls in the periscope and UZO views.

  Under the bearing tape              Beside the periscope raise/lower control
  [ TDC       |   3 5 7 ° (b) ]       [ Scope TDC Controls  v ]   (v = roll up / out)
  [ Parallaxe | + 0 0 1 °     ]       [ (o) ZIELPEILUNG ]
  [ BUG       |   3 5 4 ° (o) ]       [ FOLGEN ][ NICHT FOLGEN ]
                                      [ PARALLAXENMODUS ]
                                      [ VORN ][ ACHTERN ]
                                      [ FENSTER       ]
                                      [x] Schusswinkel (gyro angles)
                                      [x] Rechnerscheiben
                                      [x] Fächer (salvo spread)
                                      [x] Drehgeschwindigkeit (angular speed)
                                      [x] Schusswahl
                                      [x] Richtungswahl
                                      [x] Rohranzeige (torpedo tubes)
                                      [x] Torpedoeinstellung
                                      [x] Abfeuerhebel
                                      [ RESET WINDOW SIZES ]
                                      [ + EINSTELLUNGEN ]

Hover over any element for a pop-up description. Drag the background of the
counters, the panel or a window to move it (dials, switches and levers only
react on their round faces, so drags beside them move the window). With
"Ctrl+drag to move" on in the settings, hold Ctrl to move instead. The TDC
dials, both selector windows and the launch lever can be resized with the grip
in their bottom-right corner; RESET WINDOW SIZES on the panel sets them back.
Positions and sizes are saved automatically.

A button in the periscope / UZO screen's left sidebar (counter icon, next to
the chronometer) shows or hides the control panel, its windows and the gyro
angle counters. The choice is remembered (ControlsVisible in the
settings file).

TDC dials: a window on the left of the scope with the TDC's target dials,
stacked: LAGE / GEGNERFAHRT / ENTFERNUNG (AOB / TARGET SPEED / TARGET DISTANCE).
They work like the dials on the TDC itself: grab a dial and turn it around
its centre, and the TDC value follows. The readout under each dial shows the
exact value.
  AOB        like the TDC's Lagenwinkel dial: black rim marked every 10°,
             numbered every 20° (0 = bow on at the top, 180 = stern on at the
             bottom), inner disc solid red on the port half (Bug links) and
             green on the starboard half (Bug rechts), and a window at the
             bottom showing the ones digit: a ring that turns once per 10°
             with digit pairs red 0 / green 0, red 9 / green 1, red 8 /
             green 2 ..., read at the index at the bottom (green to
             starboard, red to port); brass needle with an arrow tip;
             readout S/P (Stb/Bb) 000-180°. The TDC follows the dial as you
             turn it. Locked while the TDC's locking mechanism is triggered.
  AOB motor  LAGEMOTOR flip switch under the AOB dial (click to flip): Ein =
             the TDC keeps the AOB in step as the bearing or own course
             changes (AOB tracking); Aus = the AOB stays as set. The AOB dial
             leaves the switch as it is
  Speed      0-55 kn, clockwise from just right of the top, like the TDC
  Distance   3-100 hm (300 m - 10 km) on the TDC's stretched scale,
             clockwise from just right of the top; numbers round the rim,
             marks inside them (dots on the half-way marks) and a four-lobed
             knob with a white triangle pointer, like the TDC
  Click a value on a dial's scale to set it directly. Hold Shift for 10x
  finer turning. The mouse wheel steps 1° / 0.5 kn / 50 m up to 10 hm and
  100 m above (with Shift 0.1° / 0.05 kn / 10 m). Dragging or clicking the
  distance dial sets it to the nearest 10 m. Ctrl+drag moves the window. Greyed out
  and locked while the TDC is not manned. Shown or hidden with the
  Rechnerscheiben (TDC dials) check box on the control panel.
  Typing a value: click the black readout under a dial, type the value and
  press Enter (Esc cancels). AOB -180 to 180 (+ starboard, - port), speed
  0-55 kn, distance 300-10000 m ("4500", "45 hm", "4.5 km"; a bare number
  under 300 is hm). The same works on the target length (100-200 m), spread
  angle (0-20°), bow and stern gyro coarse dials and the bow spread angle
  (0-360° / 0-20°), the TDC target bearing coarse dial (0-360°) and the
  torpedo depth (1-14 m, in quarter metres). A value out of range is refused
  and the dial stays as it was: after Enter the box stays open showing the
  range in red; clicking away flashes the readout red. The game's keys and
  the mod's hotkey hold off while you type.

Salvo spread (Fächer): a window just right of the TDC dials, stacked:
  Target length  STREUBREITE, 100-200 m, like the TDC's dial: the numbers are
                 on a ring that turns; the value under the fixed index at the
                 top is the one set (click, drag or wheel as on the TDC dials)
  Spread angle   STREUWINKEL, 0° at the top clockwise to 20° at the bottom,
                 like the TDC's dial. Brass needle: the angle the TDC works
                 out from target length and distance. Red flag: the angle set
                 (sent to the tubes, shown in the readout); turn, click or
                 wheel (0.5°) to move it
  Spread motor   STREUMOTOR flip switch, click to flip. Ein: the TDC drives
                 the red flag to the computed angle (set the spread with the
                 target length; a flag moved by hand is driven back). Aus:
                 the flag stays where you put it
  The Fächer (Salvo spread) check box under FENSTER shows it all the time.
  Unchecked, it pops up only while the Einzelschuß switch is on a spread
  position (Fächer auf geradem Kurs / im Drehen) and "Auto display spread
  settings" (settings drop-down, on by default) is on, and hides again at any
  other position. The anchor icon at its top right keeps it beside the TDC
  dials (dark = anchored: it follows them, dragging moves it relative to
  them) or lets it stand free (faint); switching keeps it where it is.
  Resize with the corner grip.

Own angular speed (Drehgeschwindigkeit): a window right of the TDC dials, its
  bottom level with theirs (the salvo spread window sits above it), with the
  TDC's rate-of-turn knob: -3 to 3 °/s, Bb (port) left / Stb (starboard) right,
  a four-lobed knob like the TDC's. The TDC leads the gyro angle by it when
  firing while turning. Mouse wheel 0.1 °/s. Shown with the Drehgeschwindigkeit
  (Angular speed) check box under FENSTER, and only on boats whose TDC has the
  knob (the check box is greyed out on the others). Anchor icon and corner
  grip as on the salvo spread window.

  Voice callouts (settings drop-down, on by default): the crew member at the
  scope announces what you dial in, with the game's own voice lines and
  subtitles, just as when you send a locked target's values to the TDC
  ("Attack parameters update... angle on bow, right, 3-0"). Values take
  effect as you turn the dials and are announced after you let go (or the
  wheel stops). Changing a value while it is
  still being said gets one more callout with the latest value. Nobody at
  the scope: values are applied silently. Distances above 9999 m are
  spoken as 9999 (the game's voice lines stop at four digits).

Firing selector (SCHUSSWAHL): a window near the bottom of the scope with the
three switches of the Schaltkasten T-Abf.-Anl.:
  Einzelschuß (Launch mode)       tube for a single shot, or a salvo on a
                                  straight course / while turning
  Fächerschuß (Salvo mode)        which tubes fire together (Aus = none)
  T.Abf. Schalter (Firing station) Brücke (bridge/UZO), Turm (tower) or Aus
  Each switch offers exactly the positions of your boat's own knob, so it
  follows the sub type (salvo labels come from the game's own text), and
  turning it does what turning the knob in the boat does; the knob in the
  boat turns along. Click a label to switch there; click the handle (left =
  on, right = back), use the mouse wheel or drag to turn it. Ctrl+drag moves
  the window. Shown and hidden with the sidebar button.
  Red lamp on the right (Abfeuerkreis, firing circuit): lit for 4 s after a
  fire command given while the launch lever lamp was green. While it is lit
  the three switches can't be turned; hover it for its state.
  Anchor icon at the top right: dark = anchored to the direction selector
  (sits left of it, bottom level with it, and follows it), faint = free (the
  default); switching keeps it where it is.
  Shown or hidden with the Schusswahl (Firing selector) check box on the control
  panel.

Torpedo tubes (Rohranzeige): a compact window, by default above the bottom
  windows in the centre, with one numbered circle per tube: bow tubes on the
  first row (Bug / Fore), stern tubes on the second (Heck / Aft), as many as your boat
  has. Colours: black = empty (or loading), grey = loaded, not flooded, yellow =
  in maintenance (the crew servicing the torpedo), blue =
  flooded (filling while it floods), green = flooded and selected, red =
  firing (for 4 seconds after its torpedo leaves).
  Outer doors (setting "Enhanced torpedo tube handling", on by default): a grey +
  across a flooded tube = its outer door is shut. Right-click a flooded tube,
  Open outer door: the door opens in 3 seconds (the + is swept away and the
  tube's door opens on the boat) and stays open. A tube fires only with its
  door fully open. After the shot the tube stays flooded and empty with its
  door open: blue with a grey X. The game no longer treats it as dry at once:
  the crew won't reload it until you shut the door (right-click, Shut outer
  door, 3 s) and drain it (double-click or right-click, Drain). No tube can be
  drained with its door open, here, on the game's torpedo panel or by the crew.
  The door state isn't saved with the game: after loading, all doors are shut.
  The game's own fire key skips a selected tube whose door isn't fully open
  (the others still fire). Fired by the crew at the lever in the torpedo room
  with the door shut, the tube still fires and its door counts as open after.
  Click a flooded tube to select it for a single shot: the Einzelschuss switch
  turns to it and Faecherschuss to Aus, as on the switch box (not while the
  firing circuit lamp is lit). Double-click a loaded tube to flood it, or to
  drain it when flooded or flooding, as the game's torpedo panel does. Clicking
  the selected tube again, or draining it, deselects it (Einzelschuss to Aus).
  Hover a tube for its state and what a click / double-click does. Drag to
  move, resize with the corner grip. Shown or hidden with the Rohranzeige (Torpedo
  tubes) check box.

Torpedo settings (Torpedoeinstellung): appears at the bottom, right of the
  centre, while a checked tube holds a torpedo (loaded, flooding or flooded):
    Tiefe (Depth)        running depth dial, scaled 0-15 m, numbered every
                         metre from 1 to 14, red below 1 m and beyond 14 m;
                         it sets 1-14 m in 0.25 m steps (wheel, click, drag)
    Fahrstufe (Speed)    L / M / S (S / M / F): only the speeds every checked
                         torpedo has; the readout shows the speed in knots;
                         also passed to the TDC, as the game's speed dial does
    Pistole (Pistol)     AZ / MZ (impact / magnetic) for the checked torpedoes
  Smaller than the TDC dials by default (resizable like the other windows).
  With the Torpedo Room mod installed, the window sets each checked torpedo's
  own Torpedo Room depth, speed and pistol (which Torpedo Room applies at
  launch) and shows the first one's.
  Shown or hidden with the Torpedoeinstellung (Torpedo settings) check box.

Launch lever (T-Abf.-Schalter): a narrow window at the bottom right with a
  spring-loaded lever, Aus (Off) / Schuß (Fire). It rests at Aus; press on
  it (grip, slot or plates) to throw it to Schuß and fire the selected tubes;
  it stays down while the mouse button is held and springs back when you let
  go. It fires through the boat's own launch lever (which moves
  along), with the same checks: not while torpedoes are being launched, and
  every checked tube must be flooded; with outer doors, every checked tube's
  door must also be fully open (for a salvo, all of them).
  Ready lamp under the slot (hover it for what it shows right now and why):
    green  ready
    red    safe - the lever does not fire (stricter than the boat's own
           lever, which only refuses while launching or with a dry tube):
             - no tube selected, or a selected tube not flooded or its
               outer door not fully open
             - the gyro angle receiver not on the selected tubes' end
               (Bug for bow tubes, Heck for stern tubes)
             - the set distance beyond the torpedo's run: the TDC's "max
               distance to target" drum (its geometry ratio x the torpedo
               range on its range knob) - how the watch officer made the
               system safe
             - the gyro angle past the gyro setting gear's ±135° stop,
               measured from the tube axis (0° bow tubes, 180° stern tubes)
    amber  firing
    dark   no power: the lever is only live while the firing station switch
           (T.Abf. Schalter) is on Brücke or Turm; on either Aus it is dead.
           (On the boat only the named station is live, bridge for the UZO,
           tower for the attack periscope; for now either one powers it.)
  Shown or hidden with the Abfeuerhebel (Launch lever) check box.

Raise fully / lower fully: two slim buttons just above and below the stock
periscope raise/lower control. Click one to run the periscope all the way up
or down at its normal speed; click it again, or use a stock raise/lower
button, to stop. Hidden in the UZO and while the game UI is hidden.
(ElevatorButtons=false in the settings file removes them.)
The setting "Hide periscope raise/lower control" (off by default) hides the
game's raise/lower control and these buttons; the keys still raise and lower
the periscope.

Version history
  1.6.0 Torpedo tube outer doors: right-click a flooded tube to open its door
       (3 s, the tube's door opens on the boat) or shut it. The launch lever
       and each tube's own lever fire only with the door fully open, and the
       game's fire key skips a tube whose door isn't open. After a
       shot the tube stays flooded and empty, door open, until you shut and
       drain it; the crew reloads it only then. Tube indicator: red while
       firing, a grey + for a shut door, a grey X for a fired tube still
       flooded. New setting "Enhanced torpedo tube handling" (on by default).
       The settings drop-down is more compact and scrolls (mouse wheel or
       its scroll bar) when it doesn't fit on the screen.
  1.5.5 Type values into the dials' readouts (from the Contact Keeper mod):
       AOB, target speed, target distance, target length, spread angle, bow
       gyro coarse and spread angle, stern gyro coarse, TDC target bearing
       coarse and torpedo depth. Values are checked against each dial's range.
  1.5.4 The gyro angle counters keep their spot: placed under the bearing
       tape once, on first start, they no longer follow it (or another
       compass on screen) around. Ctrl+drag still moves them. With the
       True Bearing on Periscope mod installed, its TRUE counter replaces
       this mod's while it shows (this one can still show on the binoculars).
  1.5.3 The raise fully / lower fully buttons sit just above the stock
       raise/lower control, beneath every other window (this mod's and other
       mods'), so a window moved over them stays on top.
  1.5.2 Target distance dial: dragging and clicking set the distance to the
       nearest 10 m; the mouse wheel steps 50 m from 3 to 10 hm (Shift 10 m);
       its scale now follows the TDC's face exactly (logarithmic; 9 and 10 were
       bunched together), with the 85 / 90 / 95 marks; numbers and marks moved
       out toward the rim, a bigger knob with its pointer reaching the ring
       round it, smaller dots on the half-way marks. The wheel lands on round
       values. AOB and target speed: marks and numbers out to the rim; the
       speed dial gets a slimmer needle with an arrowhead; longer, slimmer
       arrowheads on every needle; no index triangle by the AOB ones window.
  1.5.1Type II T.Abf. Schalter as on its box: IID = Brücke (UZO) at the
       top, Turm at the bottom (no off position); IIA (no UZO) = Aus at the
       top, Turm at the bottom. Bow gyro setting mode switch laid out as on
       the panel: auto at the top, salvo to the right, gyro at the bottom.
       Setting "Hide periscope raise/lower control" (off by default).
  1.5  TDC target bearing window (Zielrichtung): fine (10 degrees a turn) and
       coarse dials, needle = bearing source, red flag = the TDC's bearing; set
       it by hand by turning or clicking, and the TDC turns to it at 80 deg/s
       so AOB tracking keeps up. Opened from WINDOWS or the dial icon left of
       the TDC Gyro counter; x to close.
       Bow torpedo room gyro setting panel: spread angle, gyro coarse / fine
       (needle = received, flag = set), fast sync lever with red / green
       lamps, salvo size and setting mode switches, and a free-spinning hand
       wheel (manual gyro 180 deg a turn, Shift 18; else spread).
       Stern torpedo room gyro setting panel: gyro coarse / fine and a hand
       crank. Both panels open from WINDOWS, the dial icon left of the Bow /
       Stern counter, or the icons at the end of each row of the tube window.
       TRUE bearing counter above the gyro counter: where the scope looks, as a
       compass bearing; setting "Show True Bearing on binoculars" (off).
       Torpedo tubes: right-click for Flood / Drain, Select and Launch;
       Ctrl+click launches with the tube's own lever in the torpedo room.
       Mod UI hidden on the binoculars (B key and the bridge observation post).
       The TDC counter is labelled "TDC Gyro".
  1.4.3 Keep my scope bearing no longer touches the bridge binoculars (B): they
       open where you look and turn freely again (they were held on the
       bearing saved last time). Periscopes and the UZO are unchanged.
       The torpedo panel's "reset manual rotation" button works again away from
       the scope (it was blocked like a ship click), and the scope is not put
       back on the old bearing afterwards.
  1.4.2 Torpedo settings: even spacing between the depth dial and the knobs;
       pistol options named AZ / MZ. All window titles and the tube rows
       (FORE / AFT) in capitals. Tube indicator: the blue rises from the
       bottom while a tube floods (and falls while it drains).
       Torpedo Room: depth and speed set in the torpedo settings window now go
       through Torpedo Room's own edit path, only onto the checked torpedoes (the
       shared setting the others follow is left alone).
       New setting "Keep my scope bearing" (on): coming back to a periscope puts
       it back on the bearing you left it at (also after a lock / unlock), a
       target change keeps your aim, and ship / contact clicks away from the
       scope don't swing it round (covers what Stop Moving My Scope can't).
  1.4.1 The sidebar button now hides the gyro angle counters too; the
       ToggleKey hotkey works like it (the raise / lower fully buttons stay).
       Switch box knobs follow each boat: only the bearing sources its knob has
       (no UZO on the Type II); Type II layouts as on its box; Type VII / IX
       gyro receiver Bow right, Stern left, bearing source Attack left, UZO
       top, Obs. right; Type IX salvo mode positions and labels as on its box.
       No AOB motor switch on a TDC without one.
       English names: Launch mode, Salvo mode; the spreads are "Salvo on
       straight course" / "Salvo in a turn".
       Tube indicator: yellow for a tube in maintenance.
       With "Auto display spread settings", the angular speed window pops up
       on "salvo in a turn" too.
       Hotkey field in the settings drop-down: click, press a key; Backspace /
       Delete = no hotkey.
  1.4  The scope screen button no longer overlaps the velocity tool button.
       Firing selector: red firing circuit lamp, lit for 4 s after a fire
       command given with a green launch lever lamp; its switches can't be
       turned while it is lit.
       New setting "Auto select tube when flooding" (off by default): flooding
       a tube no longer checks it or moves the firing selector switches.
       Target length moved to a new salvo spread window (Fächer) with the
       spread angle dial; it pops up on its own for spreads ("Auto display
       spread settings", on by default), or always with its check box.
       Own angular speed knob (-3 to 3 °/s) in its own window beside the TDC
       dials, shown with its check box under FENSTER (WINDOWS).
       Salvo spread window: red spread angle flag (set by hand) and a spread
       motor switch that drives it to the computed angle.
       TDC dials: AOB motor flip switch under the AOB dial.
       Anchor icons: salvo spread and angular speed windows follow the TDC
       dials, the firing selector can follow the direction selector. Salvo
       spread check box = always shown.
       Red / blue interior light tint is darker and now covers everything in
       the off-white windows (text, scales, pointers, knobs; dark parts get a
       faint glow), lamps and tube circles excepted; "Tint panels with sub
       lighting" setting (on by default) turns it off.
       New torpedo tube indicator (SH3 style): one circle per tube, bow and
       stern rows, coloured by state; click selects, double-click floods or
       drains.
  1.3  Torpedo settings window (depth, speed, pistol) while a tube is checked;
       works with the Torpedo Room mod's per-torpedo settings.
       Gyro-set lamp moved to the selected receiver's counter; the TDC counter
       has a blue follow lamp (click to switch follow-up).
       The overlay hides while the game's menus or full-screen windows are open
       (escape menu, settings, load/save, overviews, crew, journal, map ...).
       The off-white windows (TDC dials, selectors, launch lever) take on the
       colour of the boat's interior lighting: reddish under the red lights,
       bluish under the blue (silent running) lights, fading with them.
       All dials and switches share one layout: the name above (a little
       larger), the current setting below in a black box. "Fire lever" is
       now "Launch lever".
       Fixed: setting AOB on the dial no longer switches the TDC's AOB tracking
       off (it now switches the AOB motor and tracking on, as the crew's AOB
       report does), and a parallax switch keeps the AOB, so sweeping the
       scope from bow to stern keeps the target's course. The launch lever
       fires like the fire key and no longer leaves the 3D lever stuck at
       Schuss. Depth scale clicks work on the whole scale; torpedo knobs
       click; less stutter (no repeated scene searches).
  1.2  Voice callouts for the TDC dials, using the game's own crew voice
       lines.
       Window check boxes (FENSTER / WINDOWS) on the control panel to show or
       hide the TDC dials, firing selector, direction selector and fire
       lever.
       Target length (Streubreite) dial (turning ring) in the TDC dial
       window, shown for spreads; AOB needle with an arrow tip; distance
       dial as a knob with an arrow.
       Launch lever window (spring-loaded Aus / Schuß) with a ready lamp.
       Direction selector window (Zielrichtung / Schußrichtung knobs) and
       replace those
       panel buttons; the panel is titled "Scope TDC Controls", with the
       target bearing and parallax buttons side by side and window check
       boxes.
       Firing selector window (single shot, salvo, firing station),
       adapted to the boat's tube setup.
  1.1  Sidebar button to show/hide the control panel.
       Raise fully / lower fully buttons on the periscope raise/lower control.
       TDC dial window (AOB, target speed, target distance) on the left of
       the scope.
       Counters start in the right place right after loading a game
       (they used to sit lower until the game UI was toggled with Alt).
  1.0  First release.

Fixes to the base game
----------------------
  Single torpedo launch fix (settings drop-down, on by default)
       Pressing fire, then checking another flooded tube a few seconds later,
       fired that tube too: the fire sequence (1.5 s delay, then 4 s between
       tubes) reads each tube's check box only when it gets to it. Now a fire
       command fires only the tubes checked when you pressed fire; tubes
       checked afterwards wait for the next command, like the real firing key.

  Gyro receiver forced to Bow
       While the periscope torpedo panel is open, the game sets the receiver
       to Bow whenever any bow tube is flooded, even with only the stern tube
       checked, so stern shots from the scope were impossible. The receiver
       now follows the checked tubes (or your pick; see "Gyro angle receiver").

  "Not following" undone while a target is locked
       The game turns bearing follow-up back on every frame while a target is
       locked. "Not following" on this mod's panel now holds until you press
       "Following" or lock a different target.

  Tube selection changing when the torpedo panel is hidden or shown
       Hiding/showing the panel could check a different tube (its check-box
       toggles fire change events on the way). Those are ignored, and your
       selection is put back on reopening. The fire-control box tube selector
       is also kept in step with the scope selection, so mods that read the
       selection from the box (e.g. Torpedo Room) keep your choice.

Counters
--------
  TDC        The gyro angle computed by the TDC (Schusswinkel), the same
             value as the TDC's gyro dial.
  Parallax   The parallax correction (degrees, +/-) the TDC adds to the gyro
             angle, because the periscope and the tubes are in different
             places.
  BUG / HECK (BOW / STERN)
             The angle the tube groups' gyros are actually set to. The
             receiving group catches up with the TDC value while the lamp is
             red. With "Show active receivers only" on, only the selected
             group is shown (none while the receiver is off). With it off,
             bow and stern are always shown and the active one has the bright
             label.

  Double-click a BUG/HECK row to aim at what those tubes are already set to:
    - all receivers shown: selects that row's receiver;
      "show active receivers only": switches to the other receiver.
    - The scope then turns so the TDC's gyro angle matches the angle that
      group's gyros already hold, so they are ready without re-setting.
      Follow-up is switched on and a target lock is released for this.
    - While the scope turns the receiver is set to Aus, so the TDC's passing
      angles don't drag those gyros along; it connects once the TDC angle
      matches (or if you click elsewhere / after 20 s). Parallax is held on
      that end meanwhile, and tubes checked at the other end are unchecked.

  The drums roll like a mechanical counter: each drum turns while the drums
  to its right pass 9 -> 0, including 359 -> 000, where the 3 and 5 roll over
  to 0 together.

  The counters sit under the bearing tape: their spot is taken from the tape
  the first time it is seen (once it holds still) and kept from then on, so
  they don't wander if the tape, another compass on screen or another mod
  moves things. Ctrl+drag moves them. To place them under the tape again,
  clear TapeAnchorX and TapeAnchorY in the config file.

  TDC not manned: while nobody is working the TDC station, the TDC counter
  is greyed out with "TDC not manned" (Rechner nicht besetzt) under it, and
  the other counters are hidden. The controls keep working. (Not shown when
  "TDC operator required" is off.)

Lamps on the counters
  TDC row (blue)   lit while the TDC is NOT following the bearing, dark while
                   following; click it to switch follow-up (Folgen / Nicht folgen)
  BUG / HECK row   gyro-set lamp, on the selected receiver's row only (no lamp
                   while the receiver is off):
                     green = that group's gyros are set to the TDC angle (3.5°)
                     red   = synchronizing (the gyros are still turning)

Control panel and windows
-------------------------
The control panel (titled "Scope TDC Controls"; hover the title for the
version and author) holds the target bearing and parallax mode buttons, the
WINDOWS check boxes and the settings drop-down (which opens above any of
the mod's windows). Bearing source and gyro receiver are
worked in their own windows:
Target bearing - on the panel (Zielpeilung: Folgen / Nicht folgen)
  Whether the TDC follows this scope's or the UZO's bearing: two buttons side
  by side, with a blue lamp in the header that is
  lit while NOT following and dark while following. The buttons use
  the game's own follow-up handler. Normally the game turns follow-up back
  on every frame while a target is locked. With this mod, "Not following"
  holds until you set Folgen again or lock a different target.

Bearing source - Zielrichtung, in the RICHTUNGSWAHL / DIRECTION SELECTOR window
  (Aus at the bottom, Zieloptik = attack periscope at the top, UZO right,
  observation periscope left)
  Selects which scope sends its bearing to the TDC. Normally the game picks
  the scope you are looking through. A pick here takes priority until a new
  target is locked; turning the physical knob on the fire-control box also
  counts as a pick. Scopes the boat doesn't have are left off the dial.

Gyro angle receiver - Schußrichtung, in the same window
  (Aus at the top and bottom, Bug left, Heck right, as on the box)
  Selects which tube group receives the TDC gyro angle. "Heck" is left off
  on boats without a stern receiver.

  Fix for the game's torpedo panel: normally, while the periscope torpedo
  panel is open, it forces the receiver to Bow whenever any bow tube is
  flooded, even if only the stern tube is checked. This mod replaces that.

  With "Auto set gyro receiver" on (default), the receiver follows the CHECKED
  flooded tubes, all the time (torpedo panel open or not):
    - only bow tube(s) checked     -> Bug
    - only stern tube(s) checked   -> Heck
    - bow AND stern checked        -> the end the periscope looks toward
                                      while follow-up is on (ahead of the
                                      beam = Bug, abaft = Heck, 5° dead band);
                                      with follow-up off it stays as it is
    - nothing checked              -> Aus (off)
  A manual pick (this panel or the receiver knob in the control room) takes
  priority until you change which tubes are checked.

  With "Auto set gyro receiver" off, checking tubes never changes the receiver;
  only you do, here or with the knob.

  Fire-control box selector: when you check tubes in the scope's torpedo
  panel, the tube selector / salvo preset on the fire-control box is set to
  match (None when nothing is checked), as on the real boat. Mods that read
  the selection from the box while the panel is closed (e.g. Torpedo Room)
  then keep your choice instead of switching to whatever the knob was on.

  Tube check boxes, one end at a time: checking a stern tube unchecks the
  bow tubes, and vice versa. When the game auto-checks a tube that finished
  flooding while tubes at the other end are checked, that auto-check is
  undone, so your selection stays. Check boxes are also kept when you hide
  and show the torpedo panel: check-box changes the game's UI fires while
  the panel is closing are ignored (they used to check another tube, so the
  receiver and parallax flipped while the panel was hidden), and on reopening
  the previous selection is restored unless a tube changed state (fired,
  drained) or the tube/salvo knob was turned in the meantime.

Parallax mode (Parallaxenmodus: Vorn / Achtern)
  Direction of the TDC's parallax correction: forward for the bow tubes,
  backward for the stern tubes. While "Auto set parallax mode" is on, the
  buttons only show the direction, which is set by the first rule that
  applies:
    1. checked bow tube(s)      -> forward
       checked stern tube(s)    -> backward
    2. receiver Bug             -> forward
       receiver Heck            -> backward
    3. otherwise                -> by the TDC's target bearing: forward
                                   while the target is ahead of the beam,
                                   backward once it is abaft (5° dead band
                                   on the beam)

Settings drop-down (EINSTELLUNGEN / SETTINGS), all on by default
  Show gyro receiver angles   show the BUG/HECK rows
  Show active receivers only  on  = only the selected receiver's row
                              off = bow and stern rows always, even when off
  Show parallax angle         show the Parallax row
  Auto set gyro receiver      on  = receiver follows the checked tubes
                                    (see "Gyro receiver" above)
                              off = only you set the receiver
  Auto set parallax mode      on  = direction follows the checked tubes,
                                    else the receiver, else the target
                                    bearing (see "Parallax" above)
                              off = choose it yourself with VORN / ACHTERN
                                    (FORWARD / BACKWARD), in realistic and
                                    fast mode
  Auto select tube when      on  = base game: a tube is checked when it
  flooding                          starts flooding and the firing selector
                                    switches follow
                              off = (default) flooding leaves the tube
                                    selection and the switches alone
  Realistic TDC follower   off = the TDC's motors (bearing follow-up, impact
                                 angle, parallax correction) run 10x faster
  Realistic receiver sync  off = the tube gyro setting motors run 10x faster
                                 (manual gyro setting at the tubes is left alone)
  Single torpedo launch fix  on  = a fire command fires only the tubes checked
                                 when you pressed fire, like the real firing
                                 key; tubes checked afterwards wait for the
                                 next command (unchecking one mid-sequence
                                 still holds it back)
                           off = base game: the fire sequence (1.5 s delay,
                                 then 4 s between tubes) also fires tubes
                                 checked while it is running
  TDC operator required    on  = base game: without a crew member at the TDC
                                 it is switched off (no calculation, its
                                 station-bar button greyed out)
                           off = the TDC calculates unmanned and can be
                                 opened and operated from the station bar;
                                 power loss and damage still switch it off
  Ctrl+drag to move        off = (default) drag a window's background to move it
                           on  = hold Ctrl and drag to move windows, counters
                                 and the panel
  Voice callouts           on  = the man at the scope announces values set
                                 with the TDC dials (see "TDC dials")
                           off = dial values are applied silently
  Each change is saved immediately and carries over to later sessions. The
  TDC options apply throughout the game, not only while looking through the
  scope. The faster modes run the game's own motor simulation more often, so
  the TDC stays stable.

Language
  Labels and pop-ups follow the game's language (German, otherwise English).
  Override this with Language=DE or Language=EN in the settings file.

Settings file
-------------
  %USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\scope-tdc-controls-config.txt

Created on first launch and re-read every time you enter the scope. Settings
added in later versions are appended automatically; your values are kept.

  OffsetX / OffsetY                   counter position (Ctrl+drag sets these)
  ControlsOffsetX / ControlsOffsetY   panel position (Ctrl+drag sets these)
  DialsOffsetX / DialsOffsetY         dial window position (Ctrl+drag sets these)
  DialsX / DialsY                     dial window base position (screen fraction)
  ShowGyros / ShowDials / ShowSwitches / ShowDirections / ShowLever
                                      windows shown (also the
                                      WINDOWS buttons on the control panel)
  SwitchesOffsetX / SwitchesOffsetY   switch box position (Ctrl+drag sets these)
  SwitchesX / SwitchesY               switch box base position (screen fraction)
  LeverOffsetX / LeverOffsetY         launch lever position (Ctrl+drag sets these)
  LeverX / LeverY                     launch lever base position (screen fraction)
  DirectionsX / DirectionsY            direction selector base position;
  DirectionsOffsetX / DirectionsOffsetY  (Ctrl+drag sets the offsets)
  Position=Below | Above              side of the bearing tape for the counters
  Scale=1.0                           size of counters and panel
  Format=360 | LR                     000-359, or L/R 000-180
  ShowTenths=true                     adds a tenths drum to every counter
  RollSpeed=90                        drum follow speed in deg/s (0 = instant)
  ShowSyncLamp / ShowControls / Tooltips
  ShowReceiverAngle / ActiveReceiverOnly / ShowParallax / AutoReceiver / AutoParallax /
  AutoSelectFloodedTube / AutoSpread / ShowAngularSpeed / TintPanels / KeepScopeBearing /
  RealisticTdc / RealisticReceiverSync / SingleTorpedoLaunchFix /
  TdcOperatorRequired / VoiceCallouts / CtrlDragToMove   (also in the drop-down)
  DialsScale / SwitchesScale / DirectionsScale / LeverScale   window sizes
                                      (the corner grips set these)
  ControlsAnchor=Elevator | Screen    Screen = place the panel at ControlsX/ControlsY
  Anchor=Compass | Screen             Screen = place the counters at FallbackX/FallbackY
  HideWithGameUI=false                true = also hide this mod's elements with Alt
  Language=Auto | DE | EN
  ToggleKey=F9                        optional hotkey: same as the sidebar button;
                                      set it with the Hotkey field in the settings
                                      drop-down (empty = no hotkey)
                                      (raise / lower fully buttons stay)
  DebugLog=true                       log every torpedo tube check-box change

Troubleshooting: search output_log.txt for "[ScopeTDC]". It logs the
detected language and where the counters and panel attached.
