# Spacing and motion validation — 19 October 2026

> Follow-up: the combat roster clipping correction is built and validated in `Captures/Motion/validated-20261019-r3`. See that folder's `REVIEW.md` and `verification.json`. The earlier evidence below remains the baseline for unchanged HUD and motion behavior.

The playable build is `Builds/SpacingMotion/RuleAndRuin.exe`. Final evidence is `Captures/Motion/validated-20261019-r2`. This supersedes the earlier incomplete motion audit and the first spacing review. Existing W1, Premium, Prismatic and Motion builds remain available.

## Layout corrections

Command panels now reserve 28 UI units of bottom padding (24 in compact mode). Primary commands have a fixed 46-unit height (40 compact), so free panel height cannot stretch them against the bevel. Panel height follows content and the nearby hint follows the actual dock bounds. Runtime checks require at least 16 units of bottom inset.

Set course and Hold have a 12-unit gap. Tabs have an 8-unit gap and take their width from their captions, including selected bold text; captions remain on one line. Toolbar and tactical controls have separate hit areas. Construction icons have their own text inset. Vertical dossiers, combat rosters and modal lists suppress horizontal scrollbars; the horizontal fleet strip retains its scrolling behavior.

The visual review caught the Development caption wrapping at enlarged UI scale, then the first correction left Overview slightly cramped. The final layout uses intrinsic caption widths instead of a special weight for one tab. It also removes duplicate simultaneous threat notices and keeps the combat animation-speed caption synchronized with the active speed.

## Motion system and authoring

Presentation envelopes live in `Assets/RR/Game/PremiumMotion.cs`. Quintic smootherstep handles travel and camera arrival; cubic ease-out handles brief pulses. Exponential damping is independent of frame rate. Visual interpolation does not advance strategy days or mutate Commandfall state.

| Surface | Implemented motion | Reduced Motion |
|---|---|---|
| Camera | Focus leads logarithmic dolly slightly; exact final pose; carried damping cleared on snap and transition; interrupted completion callback runs once. | Active transition resolves to its target immediately. |
| Strategic fleet | Damped heading, 0.42-second drive wake, 0.35-second arrival settle and brief arrival rail. | Immediate placement/heading and cleared wake. |
| Local system | Slow decorative planet/cloud/station rotation; formation suspension of 0.025 world units, 0.3° pitch and 0.55° roll around authored transforms. | Decorative rotation and suspension stop; meaningful strategic orbital positions remain. |
| Tactical move | 0.12-second anticipation, 0.26 seconds per path segment capped at 1.3 seconds, eased travel, up to 7° banking and exact legal-hex endpoint. | No anticipation or bank; shortened direct travel. |
| Jump | 0.45-second travel with departure/arrival rails and 0.55-unit arc. | No arc. |
| Weapon | 0.18-second aim, 0.10-second charge, 0.32-second beam and local recoil. | No recoil. |
| Shield / objective / destruction | 0.55 / 0.65 / 0.85-second authored beats; destruction uses at most eight faceted fragments and a dissolve. | Fixed fading rail, no flying fragments. |
| Interface | Four rows maximum, 35 ms stagger, 220 ms reveal and 5-unit displacement; brief panel entrance/exit and value emphasis. | Final state immediately; in-flight tracks settle. |

Combat timings above are at 1×. Playback speed scales their durations; Reduced Motion applies the existing 0.6 multiplier. Core movement path, hit values, costs, ETA and outcomes remain authoritative. Values appear immediately rather than counting through invented intermediate values.

Blender source: `ArtSource/Motion/Prismatic-motion.blend`. Generator: `ArtSource/Motion/motion_assets.py`. The authored segmented double rail and faceted ejecta export to `Assets/Resources/RR/Motion/pulse-rail.obj` and `crystal-ejecta.obj`. The blend retains scale/rotation keyframes as an editable reference; Unity evaluates the runtime envelopes. Shared imported meshes are retained; per-effect materials and temporary beam resources are destroyed by their owners.

For future effects, use `MotionBurst.Spawn` with a finite lifetime, bind decorative movement to Reduced Motion, and land on the authoritative transform. For UI, use `UiMotion` so detaching an element cancels its tracks. Keep controls independent of animation completion.

## Validation

