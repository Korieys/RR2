# Cinematic UI finish

Build on the accepted Command Glass composition and Silex palette. The intended benefit is a stronger sense of depth and clearer acknowledgement of an action. The principal risk is luminous decoration competing with the galaxy or lowering text contrast.

## Visual system

- Dark surfaces receive a soft upper catchlight and faint secondary reflection, with a paired bright rim and dark lower lip.
- The pale contextual card keeps dark text, the existing insets and its blue CTA. Light and dark surfaces have separate effect strengths.
- Ship portraits retain the real project art, now displayed above a quiet pool of light and interrupted orbital rings. The portrait image paints after the backing and cannot intercept pointer input.
- Hover, keyboard focus, command input and changed dossiers can trigger a single 560 ms light pass. These reuse each element's existing animation channel; repeated input replaces its prior effect.
- Settings, preparation and results use a darker vignette backdrop. Dialog content and hit targets remain stable.
- Reduced Motion immediately resolves the light pass. No decorative effect runs continuously when the UI is idle.

## Acceptance

Verify visible and settled states in the player, the portrait layering, bounds and pointer passthrough, rapid input replacement, disabled controls, immediate Reduced Motion, zero residual tracks, and unchanged authoritative state. Re-run the existing spacing/order/navigation reviews at 720p/1080p and enlarged scale, plus the campaign/battle integration path because the materials are shared by combat panels.

Capture timing is separate from performance sampling. Record native settled frame timings after recording ends. Interpret the captures as visual evidence, and the automation as functional evidence; perceived premium quality remains a design judgement until reviewed by the user.

## Reusable implementation

`CinematicUi.cs` draws bounded mesh lighting and fine line work. `Prismatic.Surface` supplies the existing faction and surface tokens. `UiKit.Ship` composites the real portrait above its backing. `UIRoot.OpenModal` applies the vignette. Existing component dimensions, game callbacks and mechanics remain authoritative.

Tune lighting in `CinematicUi.Lighting`, rim treatment in `Finish`, portrait backing in `Portrait`, and backdrop falloff in `Backdrop`. Timing remains in `Prismatic.Surface.Pulse` and `UiMotion`. Avoid adding screen-sized decorative overlays to ordinary gameplay or per-frame polling for these effects.

## Iteration notes

The first capture exposed excessive surface illumination and visible gradient bands. Illumination was reduced. Two shared 512×512 procedural textures with stable dithering replaced low-alpha vertex gradients to remove visible stepped bands. They are reused across surfaces and released on application exit. A recording-time test also exposed an automation wait that counted the preceding capture frame toward a newly requested delay; waits now use a fresh unscaled-time deadline. This changes the validation harness, not game timing.

## Verified result — 19 October 2026

The final build is [Builds/CinematicUI/RuleAndRuin.exe](../Builds/CinematicUI/RuleAndRuin.exe). The Command Glass layout and Silex palette remain the base.

| Runtime review | Passing assertions | Failures |
| --- | ---: | ---: |
| Cinematic effects, portrait/input bounds, settings and cleanup | 55 | 0 |
| 1080p HUD, dossiers, orders, spacing and motion | 939 | 0 |
| 720p HUD, dossiers, orders, spacing and motion | 937 | 0 |
| Additional full-campaign checks | 674 | 0 |

The presentation reviews include 100%, 125% and 140% UI scale. Battle replay produces the same authoritative final state, and save/load restores the saved state. Tests used disposable saves; the normal player slot hash remains unchanged.

Visual inspection covered the active and settled command effect, modal entrance/settled/closed states, world overview, development, fleet orders at 140%, settings and combat. The two review videos contain actual captured player frames held to their recorded timestamps; PNG recording affects sampling and is not a native frame-rate benchmark. Both exports were decoded and checked. Automated timing assertions run separately from the cinematic recording.

Settled host sample (after recording): `perf cinematic UI settled: median 6.94 ms (144 fps), p95 7.83 ms, p99 12.96 ms, max 34.31 ms over 119 frames; gen0 GCs 1; managed heap 11.2 MB`. This is a short sample on this machine, not a hardware performance guarantee.

Unity build 5 succeeded with zero errors. Existing camera-serialization and automation API warnings, and Unity editor shutdown JobTempAlloc messages, remain outside this UI change. Build 4 encountered a generated-cache access error; its cache was moved aside and build 5 regenerated it. Earlier iteration captures and failed checks are preserved separately.

[World dossier](../Captures/CinematicUI/final/review-1080/review_hero_world.png) · [Fleet command](../Captures/CinematicUI/final/review-720/spacing_fleet_moving_1.40.png) · [Command light and dialog recording](../Captures/CinematicUI/final/videos/command-light-and-dialog.mp4) · [Dossier transitions](../Captures/CinematicUI/final/videos/dossier-transitions.mp4) · [Manifest and hashes](../Captures/CinematicUI/final/manifest.json)

These results establish rendered appearance, interaction and cleanup correctness. No human usability or preference study was conducted. Shared simulation and vendored Commandfall source were not changed.

