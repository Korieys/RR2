# Handoff: Rule and Ruin space-strategy vertical slice (2026-10-16)

## What you can launch and play
`Builds/W1/RuleAndRuin.exe` (Windows x64, D3D12, Unity 6000.6.5f1, URP 17.6). It covers the full sequence: galaxy survey,
entering Aurelith, planet inspection, a management project, fleet orders, the Cräigon raid, battle preparation, an orbital
Commandfall battle on the real rules, the result applied to the galaxy, and save and load. See [README](../README.md) for controls.

**Important context:** this machine has no prior galaxy implementation (the "current Codex implementation" the brief refers
to was not found; see [DECISIONS](DECISIONS.md)). This is an isolated slice in `E:\ClaudeCode\RR3`. It vendors the Commandfall
rules engine unmodified and leaves Commandfall itself untouched. No before-and-after baseline exists for the galaxy, system or
planet scenes. Only orbital combat has a predecessor (Commandfall 0.15), shown in `Evidence/orbital_before_after.jpg` under
different conditions, so treat that image as illustrative rather than a matched comparison.

## Status by requirement

| Requirement | Status | Evidence or explanation |
|---|---|---|
| Survey the galaxy, territories, The Eclipse Sector | Implemented, verified | `Evidence/final__g01..g06`. 44 systems and 8 sectors positioned from faction map v2 (`Evidence/map_v2__*`). |
| Galaxy as a space scene, not a chart (overlays off) | Implemented, verified (session 2: fidelity plan, `final2__*`) | `g02`, `g04` (overlay off); `cv_galaxy_pan_*`, `cv_galaxy_orbit_*` (parallax from 70,000 world-space stars and a ray-marched nebula). |
| Galaxy to system transition and back, selection kept | Implemented, verified | `transition_*_strip.jpg`. The dive into the star, a captured-frame cross-fade, the same view direction in the system, and the galaxy bookmark restored on return. |
| Hero system: star, planets, rings, moons, station, two fleets | Implemented, verified | `s01`, `s02`, `cv_system_orbit_1`, `f01`, `r01`. The two fleets meet in-system during the raid. |
| Planet inspection carrying a close shot | Implemented, verified | `p01`, `p03`. Cubemap surfaces (no seams or pinching), the terminator tied to the star, clouds with shadows, city networks at night, an atmosphere limb. |
| One real management action | Implemented, verified | `p02`. Commissioning deducts the shown cost, takes its days, and the ship joins the fleet (tour log, unit tests). The foundry is an alternative choice with a different outcome (tested). |
| Fleet order, encounter | Implemented, verified | Hold: contact at Aurelith on day 16. Intercept: contact at Cinder Verge on day 9 on the Wreck Field (`tour_intercept_1440__*`). |
| Battle preparation with real constraints | Implemented, verified | `b01`. The budget of 30, 8 ships and one leader come from Commandfall's validator. Roles, movement, weapon, active and passive are shown, and the scouted enemy line is listed. |
| Deterministic orbital battle on actual rules | Implemented, verified | `b02..b05`. The previews use the same functions that resolve the attack. Replay from setup and command log gives an identical final state. Identical inputs gave identical digests across runs. |
| Leader kill, defended leader, blocked movement, repeated input | Verified by tests | `SliceDoctrineTests`. Impenetrable Front intercepts a lethal shot. Interception Snap correctly does not cover battleship salvos. Proximity alone protects nobody. |
| Consequence applied exactly once, persists through save and load | Implemented, verified | `b07`, `r01..r03`. The second apply is rejected. The save digest equals the digest after loading. An orbital win never transfers a planet; the orbit and a blockade are the only stakes. |
| Planetary Commandfall stays accessible | Verified | Settings launches Commandfall 0.15. Its `-cfRosterTour` passed: 0 problems, planetary and orbital replays matched. |
| Faction identity in ships | Implemented, needs creator approval | `ship_sheet_in_engine.jpg`. 20 codex ships, 2 tokens and the citadel are new art interpretations. |
| Interface at 1080p, at 1440p and with enlarged text | Verified | 1080p native. 2560x1440 offscreen with UI scale 1.25 (`tour_1440__*`); this caught and fixed an off-screen footer. A 2560x1080 window was also tested. |
| UI input capture, camera cancellation | Verified | Tour log: the pointer over the inspector blocks world input; an interrupted transition lands on its target. |
| Repeated transitions without leaks or duplicates | Verified | `-rrSoak`: 10 cycles, flat transform and material counts, native memory 391 to 380 MB. |
| Audio cues and beds, volumes, mute | Implemented, partly verified | 23 original clips are wired to events with duplicate suppression. Audibility was not verified by listening. |
| Reduced motion | Implemented, not visually verified | Transitions shorten to about 0.12 s and ambient motion slows. |
| Full diplomacy, technology, economy balance, landing and ground conquest stages | Out of scope | Per the brief. Conquest stops at stage 1, which is stated in the UI. |
| Manual deployment in battle | Out of scope | Both sides auto-deploy through Commandfall's own deployment rule. |
| Endless Space 2 and Stellaris gameplay inspection | Not done | No gameplay video was inspected; the references were used experientially from the brief only. |

