Scope TDC Controls  v1.2  (UBOAT 2026.1 patch 21)
=================================================

SH3-style TDC readouts and controls in the periscope and UZO views.

  Under the bearing tape              Beside the periscope raise/lower control
  [ TDC       |   3 5 7 ° (o) ]       [ Scope TDC Controls ]
  [ Parallaxe | + 0 0 1 °     ]       [ (o) ZIELPEILUNG ]
  [ BUG       |   3 5 4 °     ]       [ FOLGEN ][ NICHT FOLGEN ]
                                      [ PARALLAXENMODUS ]
                                      [ VORN ][ ACHTERN ]
                                      [ FENSTER       ]
                                      [x] Schusswinkel
                                      [x] Rechnerscheiben
                                      [x] Schusswahl
                                      [x] Richtungswahl
                                      [x] Abfeuerhebel
                                      [ + EINSTELLUNGEN ]

Hover over any element for a pop-up description. Hold Ctrl and drag the
counters or the panel to move them. The new position is saved automatically.

A button in the periscope / UZO screen's left sidebar (counter icon, next to
the chronometer) shows or hides the control panel and the TDC dials. The
counters stay visible. The choice is remembered (ControlsVisible in the
settings file).

TDC dials: a window on the left of the scope with the TDC's target dials,
stacked: LAGE / GEGNERFAHRT / ENTFERNUNG / STREUBREITE (AOB / TARGET SPEED /
TARGET DISTANCE / TARGET LENGTH).
They work like the dials on the TDC itself: grab a dial and turn it around
its centre, and the TDC value follows. The readout under each dial shows the
exact value.
  AOB        like the TDC's Lagenwinkel dial: black rim marked every 10°,
             numbered every 20° (0 = bow on at the top, 180 = stern on at the
             bottom), inner disc solid red on the port half (Bug links) and
             green on the starboard half (Bug rechts), and a window at the
             bottom where the ones digits roll by (red port, green starboard);
             brass needle with an arrow tip; readout S/P
             (Stb/Bb) 000-180°. As on the TDC, turning it moves a pale ghost
             pointer; on release the TDC's AOB motor turns the brass pointer
             there (the game switches AOB tracking off meanwhile). Locked
             while the TDC's locking mechanism is triggered.
  Speed      0-55 kn, clockwise from just right of the top, like the TDC
  Distance   3-100 hm (300 m - 10 km) on the TDC's stretched scale,
             clockwise from just right of the top; numbers round the rim,
             marks inside them (dots on the half-way marks) and a four-lobed
             knob with a white triangle pointer, like the TDC
  Length     100-200 m, like the TDC's Streubreite dial: the numbers are on
             a ring that turns, and the value under the fixed index at the
             top is the one set. With the distance it gives the spread angle,
             so it is only shown while the Einzelschuß switch is on a spread
             (Fächer auf geradem Kurs / im Drehen); the window shrinks and
             grows to fit
  Hold Shift for 10x finer turning. The mouse wheel steps 1° / 0.5 kn /
  100 m (with Shift a tenth of that). Ctrl+drag moves the window. Greyed out
  and locked while the TDC is not manned. Shown or hidden with the
  Rechnerscheiben (TDC dials) check box on the control panel.

  Voice callouts (settings drop-down, on by default): the crew member at the
  scope announces what you dial in, with the game's own voice lines and
  subtitles, just as when you send a locked target's values to the TDC
  ("Attack parameters update... angle on bow, right, 3-0"). Speed and
  distance are set at once and announced after you let go (or the wheel
  stops). AOB is set, as in the game, once it has been said: AOB motor and
  tracking on, the TDC jumps to the new AOB. Changing a value while it is
  still being said gets one more callout with the latest value. Nobody at
  the scope: values are applied silently. Distances above 9999 m are
  spoken as 9999 (the game's voice lines stop at four digits).

Firing selector (SCHUSSWAHL): a window near the bottom of the scope with the
three switches of the Schaltkasten T-Abf.-Anl.:
  Einzelschuß (Single shot)       tube for a single shot, or a spread on a
                                  straight course / while turning
  Fächerschuß (Salvo)             which tubes fire together (Aus = none)
  T.Abf. Schalter (Firing station) Brücke (bridge/UZO), Turm (tower) or Aus
  Each switch offers exactly the positions of your boat's own knob, so it
  follows the sub type (salvo labels come from the game's own text), and
  turning it does what turning the knob in the boat does; the knob in the
  boat turns along. Click a label to switch there; click the handle (left =
  on, right = back), use the mouse wheel or drag to turn it. Ctrl+drag moves
  the window. Shown and hidden with the sidebar button.
  Shown or hidden with the Schusswahl (Firing selector) check box on the control
  panel.

Fire lever (T-Abf.-Schalter): a narrow window at the bottom right with a
  spring-loaded lever, Aus (Off) / Schuß (Fire). It rests at Aus; press on
  it (grip, slot or plates) to throw it to Schuß and fire the selected tubes;
  it stays down while the mouse button is held and springs back when you let
  go. It fires through the boat's own launch lever (which moves
  along), with the same checks: not while torpedoes are being launched, and
  every checked tube must be flooded.
  Ready lamp under the slot (hover it for what it shows right now and why):
    green  ready
    red    safe - the lever does not fire (stricter than the boat's own
           lever, which only refuses while launching or with a dry tube):
             - no tube selected, or a selected tube not flooded
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
  Shown or hidden with the Abfeuerhebel (Fire lever) check box.

Raise fully / lower fully: two slim buttons just above and below the stock
periscope raise/lower control. Click one to run the periscope all the way up
or down at its normal speed; click it again, or use a stock raise/lower
button, to stop. Hidden in the UZO and while the game UI is hidden.
(ElevatorButtons=false in the settings file removes them.)

Version history
  1.2  Voice callouts for the TDC dials, using the game's own crew voice
       lines.
       Window check boxes (FENSTER / WINDOWS) on the control panel to show or
       hide the TDC dials, firing selector, direction selector and fire
       lever.
       Target length (Streubreite) dial (turning ring) in the TDC dial
       window, shown for spreads; AOB needle with an arrow tip; distance
       dial as a knob with an arrow.
       Fire lever window (spring-loaded Aus / Schuß) with a ready lamp.
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

  The counters sit under the bearing tape. Their position is taken while the
  game UI is visible and then held, so hiding the game UI with Alt doesn't
  move them.

  TDC not manned: while nobody is working the TDC station, the TDC counter
  is greyed out with "TDC not manned" (Rechner nicht besetzt) under it, and
  the other counters are hidden. The controls keep working. (Not shown when
  "TDC operator required" is off.)

Gyro-set lamp
  green = the selected tube group's gyros are set to the TDC angle (within 3.5°)
  red   = synchronizing (the tube gyros are still turning to the TDC angle)
  dark  = the gyro receiver is off

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

  With "Auto set receiver" on (default), the receiver follows the CHECKED
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

  With "Auto set receiver" off, checking tubes never changes the receiver;
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
  backward for the stern tubes. While "Auto set parallax" is on, the
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
  Show receiver angle         show the BUG/HECK rows
  Show active receivers only  on  = only the selected receiver's row
                              off = bow and stern rows always, even when off
  Show parallax               show the Parallax row
  Auto set receiver           on  = receiver follows the checked tubes
                                    (see "Gyro receiver" above)
                              off = only you set the receiver
  Auto set parallax           on  = direction follows the checked tubes,
                                    else the receiver, else the target
                                    bearing (see "Parallax" above)
                              off = choose it yourself with VORN / ACHTERN
                                    (FORWARD / BACKWARD), in realistic and
                                    fast mode
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
  LeverOffsetX / LeverOffsetY         fire lever position (Ctrl+drag sets these)
  LeverX / LeverY                     fire lever base position (screen fraction)
  DirectionsX / DirectionsY            direction selector base position;
  DirectionsOffsetX / DirectionsOffsetY  (Ctrl+drag sets the offsets)
  Position=Below | Above              side of the bearing tape for the counters
  Scale=1.0                           size of counters and panel
  Format=360 | LR                     000-359, or L/R 000-180
  ShowTenths=true                     adds a tenths drum to every counter
  RollSpeed=90                        drum follow speed in deg/s (0 = instant)
  ShowSyncLamp / ShowControls / Tooltips
  ShowReceiverAngle / ActiveReceiverOnly / ShowParallax / AutoReceiver / AutoParallax /
  RealisticTdc / RealisticReceiverSync / SingleTorpedoLaunchFix /
  TdcOperatorRequired / VoiceCallouts   (also in the drop-down)
  ControlsAnchor=Elevator | Screen    Screen = place the panel at ControlsX/ControlsY
  Anchor=Compass | Screen             Screen = place the counters at FallbackX/FallbackY
  HideWithGameUI=false                true = also hide this mod's elements with Alt
  Language=Auto | DE | EN
  ToggleKey=F9                        optional show/hide hotkey
  DebugLog=true                       log every torpedo tube check-box change

Troubleshooting: search output_log.txt for "[ScopeTDC]". It logs the
detected language and where the counters and panel attached.
