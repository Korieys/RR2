# Galaxy view: visual fidelity audit and plan (2026-10-16)

Reference: `Docs/Reference/faction_map_v2.webp` (the updated faction map, 1448 x 1086, same frame and scale bar as v1, so its
pixel positions drop straight into `SliceScenario`). Previous map: `Docs/Reference/faction_map_v1.png`.
Current build: `Captures/galaxy_final`, `Docs/Evidence/galaxy_v2__*`.

## 1. Summary
The updated map changes the geography, not just the look: **The Eclipse Sector moves to the centre of the galaxy and grows
to roughly twice its size, Meeples moves to the south-east (where the Shattered Veil was), and the Craigon influence now runs
along the southern edge and into the Eclipse.** The galaxy view has to adopt that layout before any polish is worth doing.

The premium target has two layers that must not be confused:
* **The space layer** (overlays off) is the physical galaxy: nebulae, dust, stars, and the Eclipse as the galactic heart.
  It must read as a place, never as a chart.
* **The chart layer** (overlays on) is the faction map brought to life: glowing territory borders, emblems, titles and
  subtitles, capital worlds, jump routes, incursion routes and disputed space. This is where the map look belongs, and
  today it is the weakest part of the view.

Budget: the overview costs 1.8 ms at 1080p today (matched A/B), so about 2.2 ms remains under the 4 ms ceiling.

## 2. What the updated map changes

| Element | Current scenario (v1 map) | Updated map (v2) |
|---|---|---|
| The Eclipse Sector | Provisional, south-central, centre (700, 880), radius 120 px | **Centre of the galaxy**, about (730, 500), roughly 270 x 185 px; a black eclipse disc with a violet corona at its heart, swirling dark-violet nebula, violet routes inside |
| Meeples | Centre, (760, 580), radius 230 | **South-east**, about (1085, 715), radius about 210; Meeple Core cluster about (1090, 790) with Unity, Hope, Commonweal, Hearth around it |
| The Shattered Veil | Already removed in data | Gone from the map too (confirms the earlier decision) |
| Elven Front | (1060, 480), radius 120 | About the same, slightly smaller, now between Inhumanus and Meeples |
| Craigon influence | Band across the south-west to south-centre | Magenta band along the southern edge to about (770, 880), plus red incursion routes reaching into the west of the Eclipse |
| Craigon Homeworlds | Origin Brood system | Drawn as a **small purple spiral vortex** at about (105, 565), "Origin Sector" |
| Disputed space | Shader hatch, barely visible | Hatched zones between Elven Front and Meeples (about 1030, 545) and south-west of Meeples (about 890, 800) |
| Silex, Pygmaei, Pirate Reach, Inhumanus, Craigon | Unchanged | Unchanged (Silex and the raid route stay where they are) |

Gameplay check: the scripted raid route (Spore Drift, Cinder Verge, Aurelith) is entirely in the west and does not move, so
contact days (hold day 16, intercept day 9) are unaffected. The EditMode tests check reachability and the Eclipse by name,
not coordinates. Lanes touching Meeples and the Eclipse must be re-authored; every system must stay reachable.

## 3. Audit: current build against a premium target
Overview scale: camera 720 units away, 40 degree field of view, so about 2 screen pixels per world unit at 1080p.

| # | Area | Today | Gap | Severity |
|---|---|---|---|---|
| A1 | Layout | v1 geography; Eclipse south, Meeples centre; the bulge glow sits under Meeples | Does not match the approved map | Blocking |
| A2 | Territory borders | 512 x 384 owner texture (1.76 units, about 3.6 px per texel); 1 px stair-stepped lines; two colours double up where sectors meet (`crop_border`, `g03`, `g05`) | The cheapest-looking element on screen; the map has smooth, glowing, fractal edges | High |
| A3 | Sector titles | Plain serif name only | The map pairs an emblem, a spaced-caps title and an italic subtitle; no emblems exist in the build | High |
| A4 | Eclipse | A dark disc of dust with a cold rim that reads as a drawn loop from far out | The map makes it the focal point: a black disc, a bright violet corona, dust spiralling inward | High |
| A5 | Nebula detail | Volume 384 x 24 x 288 (2.3 units, about 4.8 px per voxel); soft at the overview, no fine wisps | The map has crisp filaments and cauliflower edges; needs sub-voxel detail | High |
| A6 | Stars and systems | System markers are small soft dots; capitals look like any other system; some ambient stars render as 1 to 3 px dashes (artifact, `crop_grain`) | Capitals need a ring glyph; bright stars need halos and spikes; the dash artifact must go | Medium |
| A7 | Jump routes | One dotted grey style, major routes only slightly brighter | The map separates major and minor routes and adds red incursion routes with glowing nodes | Medium |
| A8 | Disputed space | Hatch at 5% strength, effectively invisible | Hatched zones are part of the map legend | Medium |
| A9 | Craigon Origin | An ordinary system marker | The map draws a purple spiral vortex | Medium |
| A10 | Beyond the disk | Flat sky behind the galaxy | Faint distant galaxies and dust would give the disk an edge and a scale | Low |
| A11 | Motion | Only a slow Eclipse swirl | A premium map breathes: drifting dust, twinkle, pulsing capitals, flowing incursion routes | Medium |
| A12 | Grading | One post profile for all scales | The galaxy wants deeper blacks, controlled bloom on knots, and saturation closer to the map | Medium |
| A13 | Close views | Darker and less saturated since the last pass (`galaxy_before_after__cv_galaxy_orbit_1`) | Should be at least as rich as before | Medium |
| A14 | Map chrome | HUD is clean but generic | The map frame language (thin gold rules, spaced caps, compass, scale bar, legend) could carry into a toggleable legend | Low |