## Measured performance (declared conditions)
RTX 5060 8 GB, i5-14400F, 32 GB, Windows 11 26200, D3D12, release (non-development) Mono player, quality "Ultra"
(the URP asset is the same on all levels), MSAA 4x, HDR, native render scale 1.0, vsync off (`-rrUncapped`). Frame times were measured
over 200 to 300 frames per bookmark in the final tour:

| Scene | 1920x1080 median / p95 / p99 | 2560x1440 offscreen median / p95 / p99 |
|---|---|---|
| Galaxy overview | 2.38 / 2.62 / 2.75 ms | 4.14 / 4.37 / 4.57 ms |
| Hero system | 1.69 / 1.97 / 2.00 ms | 2.95 / 3.27 / 3.86 ms |
| Planet inspection | 1.29 / 1.55 / 1.64 ms | 2.18 / 2.49 / 2.64 ms |
| Fleet close-up | 1.65 / 1.98 / 2.11 ms | 2.90 / 3.12 / 3.95 ms |
| Orbital battle | 1.66 / 1.96 / 2.44 ms | 2.86 / 3.09 / 3.20 ms |

These are the final session 2 numbers (galaxy fidelity plan complete; `Captures/final2`, `Captures/final2_1440`). Session 1
measured the overview at 2.27 ms (1080p) and 3.89 ms (1440p); a matched A/B puts the cost of all the galaxy work at about
+0.26 ms at 1080p.

* These are session 1 numbers (true 1920x1080). Later galaxy numbers are under "Faction map v2 layout"; the session 2 A/B
  under "Galaxy overview pass" was taken at 1600x900. Launch with `-screen-fullscreen 1 -screen-width 1920 -screen-height 1080`
  to be sure of the resolution; the tour log states it on its second line.
