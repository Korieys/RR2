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
| Survey the galaxy, territories, The Eclipse Sector | Implemented, verified | `Evidence/final__g01..g06`. 37 systems and 8 sectors positioned from the approved map. |
| Galaxy as a space scene, not a chart (overlays off) | Implemented, verified | `g02`, `g04` (overlay off); `cv_galaxy_pan_*`, `cv_galaxy_orbit_*` (parallax from 70,000 world-space stars and a ray-marched nebula). |
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
| Galaxy overview | 2.27 / 2.85 / 3.06 ms | 3.89 / 4.15 / 4.33 ms |
| Hero system | 1.55 / 2.59 / 2.93 ms | 2.77 / 3.04 / 3.51 ms |
| Planet inspection | 1.24 / 3.23 / 3.60 ms | 2.11 / 2.82 / 3.79 ms |
| Fleet close-up | 1.73 / 3.89 / 5.76 ms | 2.56 / 2.81 / 3.40 ms |
| Orbital battle | 1.76 / 3.48 / 4.13 ms | 2.67 / 2.99 / 3.28 ms |

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
* The galaxy overview still reads partly as a map. Its sectors are evenly bright, organic blobs, close to the approved map's
  style. Up close it is a convincing space scene; from far out it is closer to an illuminated chart.
* The star surface is restrained and a little flat at system scale. Its corona is modest by design, to protect UI contrast.
* The planet backdrop in battle is magnified past the 1024 cubemap resolution, so coastlines soften rather than resolve.
* The Silex small ships share one silhouette family; capital ships differ more. The Cräigon auxiliary hulk token is crude.
* Ship scale in the system view is a deliberate exaggeration; the fleet close-up still reads small next to the planet.
* There is no in-battle camera cinematics and no per-ability effects (abilities use generic flashes and floating text).
* UI icons are simple vector glyphs, with no faction emblems; the approved insignia were not available as vector files.
* The transition cross-fade shows a brief double image (the galaxy frame fading over the system arrival).
* The heavy gold tint on selected ship rows in battle preparation could be lighter.

## Dependencies and next steps
* The creator's aesthetic approval of the ship interpretations, the Eclipse placement and the provisional names.
* The location of the original Codex strategy implementation, if it exists elsewhere, for a true comparison.
* The final insignia and the faction emblem vector art; licensed or composed music (the FL Studio battle theme exists only as a
  project file).
* Decide whether the slice's strategic layer should merge into the Commandfall repository (one game) or stay separate.