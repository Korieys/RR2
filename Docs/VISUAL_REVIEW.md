# Rule and Ruin: cinematic presentation pass

Review date: October 17, 2026 (America/Chicago).

Project: `E:\ClaudeCode\RR3`. Starting commit: `b23e354`. Working branch: `codex/cinematic-command-ui`.

The original build remains at `Builds/W1/RuleAndRuin.exe`. The revised build is `Builds/Premium/RuleAndRuin.exe`.

## Evidence and design intent

The five attachments from **Game Engine Strategy** were recovered and inspected: `Screenshot_272.png`, `Screenshot_273.png`, `image(20261018-000102).png`, `image(20261018-000048).png`, and `image(20261018-000040).png`. They show three Prismvale system views (two nearly identical), a selected-system galaxy view, and a selected-fleet galaxy view.

The installed `C:\Users\jacob\.agents\skills\game-design-director\SKILL.md` and its feedback/onboarding, design-principles, and playtesting references informed the audit. The intended experience remains an interstellar ruler moving between a navigable galaxy, physical local systems, and deterministic Commandfall encounters.

| Observed evidence | Player consequence | Implemented response |
| --- | --- | --- |
| Dense, similarly bright star speckles and broad blue-violet gas compete with small local planets. | The environment has little visual hierarchy; local objects struggle to dominate the view. | Reduced sky-star density and strength; narrowed the galactic band and reduced local nebula intensity. Galaxy sector geometry, ownership, routes, and navigation remain intact. |
| The central star is a softly shaded pale sphere with a broad diffuse halo. | The main light source reads more like a solid prop than a luminous body. | Retuned radiance, limb darkening, granulation, plasma filaments, and the corona falloff. Ambient stellar animation respects reduced motion. |
| Planet inspection initially fills and crops the viewport; the water specular hotspot and heavy clouds compete with surface identity. | The planet is harder to read as a complete world; its materials look glossy and uniform. | Pulled back and angled the inspection camera toward the terminator, reduced water specular intensity, retuned crystal-world geology and cloud coverage, softened exaggerated surface normals, and added filtered surface detail. |
| Cloud shading is sampled directly at the surface direction. | Clouds and terrain are insufficiently separated as the camera moves. | Added thin-shell view parallax and corresponding star-relative cloud-shadow sampling. |
| Flat gray repeated rows, transparent panels, tiny unlabeled resources, and free-floating log text dominate the supplied UI. | Selection, action priority, and strategic state are harder to distinguish against bright space. | Rebuilt the header hierarchy, added resource labels and income grouping, opaque charcoal/navy panels, gold emphasis, context headings, planet-type symbols, restrained health bars, and a contained dispatch feed. |
| Movement/return actions share the long inspector scroll area. | A frequent action can move off screen as information grows. | Moved the primary actions into a persistent inspector footer; bounded outliner and battle rosters; styled native scroll controls. |
| The intercept-path capture shows a very bright volcanic planet behind the right-hand tactical cells. | The backdrop competes with combat targets and legal-move highlights. | Reduced the battle planet's apparent size, moved it farther toward the upper-right of the frame, and reduced its emissive intensity; grid coordinates and picking remain unchanged. |

These are screenshot and implementation observations. They are not evidence of improved player preference or enjoyment; no human playtest study was conducted.

## Scope and preservation

- No strategic rule, roster definition, Commandfall rule, balance value, unit identity, map topology, save schema, or victory condition was changed.
- Existing scene scale transitions, system selection, route planning, galaxy navigation, management actions, and battle authority remain in their original owners.
- Existing ship meshes remain. Silex hull roughness, stone texture strength, trim metalness, royal trim, and emissive intensity were retuned; no new production ship art was made.
- Added an optional `-rrSavePath` argument so automated reviews use a disposable save rather than the player's slot.
- The original player save was restored after the W1 baseline tour and its file hash matches the pre-review backup. Subsequent revised-build runs used disposable save paths.
- Added `-rrReview <directory>` to capture Prismvale and Aurelith, check panel bounds at multiple UI scales, and confirm that navigation does not alter authoritative state. `-rrReviewStay` leaves the reviewed game open for direct interaction.

## Capture and test method

The original W1 and first revised build were run through the existing real-game `-rrTour` sequence at native 1600×900. The tour calls the same game actions used by the interface, captures navigation and battle stages, checks input blocking and transition cancellation, replays combat, and verifies save/load digests. Separate direct game-window inspection was used as well.