* The worst single frame was 12.8 ms in the battle at 1080p (the AI's decision frame). There are no recurring hitches. Garbage
  collection ran 0 to 3 times per sample window, with a 6 to 16 MB managed heap.
* Cold start includes baking the hero system's cubemaps (about 1 s, with a progress bar). Other systems bake on first entry.
* The synthetic stress fixture (1,000 systems, 200 fleets, 365 days) took a median of 8.6 ms and a p95 of 32.8 ms per simulated
  day; route planning dominates. This is a synthetic load test, not a forecast for a real campaign.
* The target of a stable 60 fps at 1080p is met with a wide margin on this machine.

## Tests
`Tools/SliceTests`: **167/167 pass**. That is 146 vendored Commandfall rules tests and 21 slice tests, covering legal and
illegal orders, management, contact, interception, retreat, selection constraints, determinism, exactly-once results, carried
damage, save and load (mid-campaign, mid-engagement, foreign or future files), leader elimination, defended leaders,
non-protection by proximity, blocked movement, repeated-input rejection, and preview equals resolution.

## Review passes (my own, not a substitute for the creator's approval)
1. **v1:** the galaxy nebula was an overexposed opaque fog, the Eclipse Sector a black sticker, the transition frame upside
   down and showing the UI twice, the star blown out, the battle planet acting as wallpaper, and a loading bar stuck on screen.
   All fixed.
2. **v2 and v3:** structured nebulae with dust lanes, the Eclipse rework (patchy dust, cold rim, its own stars), a regional
   galactic band in system skies, the battle planet as a side crescent, anti-aliased coastlines, and an inspector layout bug
   fixed (rows collapsing).
3. **Assets:** the Cräigon ships were identical dark pills. They were rebuilt with an arched segmented carapace, a claw maw,
   pincers, a crest, pods and a dome.
4. **1440p with enlarged text:** the battle preparation footer was off screen. Fixed.

## Known weaknesses (unresolved)
* Galaxy view (session 2, fidelity plan complete): the 1440p overview is 4.14 ms median, slightly over the 4 ms 1080p target;
  emblems are provisional crops of the map legend (low resolution) until the creator's artwork arrives; the Craigon incursion
  routes are rough tracings; the galaxy bake adds about 4 s at first load (cached afterwards); star clusters near the heart and
  the optional map legend panel were not done.
* The star surface is restrained and a little flat at system scale. Its corona is modest by design, to protect UI contrast.
* The planet backdrop in battle is magnified past the 1024 cubemap resolution, so coastlines soften rather than resolve.
* The Silex small ships share one silhouette family; capital ships differ more. The Cräigon auxiliary hulk token is crude.
* Ship scale in the system view is a deliberate exaggeration; the fleet close-up still reads small next to the planet.
* There is no in-battle camera cinematics and no per-ability effects (abilities use generic flashes and floating text).
* UI icons are simple vector glyphs, with no faction emblems; the approved insignia were not available as vector files.
* The transition cross-fade shows a brief double image (the galaxy frame fading over the system arrival).
* The heavy gold tint on selected ship rows in battle preparation could be lighter.

## Galaxy overview pass (2026-10-16, session 2)
Goal: make the far overview read as a space scene rather than an illuminated map, keeping the regional layout and the Eclipse
Sector. Code: `Assets/RR/Game/GalaxyPresentation.cs` (`GalacticStructure`, `LookFor`, `BuildVolume`/`BakeDensity`, `BuildStars`),
`RR_GalaxyVolume.shader` (emission decode), `RR_Territory.shader` (distance fade).

* **Galactic anatomy under the sectors.** A warm stellar bulge around the core worlds (Meeples), an exponential disk, two loose
  spiral arms and dust rifts on their inner edges. Cloud brightness follows this structure, so Meeples is lit and the rim
  sectors (Silex, Pygmaei, Pirate Reach) fall off into the dark.
* **Per-sector character** (`LookFor`): brightness, filament strength, dust and 2 to 4 bright star-forming knots per sector.
  Inhumanus has dense knots, Pirate Reach is dim and dusty, Silex is filamentary.
* **No outline.** The bright rim along each sector edge (the strongest map cue) is reduced to a broken trace on filaments.
  Clouds now thin out towards their edge, and neighbouring colours blend across borders.
* **Dark rifts.** Large coherent dark clouds cut across sector borders and also thin the star field.
* **Stars follow the galaxy, not the political blobs.** Density rises toward the bulge and along the arms; the bulge population
  is warmer and thicker, arm stars are bluer.
* **Territory overlay at the overview.** Fill and soft halo fade out from about 380 units of camera distance; the fine border
  line stays. Close views are unchanged.
* **Resolution and range.** The density volume went from 192x20x144 to 384x24x288 (10.6 MB), with square-root encoded
  emission over [0, 2.5] so faint gas keeps precision and knots do not clip.
* **The Eclipse Sector** keeps its dark, patchy dust and cold rim (the rim is 25% softer so it reads less like a drawn ring
  against the now darker surroundings). Its stars are unchanged.

Evidence: `Evidence/galaxy_before_after__g01_galaxy_overview.jpg`, `__g02_galaxy_overview_no_overlay`, `__g05_frontier_approach`,
`__cv_galaxy_pan_0`, `__cv_galaxy_orbit_1` (before on top, after below), and the after set `Evidence/galaxy_v2__*`. Full
captures: `Captures/galaxy_final` (the full tour, 47 shots) against `Captures/final`.

Performance, matched A/B on this machine on the same evening, **1600x900, not 1080p** (correction: the player had kept a
1600x900 window from an earlier run; this was found during the map v2 work, and true 1080p numbers are under "Faction map v2
layout" below), `-rrUncapped`, three alternating runs each of the HEAD
baseline (`Builds/W1_base`) and the new build (`Builds/W1`): galaxy overview median 1.77 / 1.74 / 1.87 ms before and
1.76 / 1.78 / 1.81 ms after, so no measurable cost (p95 was noisy at 4.1 to 5.2 ms in both builds because of background load
on the machine that evening). One full tour run that evening was 2 to 3 times slower in every scene, including scenes this
pass does not touch; it was discarded as contaminated.

Costs and trade-offs:
* The volume bake at startup grew from roughly 0.4 s (estimated) to 1.9 s (measured, Mono player, 10 cores). The baked texture
  is now cached across galaxy rebuilds when the sector layout is unchanged, so loading a save no longer bakes again (the tour
  log shows one bake; the save and load digests still match).
* The close galaxy views (`cv_galaxy_*`) are moodier than before: darker gaps between the clouds and less saturated cloud
  in the Silex home region. This is a judgement call for the creator; raise `LookFor("silex_expanse").Bright` for a richer look.
* The bulge sits under the MEEPLES label. It was dimmed until the label reads, but it is the brightest thing on the map.

Follow-ups noticed, not done: the territory texture (512x384) shows stair-stepped borders in close views (`g03`, `g05`);
the Eclipse cold rim still reads as a closed loop from the overview.

## Faction map v2 layout (2026-10-16, session 2, fidelity plan phase 1)
The creator confirmed the updated faction map (`Reference/faction_map_v2.webp`) as the canonical layout. See
[GALAXY_FIDELITY_PLAN.md](GALAXY_FIDELITY_PLAN.md) for the remaining phases.

* **Layout:** The Eclipse Sector is now the centre of the galaxy (oval, about 240 x 215 map px) with the galactic heart north-east
  of its middle; Meeples moved to the south-east as a wide oval; the Elven Front was re-traced. Silex, Pygmaei, Pirate Reach,
  Inhumanus and Craigon are unchanged. Sectors gained an `Aspect` (oval) field.
* **Systems and routes:** the Meeple Core ring moved with Meeples, Concord moved into Meeples, and Lantern, Accord and Bastion
  were added there; the Eclipse has seven systems (Umbra, Gloam, Nocturne, Penumbra, The Quiet, Vesper, The Hollow Star). Routes
  between the west and Meeples now cross the Eclipse. All new names are provisional (DECISIONS).
* **Map features as data** (`MapFeature`, presentation only): the galactic heart, the Craigon origin vortex, two disputed zones
  and three Craigon incursion routes (traced roughly, provisional). They are saved; nothing draws the zones, routes or vortex yet
  (phases 3 and 4).
* **Galaxy view:** the galactic structure is centred on the heart, and the old Meeples bulge is gone. The heart is a placeholder
  (an opaque column with a thin violet corona) until phase 2; it looks blocky in close views (`map_v2__g03`). The Craigon band
  follows the map's southern rim.
* **Tour:** new straight-down shots `g00_map_frame` (overlay on and off) for registration; the Eclipse shot is framed wider.
  `Evidence/map_v2__registration_overlay.jpg` lays the map over `g00_map_frame` at the camera scale: the heart, sector shapes,
  the origin system and the labels line up (the Meeples title sits about 40 px lower than on the map, at the sector centre).
* **Unchanged gameplay:** the raid still makes contact on day 16 at Aurelith; save and load digests match; tests 167/167 (the
  reachability test covers the new routes).
* **Saves:** still format version 1. Aspect and features are optional; older saves load with their own v1 geometry.

Performance at true 1920x1080 fullscreen, matched A/B, three alternating runs each: galaxy overview median 2.35 / 2.44 / 2.40 ms
for the HEAD baseline (session 1 code) and 2.41 / 2.57 / 2.47 ms for this build (session 2 pass plus the v2 layout), about
+0.08 ms, inside the run-to-run noise. Full tour at 1080p: hero system 1.54, planet 1.43, fleet close 1.72, battle 1.73 ms median.

Evidence: `map_v2__registration_overlay`, `map_v2__g00_map_frame` (and `_no_overlay`), `map_v2__g01` to `g05`,
`map_v2__cv_galaxy_pan_0`, `map_v2__r02_after_battle_galaxy`, and `map_v2_before_after__g01`, `__g02`, `__g03` (top: the session 2
pass on the v1 layout, captured at 1600x900; bottom: v2 layout at 1080p). Full captures: `Captures/phase1`.

## Nebula detail and chart layer (2026-10-17, fidelity plan phases 3 and 4)
The creator skipped phase 2 (the Eclipse heart) and asked for the black disc to be removed; it is gone. The galactic structure
stays centred on the heart position.

**Phase 3, nebula detail** (`RR_GalaxyVolume.shader`, `GalaxyPresentation.DetailNoise`, `NebulaColor`):
* A tiling 64-cubed detail noise texture (billow, ridges, slow variation) is applied once per pixel where the ray crosses the
  galactic plane, in two rotated, warped layers. Faint gas breaks into frilly wisps, bright cores vary gently, and no tile grid
  shows. A first version sampled it at every ray step: 6.85 ms at 1080p and vertical streaks, so it was moved to the plane pass.
* The ray march step count now follows the path length (about 1.5 world units per step, capped at 48).
* Nebula colours per sector follow the faction map (deeper Silex blue, saturated Pygmaei violet, warm Pirate gold, a true blue
  for Meeples instead of teal). Overlay colours stay the faction accents. Emission went from 1.5 to 1.9 and the gas sheet is
  thinner.
* The Craigon origin is a small two-armed purple vortex in the volume.

**Phase 4, chart layer** (`RR_Territory.shader`, `BakeTerritory`, `BuildIncursions`, `StrategyHud.AddEmblem`):
* Territory borders come from a signed distance field (768 x 576; exact Euclidean distance transform, refined to sub-texel
  precision at the edge). Each sector draws a crisp, glowing, anti-aliased line of constant pixel width just inside its edge,
  frilled by the nebula detail noise, so neighbours show two coloured lines with a dark gap, as on the map. The old
  stair-stepped 512 x 384 borders are gone.
* Sector titles as the map sets them: emblem, title, italic subtitle (the Eclipse reads "(Unexplored Space)"). The emblems are
  **provisional crops of the map legend** in `Assets/Resources/RR/Emblems/<polity id>.png`; replacing those seven PNGs with the
  creator's artwork is all that is needed.
* Major routes in long dashes, minor routes in short ones; red Craigon incursion routes with flowing dashes and glowing nodes;
  hatched disputed zones; capital worlds (Lithar, Origin Brood, Meeple Core, Forgefall, Blackwake, Ledgerhold, Aethel Spire)
  get a double ring.
* **Zoom clearing** (creator request): zoomed in on a fleet or system, the territory mist thins around the camera focus (none
  above 220 units of camera distance, about 80% by 60 units), so units stay clear (`phase4__g06b_fleet_zoomed`).
* A debug switch `-rrDumpTerritory <png>` writes the baked distance field as an image.

Startup: the territory bake takes about 2.4 s and the volume about 2 s; both are cached across galaxy rebuilds (loading a save
reuses them).

Verification at true 1920x1080 fullscreen: full tour passed (raid contact day 16, save and load digests equal), tests 167/167.
Matched A/B, three alternating runs each against the session 1 baseline: galaxy overview median 2.31 / 2.35 / 2.44 ms before,
2.41 / 2.54 / 2.47 ms now (about +0.1 ms). Full tour medians: overview 2.47, hero system 1.64, planet 1.40, fleet close 1.68,
battle 1.76 ms.

Evidence: `phase3_before_after__*` (phase 1 on top, phase 3 below, overlays off where noted), `phase4__*` (after),
`phase4_before_after__g01`, `__g03`, `__g05` (phase 1 on top), `phase4__registration_overlay` (map v2 over the straight-down
frame). Full captures: `Captures/phase3`, `Captures/phase4`.

Known issues: the Inhumanus title runs under the right-hand inspector at the default overview framing; emblems are low
resolution (about 60 px source) until the real artwork arrives; the incursion routes are rough tracings.

## Stars and system markers (2026-10-17, fidelity plan phase 5)
* **Dashed-star artifact fixed** (`RR_SpaceSky.shader`): the sky star layers sized stars with `fwidth(dist)`, which jumps at
  every lattice-cell boundary and lit whole pixel rows along cell faces (short horizontal and vertical dashes). The footprint now
  comes from the continuous lattice coordinate. Before and after: `phase4__sky_dashes_crop.png`, `phase5__sky_no_dashes_crop.png`.
* **Marker halos and diffraction spikes** (`RR_Marker.shader`): every system has a soft outer halo in its star colour; the seven
  capitals carry long four-point spikes with faint diagonals, the hero system shorter ones (`phase5__capital_marker_crop.png`).
  The quad is enlarged (`_Extent`) so halos and spikes are never cut off. Incursion nodes stay spike-free.
* **Distant galaxies** in the sky: 40 faint spirals and ellipticals at fixed, hash-chosen directions, sizes and tilts, generated
  once on the CPU (`Look.SeedDistantGalaxies`). They show mostly in system views, where the sky is open (`phase5__cv_system_orbit_1`);
  in the galaxy overview the disk covers most of the sky. A first version generated them per pixel in the shader and cost about
  0.5 ms; the CPU version costs almost nothing.
* **Twinkle:** the brighter ambient stars twinkle slowly. A new global `_RR_Motion` (0.2 with reduced motion) scales ambient
  shader motion; the twinkle uses it. (`_RR_Time` itself is not paused by reduced motion, contrary to an old comment, now fixed.)

Verification at true 1080p: full tour passed (raid contact day 16, save and load digests equal). Matched A/B, three alternating
runs: galaxy overview median 2.19 / 2.14 / 2.16 ms for the session 1 baseline, 2.39 / 2.39 / 2.37 ms now (+0.22 ms for all
galaxy work since session 1). Full tour medians: overview 2.38, hero system 1.69, planet 1.29, fleet close 1.65, battle 1.67 ms.
Strategy code is unchanged in this phase, so the tests were not re-run (167/167 at phase 4).

Evidence: `phase5__*`, `phase5_before_after__g05_frontier_approach`, `__s02_hero_system_no_overlay` (phase 4 on top).
Full captures: `Captures/phase5`.

## Motion, grading and camera feel (2026-10-17, fidelity plan phase 6)
* **Galaxy grade** (`GameRoot.BuildGalaxyGrade`): a second global post volume layered over `RR_PostFX` for the galaxy view only
  (contrast +12, saturation +12, exposure +0.25, bloom threshold 0.95 and intensity 0.7, vignette 0.3). Its weight fades in
  and out with the view scale over about 0.35 s (0.1 s with reduced motion). A first version with a negative lift crushed the
  faint gas (the Eclipse rim and Pirate Reach nearly vanished) and was removed; the grade is deliberately gentle.
* **Ambient motion:** the two nebula detail layers drift slowly against each other, so the wisps evolve; capitals breathe with
  a slow 12% pulse (about 4.5 s); the bright-star twinkle and the incursion-route flow already existed. All of it is scaled by
  `_RR_Motion` (0.2 with reduced motion).
* **Camera feel** (`CameraRig`): in the galaxy view the pitch follows the zoom, steep from far out (62 degrees at the home view)
  and flatter close in (40 degrees at 60 units), applied as a change so a pitch chosen by orbiting is kept. A drag ends with a
  short glide (eases out over about 0.15 s, capped at about 0.14 x the camera distance; none with reduced motion). Wheel zoom
  eases a little longer. The tour checks both: zooming 720 to 147 moved pitch 62.0 to 42.8 degrees; a brisk drag
  (700 px/s for 0.3 s) glided 19 units at distance 147.

Verification at true 1080p: full tour passed (raid contact day 16, save and load digests equal). Matched A/B, three alternating
runs: galaxy overview median 2.16 / 2.17 / 2.16 ms for the session 1 baseline, 2.43 / 2.43 / 2.41 ms now (+0.26 ms for all
galaxy work since session 1). Full tour medians: overview 2.41, hero system 1.71, planet 1.30, fleet close 1.64, battle 1.66 ms.
Strategy code is unchanged in this phase (tests 167/167 at phase 4).

Evidence: `phase6_before_after__*` (phase 5 on top), `phase6__g01_galaxy_overview`. Full captures: `Captures/phase6`. Motion
and camera feel do not show in still images; they need a playtest.

## Final verification (2026-10-17, fidelity plan phase 7)
* **1920x1080 fullscreen tour** (`Captures/final2`): passed. Raid contact on day 16 at Aurelith; save and load digests equal;
  UI input capture and camera cancellation checks true; camera feel checks as in phase 6.
* **2560x1440 offscreen, UI scale 1.25** (`Captures/final2_1440`): passed with the same checks. It found one layout bug: the
  HUD panels are translucent, so world labels behind them (the Inhumanus and Meeples titles) showed through the inspector.
  Labels now treat visible panels as occupied space and hide behind them (`StrategyHud.UpdateLabels`).
  The galaxy overview at 1440p is 4.14 ms median, just over the 4 ms target set for 1080p (1080p is 2.38 ms).
* **Soak** (`Captures/soak2`, 10 galaxy, system and planet round trips): flat transform (178) and material (205) counts every
  cycle, native memory 398 to 380 MB, managed 6 MB. The counts are higher than session 1 (132 and 181) because of the new chart
  layer objects (incursion nodes, capital rings, major lanes), not growth.
* **Tests:** 167/167.
* **Side by side with the faction map:** `Evidence/final2__map_side_by_side.jpg` (overlays on) and `_no_overlay.jpg`, the
  straight-down 1080p frame cropped to the map at the same scale. Layout, sector shapes, emblems, titles, routes, incursions and
  disputed zones correspond. Differences, all known: no in-engine eclipse disc (skipped by the creator), the map's spiral at the
  Craigon origin is subtler in engine, the map draws many more star nodes and red incursion nodes, and its subtitles are longer
  (the engine uses the short taglines).
* **Session 1 against now:** `Evidence/final2_session1_vs_now__g01_galaxy_overview.jpg` and `__g02_..._no_overlay.jpg`.

## Dependencies and next steps
* The creator's aesthetic approval of the ship interpretations, the Eclipse placement and the provisional names.
* The location of the original Codex strategy implementation, if it exists elsewhere, for a true comparison.
* The final insignia and the faction emblem vector art; licensed or composed music (the FL Studio battle theme exists only as a
  project file).
* Decide whether the slice's strategic layer should merge into the Commandfall repository (one game) or stay separate.