- Unity Windows build succeeded. Build log: `Logs/spacing-final-build-3.log`.
- 1280×720 presentation review: **908 passed, 0 failed**.
- Complete campaign: **461 passed, 0 failed**.
- 1920×1080 presentation review: **906 passed, 0 failed**.
- Deterministic suite: **167 passed, 0 failed** (146 Commandfall, 21 slice), from the user's completed run. Subsequent refinements touch presentation only.
- Battle result: **winner seat 0 (CommanderEliminated) after round 8, 103 commands**; replay yields the identical authoritative state.
- Save/load restores the campaign digest; normal player save is byte-for-byte unchanged.
- Motion effects all terminate; native material count returns to baseline; no active motion effects remain after battle teardown.
- Eleven gameplay MP4s encoded from **637 timestamped source frames**. `verification.json` records source/build hashes and clip metadata.

The first campaign attempt timed out while procedural Cinder Verge assets were still loading. The harness now waits for readiness and exits cleanly on failure instead of dereferencing a missing battle. Only `Automation.cs` changed in that final rebuild: the passing 720p presentation review remains valid, and the campaign and 1080p review use the final build. `campaign-interrupted/` and `player-720.log` preserve the unsuccessful attempt; the successful retry is recorded in `player-campaign.log` and `campaign/`.

The full campaign starts in the galaxy, enters a system, inspects a world, commissions a ship, inspects a fleet, issues an intercept order, arrives at contact, completes an orbital battle, applies the result, returns to the galaxy and saves/loads. It checks settled unit transforms against legal hexes throughout and replays the command log from the original battle setup.

The presentation checks cover 100%, 125% and 140% UI scale, real command transactions, hit testing, frame insets, tab text width, rapid tab replacement, animation cleanup, camera interruption, fleet arrival, Reduced Motion, effect lifetimes and native-material return to baseline. Normal save SHA256 remains `7e65361205dc3cabb1bbc5cc70def580c7051c2856c19112ea99a3ace0ea89e8`.

Visual inspection used actual timestamped player sequences, including camera approach/settle, fleet arrival, formation dwell, UI reveals, normal/double-speed combat and Reduced Motion. Final layout captures were checked at both resolutions. This is engineering and visual validation, not a human usability study.

## View or reproduce

Eleven MP4s in the evidence folder's `videos/` directory cover `galaxy-to-system`, `world-inspection`, `formation-dwell`, `system-to-galaxy`, `fleet-travel`, `combat-opening`, `combat-double-speed`, `combat-midgame`, `combat-reduced-motion`, `camera-orbit-settle` and `interface-reveals`. Raw PNGs and `timings.csv` remain alongside the review/campaign logs.

`clip-sources.json` selects the complete 44-frame galaxy-to-system sequence from the successful early portion of the first campaign attempt. It uses the same production UI/camera code as the final build. The retry's cold procedural loading consumed that short capture window, leaving only three early frames; those raw files remain untouched. All other clips use the final campaign or passing UI review. The evidence manifest records each exact source timeline.

To encode any matching evidence directory:

```powershell
& 'C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe' -b --factory-startup --python 'E:\ClaudeCode\RR3\ArtSource\Motion\encode_motion_review.py' -- 'E:\ClaudeCode\RR3\Captures\Motion\validated-20261019-r2'
```

Record a combined review/campaign using `-rrReview <dir> -rrHudReview -rrPrismaticReview -rrUiKitOrders -rrSpacingReview -rrMotionReview -rrMotionRecord -rrMotionClip -rrTourAfterReview <campaign-dir> -rrTourPlan intercept -rrTourUiScale 1 -rrRes 1280x720 -rrUiScale 1 -rrSavePath <disposable-save> -logFile <log>`. Launch a visible window. For the second resolution, use `-rrRes 1920x1080` and omit the tour and recording flags.

The MP4 timeline holds the captured frames using their measured timestamps; it does not synthesize gameplay frames. PNG capture has overhead, so these clips assess composition, sequence and settling, not native refresh-rate smoothness or GPU performance. Unity's batch editor still reports the previously observed JobTempAlloc shutdown warnings; the standalone effect-lifecycle and material-baseline checks pass. No strategy or vendored Commandfall source changes are part of this spacing/motion pass.