The original offscreen capture mode produced black 3D content with visible UI in this session. Those outputs are excluded from the visual comparison. Native game captures are the evidence used for iteration. Early baseline performance samples overlapped compilation or other capture runs and are not a controlled performance comparison.

The first revision exposed a cropped inspection globe, default white scrollbar tracks, and a battle roster extending beyond the bottom of the window. The second revision addresses those findings.

## Validation record

- Existing rules suite before edits: **167 passed, 0 failed** (146 Commandfall, 21 slice tests).
- Existing rules suite after edits: **167 passed, 0 failed**.
- First revised build: successful Unity build, 0 build errors.
- First revised full tour: commander-elimination victory; replay produced an identical final state; exactly one applied battle after load; save digest before and after load `c21758289e4600fe`.
- Final Premium build: successful Unity Windows build, 0 errors.
- Final native 1920×1080 presentation review: **0 failures**. The top bar, outliner, inspector, persistent action footer, and dispatch feed remained within the viewport at 100%, 125%, and 140% UI scale. These bounds checks supplement the inspected screenshots; they do not prove every possible content state fits.
- Navigation from Prismvale to a planet and back preserved the selected system. The full presentation review left the authoritative game-state digest unchanged.
- Direct mouse checks in the final running build: selected Aurelith from the outliner, entered through the inspector action, returned to the galaxy through the same footer, and opened fleet destination selection from a local system. The return kept Aurelith selected. Injected Escape key cancellation was not conclusively verified by the desktop input helper.
- Reduced-motion intercept tour at 1600×900 and 125% UI scale: commander-elimination victory after round 8 and 103 commands; replay produced an identical final state. Save/load restored digest `95d58cc865d8455b`, with exactly one applied battle. Its captures prompted the final battle-backdrop refinement.
- Final backdrop refinement verification: rebuilt successfully (117 MB, 0 build errors) and repeated the full intercept tour. The inspected battle-start capture keeps the volcanic planet above the board. Combat again ended after round 8 and 103 commands, replay matched, and save/load restored `95d58cc865d8455b` with one applied battle. Process exit code 0; no runtime exception, timeout, replay divergence, or save mismatch markers in the run logs.
- Final diff checks passed. Commandfall vendor code, strategic rules, rule tests, project settings, and packages have no changes.

## Evidence locations

`Captures/PremiumReview/` contains selected original W1 captures, the final 1080p presentation captures, and verification logs. Images are unmodified game captures. `presentation/tour_log.txt` records the layout and state checks. `intercept/tour_log.txt` records the final combat/replay/save-load sequence at 1600×900 and 125% UI scale with reduced motion enabled.

The build and evidence remain local; no repository changes were committed or published. The implementation is on branch `codex/cinematic-command-ui`. Builds and captures are excluded by the existing repository ignore rules; preserve them separately when sharing the project.

### Captured views

Original W1 system (1600×900, default UI scale):

![Original W1 system](../Captures/PremiumReview/baseline/s01_hero_system.png)

Revised planet inspection (1920×1080, default UI scale):

![Revised Aurelith Prime](../Captures/PremiumReview/presentation/review_hero_world.png)

Final Cinder Verge battle (1600×900, 125% UI scale, reduced motion):

![Final orbital Commandfall view](../Captures/PremiumReview/intercept/b02_battle_start.png)

These views illustrate different contexts, not a controlled image-quality or performance benchmark. The baseline and final tour directories also contain corresponding galaxy and fleet views.

## Remaining quality limits

This pass improves the implemented presentation, not the entire asset library. Ship silhouettes and station geometry are still the original generated models. The stylized procedural planets and sector borders remain recognizable as that art direction; bespoke geology textures and faction ship art could raise their ceiling further. Each cold launch still performs procedural planet baking, which took minutes on this machine. The inherited offscreen capture problem was avoided, not repaired. Audio quality was not evaluated by listening, and subjective premium quality still needs creator review in motion.

For a short formative playtest, ask a new player to identify the selected fleet, explain available resources and daily income, enter Prismvale, inspect a world, return to the galaxy, and issue one legal order without help. Record mistakes, assistance, and whether the player can explain the result. Compare against W1 under matching display settings. Treat faster, clearer completion and a stated visual preference as provisional design evidence, not a population-level result.
