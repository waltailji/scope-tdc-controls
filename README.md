# Scope TDC Controls

A mod for **UBOAT** (built for 2026.1 patch 21) that adds SH3-style TDC readouts and fire-control controls to the periscope and UZO views, inspired by the gyro angle displays of GWX and OneAlex.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3843459058

![Preview](mod/Preview.jpg)

## Features

### Rolling drum counters
Compact mechanical-style counters under the bearing tape. Every drum rolls over like a real odometer, including 359 → 000.

- **TDC**: the gyro angle (Schusswinkel) computed by the TDC
- **Parallax**: the parallax correction the TDC adds (+/-)
- **Bow / Stern**: the angle the tube gyros are actually set to
- **Lamps**: a blue follow lamp on the TDC counter (click to switch follow-up), and a gyro-set lamp (green = set, red = synchronizing) on the selected receiver's counter
- Greyed out with "TDC not manned" while nobody is at the TDC
- **Double-click a Bow/Stern counter** to switch to that receiver: the scope swings until the TDC angle matches what those tubes are already set to

### Control panel and windows
A panel titled "Scope TDC Controls" beside the periscope raise/lower control (parallax mode, window buttons, settings), plus off-white windows styled after the boat's control boxes:

- **Target bearing** (on the panel): Folgen / Nicht folgen, blue lamp while not following
- **Direction selector**: rotary switches for the bearing source (Zielrichtung: Off, UZO, observation or attack periscope) and the gyro angle receiver (Schußrichtung: Off, Bow, Stern)
- **Parallax mode** (on the panel): Forward or Backward
- **Windows**: show or hide the gyro counters, TDC dials, firing selector, direction selector and launch lever; reset their sizes or positions
- Shown or hidden with a button added to the periscope screen's stock left sidebar
- Windows that end up off screen (for instance after a resolution change) are moved back on screen by themselves

### TDC dials
A window on the left of the scope with the TDC's **AOB**, **target speed**, **target distance** and **target length** dials (target length only while a spread is selected). Grab a dial and turn it, just like on the TDC; Shift for fine adjustment, mouse wheel for steps, exact readout under each dial. Shown and hidden with the sidebar button together with the panel. With **Voice callouts** on, the crew member at the scope announces what you dial in using the game's own voice lines, as when sending a locked target to the TDC.

At the bottom, **time to impact** (Laufzeit): three drums with the torpedo's run in seconds from launch to the point the TDC aims it at, worked out from the TDC's entries (bearing, distance, AOB, target speed, torpedo speed and gyro angle, including the torpedo's 9.5 m straight run and 95 m radius turn onto its course), timed at the selected torpedoes' speed (the slowest, for a salvo). With no tube selected it keeps the last time. The drums use the game's Roboto Black font; `ImpactTimeFont` in the settings file picks another of the game's fonts, and `ImpactTimeWeight` (-1 to 1) makes the digits thinner or bolder (default -0.3).

### Firing selector
The three rotary switches of the boat's Schaltkasten T-Abf.-Anl. in a window near the bottom of the scope: **Einzelschuß** (single tube / spread), **Fächerschuß** (salvo tubes) and **T.Abf. Schalter** (firing station). Each offers exactly your sub's positions and works like the knob in the boat.

### Launch lever
A spring-loaded Aus / Schuß lever: pull it to Schuß to fire the selected tubes through the boat's own launch lever. A ready lamp shows whether the selected tubes are flooded and the gyro angle receiver is on their end.

### Torpedo run timers
Silent Hunter style: each torpedo launched gets a row near the top of the screen with a torpedo and a clock counting down from the time-to-impact value at launch. Several torpedoes stack, newest at the bottom; at 0:00 a row fades out over a minute. They show in every view, count in game time (time compression speeds them up), and can be dragged anywhere. **Double-click a timer** to watch that torpedo in the game's orbit camera, as with the camera button for a torpedo selected on the map; on a hit the view moves to the ship it hit, after a miss it goes back to where you were. Double-click again to go back sooner. Setting: **Torpedo run timers**.

### Raise fully / lower fully
Two buttons above and below the stock periscope raise/lower control run the periscope all the way up or down at its normal speed.

### Settings (saved between sessions)
Show gyro receiver angles · Show active receivers only · Show parallax angle · Auto set gyro receiver · Auto set parallax mode · Realistic TDC follower · Realistic receiver sync · Single torpedo launch fix · TDC operator required · Voice callouts · Enhanced torpedo tube handling · Torpedo run timers

### Fixes to the base game
- **Single torpedo launch fix**: a tube checked a few seconds after pressing fire is no longer launched too
- The torpedo panel no longer forces the gyro receiver to **Bow** whenever a bow tube is flooded
- **Not following** holds while a target is locked
- Hiding/showing the torpedo panel no longer changes the tube selection; the fire-control box tube selector stays in step
- A torpedo tube **outer door saved part open** (a save made while a torpedo was leaving) is closed again when the game loads

English and German (follows the game's language), pop-up descriptions on every element, Ctrl+drag to reposition. See [`mod/README.txt`](mod/README.txt) for full details and all settings.

## How it works

Everything is done at runtime: UBOAT compiles the C# files in `mod/Source` when the mod loads, and the mod applies [Harmony](https://github.com/pardeike/Harmony) patches in memory. No game files are modified, and nothing is stored in save games. Settings live in `scope-tdc-controls-config.txt` in `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\`.

## Repository layout

```
mod/            the mod itself (this is what goes to the Workshop)
  Manifest.json
  README.txt    player documentation
  Preview.jpg   Workshop thumbnail
  Source/       C# source, compiled by the game
workshop/
  description.bbcode.txt   Steam Workshop page description (Steam BBCode)
```

The UBOAT Launcher replaces the Workshop description with the plain one from `Manifest.json` on every update, so paste `workshop/description.bbcode.txt` back into the Workshop page afterwards.

### Developing
Keep the repo anywhere outside the game folders and point the game's mod folder at `mod/` with a directory junction, so `.git` never ends up in a Workshop upload (the UBOAT Launcher uploads every folder inside the mod):

```powershell
New-Item -ItemType Junction -Path "$env:USERPROFILE\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\scope-tdc-controls" -Target "<repo>\mod"
```

Then upload or update the Workshop item from the UBOAT Launcher as usual.

## License

[MIT](LICENSE)
