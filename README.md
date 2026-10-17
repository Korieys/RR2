# Rule and Ruin: space-strategy vertical slice

A playable Unity 6 (URP) slice that runs from the galaxy, into the Aurelith system, to an orbital Commandfall battle fought
on the real Commandfall rules, and back again. Start with [Docs/HANDOFF.md](Docs/HANDOFF.md); the continuation record is
[Docs/PROGRESS.md](Docs/PROGRESS.md).

## Play it

```bash
./Builds/W1/RuleAndRuin.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

The playable sequence takes 5 to 10 minutes:
1. **Survey the galaxy.** Seven sector nebulae sit where they do on the approved faction map, with The Eclipse Sector south of the
   Meeple core. Tab toggles the strategic overlay (labels, lanes, borders).
2. **Enter Aurelith.** Double-click the system or use "Enter system". Inspect Aurelith Prime, then commission a ship or raise a
   Crystal Foundry; costs and build times are shown.
3. **Command the First Crown Fleet.** Review its ships, then hold at Aurelith or right-click Cinder Verge to intercept
   (or click "Order a move"). Press Space or 1, 2 or 3 to run time.
4. **Contact.** The Cräigon brood leaves Spore Drift on day 3. Time stops at contact. Choose your battle line within the
   30-point, 8-ship limit (the Throne Ark leads), or withdraw.
5. **Orbital Commandfall.** Select a ship: gold hexes are legal moves and red hexes are legal targets. Hover a target for the
   exact deterministic result, press 3 for the ship's ability and Space to wait. Destroying the enemy flagship ends the battle.
6. **The consequence persists.** Losses and damage carry over, the orbit is held or lost, and F5 and F9 save and load.

Controls: left click selects, double click enters, right click orders the selected fleet, wheel zooms, right or middle drag
orbits, left drag or WASD pans, Q and E rotate, Home recenters, Escape returns, Tab toggles the overlay. Settings (gear) has
reduced motion, interface size, volumes, mute, and a launcher for planetary Commandfall.

## Build and test

```bash
"/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod RR.EditorTools.BuildTools.BuildWindows -rrSetup -rrBuildOut Builds/W1/RuleAndRuin.exe -logFile Logs/build.log
```

```bash
cd Tools/SliceTests && "/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe" build -c Release && "/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe" bin/Release/net8.0/SliceTests.dll run
```

```bash
"/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe" Tools/SliceTests/bin/Release/net8.0/SliceTests.dll stress 1000 365
```

Regenerate assets:

```bash
"/c/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Tools/Blender/rr_ships.py -- [--only id,id] [--no-bake] [--render]
```

```bash
python Tools/Audio/rr_audio.py
```

If a build fails with `CreateDirectory ... PlayerDataCache ... Access is denied`, delete `Library/PlayerDataCache` and build again.

## Automation flags (player build)

| Flag | Effect |
|---|---|
| `-rrTour <dir>` | The whole sequence through the real game. Captures every bookmark, transition frames, chart-versus-space pans and orbits, battle, result, save and load, plus frame-time samples and a `tour_log.txt`. `-rrTourPlan intercept` plays the other legal choice. |
| `-rrOffscreen 2560x1440` | Renders scene and interface offscreen at that size; shots are composited (both displays here are 1080p). |
| `-rrUiScale 1.25` / `-rrReducedMotion` / `-rrUncapped` | Enlarged text, reduced motion, vsync off for measurements. |
| `-rrShipSheet <dir>` | Every model in-engine, two fixed views each. |
| `-rrSoak <dir>` | Ten galaxy, system and planet round trips, logging object and memory counts. |
| `-rrCapture <dir>` | Fixed hero-system bookmarks (Stage B comparison set). |
| `-rrCommandfallExe <path>` | Path to the planetary Commandfall build for the Settings launcher. |

## Layout

```
Assets/RR/Strategy   engine-free strategic layer: model, scenario, rules, battle bridge, save format
Assets/RR/Game       composition root and flow, camera, galaxy, system and battle presentation, planet baker, UI, audio, automation
Assets/RR/Shaders    planet, atmosphere, ring, star, corona, ship, sky, star field, galaxy volume, lanes, markers, territory, hex grid, haze, fx
Assets/RR/Vendor     Commandfall Core, AI and EditMode tests, vendored unmodified from Commandfall 5a31053
Assets/RR/Tests      slice tests (also run by Tools/SliceTests)
Assets/RR/Art        Blender-generated FBX models          Tools/Blender: generators and the editable .blend
Assets/RR/Audio      synthesised WAV cues and beds          Tools/Audio: generator
Docs                 handoff, decisions, references, provenance, progress, evidence
```