## 4. Plan
Each phase ends with a rebuild, the `-rrTour` capture, before and after images in `Docs/Evidence`, and a note in HANDOFF.
Frame time is measured as alternating A/B runs against the previous build (single runs on this machine drift 2 to 3x).

### Phase 1: adopt the updated layout (data). Size: M. Fixes A1, prepares A8 and A9.
**Status: done 2026-10-16** (see HANDOFF, "Faction map v2 layout"). The creator confirmed v2 as canonical and asked for
provisional emblems until the artwork arrives.

* `SliceScenario.cs`: trace sector centres and radii from `faction_map_v2.webp`. Move Meeples to the south-east; make the
  Eclipse central and larger; move the Meeple Core cluster (Unity, Hope, Commonweal, Hearth, plus Concord) with it.
* Give the larger Eclipse 5 to 7 systems (Umbra near the heart, Nocturne, The Hollow Star, plus provisional names), and the
  violet internal routes shown on the map.
* Re-author lanes touching Meeples and the Eclipse; keep Spore Drift, Cinder Verge and Aurelith exactly as they are.
* Add data for what the map draws but the model lacks: a Craigon incursion network (polylines plus nodes), disputed zones
  (centre and radius), and the Craigon Origin vortex position. Save it in the save format; old saves keep their own layout.
* Move the galactic core in `GalaxyPresentation` to the Eclipse heart (about world 3, 25), so the old Meeples bulge goes.
* Update DECISIONS (the Eclipse placement is no longer provisional) and the tour poses that frame Meeples or the Eclipse.
* Accept when: the overview overlays the map within about 10 px at matching framing; tests pass; tour contact days unchanged.

### Phase 2: the Eclipse as the galactic heart. Size: L. Fixes A4.
**Status: skipped by the creator (2026-10-17); the placeholder disc was removed.**

* A black event disc at the centre (a depth-writing sphere so fleets and markers sort against it) with a corona and photon
  ring shader: thin bright violet-white ring, softer outer corona, a slight asymmetric brightening on one side.
* The surrounding volume becomes an inward log spiral of dark dust with violet emission brightening towards the ring,
  replacing the closed cold rim. The existing swirl in `RR_GalaxyVolume` extends to a slow differential rotation.
* An optional screen-space lensing pass just around the disc (a small distortion of stars behind it), behind a quality toggle.
* The Eclipse keeps its own dim red and violet stars, no faction colour and its unclaimed status.
* Accept when: at the overview the Eclipse is the clear focal point without washing out labels, and still reads as
  "unexplored" (dark, not lit up). Reduced motion stops the rotation.

### Phase 3: nebula fidelity. Size: M. Fixes A5, A9, A13.
**Status: done 2026-10-17** (HANDOFF, "Nebula detail and chart layer").

* A tiling 64-cubed detail noise texture sampled in the ray march to erode density and add fine wisps below voxel size
  (one extra 3D sample per step, the standard cloud-rendering trick).
* Sharper, fractal sector edges in the volume itself (detail erosion concentrated at the edge) so the clouds echo the map
  without a drawn rim.
* Re-tune per-sector palettes towards the map (deeper blue Silex, saturated Pygmaei violet, warm gold Pirate Reach), and
  restore richness in close views.
* The Craigon Origin as a compact purple spiral vortex in the volume.
* Accept when: g02 at 2x crop shows filaments finer than 2 px; cv shots are at least as rich as the session 1 captures.

### Phase 4: a premium chart layer. Size: L. Fixes A2, A3, A7, A8, A14.
**Status: done 2026-10-17**, except the optional legend panel and zoom-based label detail levels. Added at the creator's request: mist clearing when zoomed in on units.

* Territory borders from a **signed distance field** (owner map at 2048 x 1536, distance transform at bake time), drawn
  with analytic anti-aliasing: a thin bright line, a soft outer glow, an inner gradient, and the same fractal noise as the
  nebula edge. One line per border shared by both sectors, not two coloured lines side by side.
* Sector titles as the map sets them: emblem above, spaced small caps title, italic subtitle, a soft dark halo for
  legibility. Emblems as SDF textures so they stay crisp at any zoom (source: creator files, see decisions).
* Capital worlds with the map ring glyph; major and minor routes in distinct dash patterns; Craigon incursion routes as red
  dashes with slowly pulsing nodes; disputed zones hatched at a visible strength.
* Level of detail: sector titles and emblems at the overview, system names closer in, routes fade with zoom.
* An optional legend panel (compass, scale bar, route key) in the map frame style, off by default.
* Accept when: overlays on, the overview reads like the faction map; overlays off, none of this is visible.

### Phase 5: stars and systems. Size: M. Fixes A6, A10.
**Status: done 2026-10-17**, except the star clusters near the heart (HANDOFF, "Stars and system markers").

* System markers by star class with a halo; capitals and the hero system get four-point diffraction spikes and a ring.
* Fix the dashed-star artifact in `RR_StarField` (suspect the clip-space size clamp for sub-pixel stars).
* A few star clusters near the heart; slow twinkle on bright stars.
* Faint distant galaxies and dust in `RR_SpaceSky` behind the disk.

### Phase 6: motion, grading and camera. Size: S to M. Fixes A11, A12.
**Status: done 2026-10-17** (HANDOFF, "Motion, grading and camera feel").

* Ambient motion: dust drift, twinkle, capital pulse, flowing incursion routes; all scaled by reduced motion.
* A galaxy post profile (deeper blacks, bloom threshold for knots and the corona, map-like saturation), blended by scale.
* Camera feel: pitch eases flatter as you zoom in, with gentle inertia, so the disk shows its depth.

### Phase 7: verification and sign-off. Size: S.
**Status: done 2026-10-17** (HANDOFF, "Final verification"). Sign-off itself is the creator's.

* Full tour at 1080p and offscreen 1440p with UI scale 1.25; soak 10 cycles; tests 167/167.
* A side-by-side of the overlay-on overview against `faction_map_v2.webp` at matching framing.
* A/B frame time per phase; HANDOFF and Evidence updated.

## 5. Budgets
| Item | Estimate at 1080p |
|---|---|
| Detail noise in the ray march | 0.3 to 0.5 ms |
| Eclipse disc, corona, optional lensing | 0.2 ms, plus 0.2 ms with lensing |
| SDF borders and chart glyphs | under 0.2 ms |
| Stars, spikes, sky galaxies | under 0.2 ms |
| Total | about 1 ms, so roughly 2.8 to 3 ms median, under the 4 ms ceiling |
Startup: the SDF bake adds about 0.3 s (parallel); the volume bake stays cached across loads.

## 6. Decisions needed from the creator
1. **Adopt the updated map as the canonical layout** (Phase 1 moves Meeples and the Eclipse). Recommended: yes.
2. **Faction emblems:** provide the emblem artwork (vector or 1024 px PNG per faction), or I recreate the seven emblems from
   the map as provisional SDF icons. Recommended: provide the files; provisional icons in the meantime.
3. **Chart intensity:** with overlays on, should the borders glow as strongly as on the map, or stay subtler so the space
   scene still leads? Recommended: map strength at the overview, fading as you zoom in.
4. **The Eclipse heart:** a black-hole-like eclipse is the map reading. Confirm there is no lore reason to avoid a black hole
   (it could also be a dead star or an artifact; the visuals work for any of them).

## 7. Risks
* The larger central Eclipse sits between Silex and Meeples, so routes across the middle get longer or must pass through
  unclaimed space. This is a design consequence, not only a visual one.
* Emblems recreated from the map image would be low resolution and provisional; real files avoid rework.
* Lensing and detail noise are the costliest items; both sit behind quality toggles if the budget tightens.
* Old saves keep their v1 geometry; a save version bump with a clear message is cleaner than silent mixing.

## Order
Phase 1 first (everything depends on the layout), then 2 and 3 (the space layer), then 4 (the chart layer), then 5 and 6,
with 7 after each phase and once at the end.
