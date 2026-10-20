# Prismatic UI system

Direction B implemented as runtime Unity UI, with Silex light blue, white and navy identity. The original gameplay title is absent from the interface. The faction name and command context identify the player's current view.

## Components

- `Prismatic.cs` draws cut-corner surfaces as UI Toolkit meshes and vector outlines. Surfaces stay sharp at every scale, without texture slicing or baked text.
- `Prismatic.uss` owns layout, spacing and responsive sizing; `UIThemes/silex.uss` supplies color tokens. Normal and compact layouts share the same components.
- Dossier: inset paper surface, icon tabs, icon/value rows, a five-segment development meter, three daily-production columns, orbital status and a fixed navigation footer.
- Fleet strip: chamfered navy cards, real hull portraits, ship count, explicit operational state and location.
- Contextual command card: portrait bay, project duration, exact authoritative costs and a single primary action. Hull selection remains separate from spending resources.
- Battle panels, preparation/results modals, settings and utility buttons use the same frame and typography rules. Notifications use a simple navy strip with native text, preserving readable feedback during opacity transitions.
- `PrismaticButton` keeps native semantic text and draws a child caption above the custom face. The child participates in layout so short controls size correctly; native text is transparent to prevent doubled lettering in disabled states. Dynamic battle-button captions remain synchronized.

## Motion

| Event | Behavior | Duration |
|---|---|---|
| Dossier tab/selection changes | Short eased content reveal; command card settles 8 logical pixels; single edge sweep | 260 / 440 ms |
| Pointer enters/leaves a control | Surface and edge emphasis | 140 ms |
| Pointer press or keyboard submit | Brief edge sweep; action executes immediately | 440 ms |
| Modal enters | 12-pixel eased entrance | 260 ms |
| Toast arrives/leaves | 7-pixel entrance, then short fade/lift on expiry | 260 / 160 ms |
| Resource changes | Value updates immediately, then opacity emphasis | 320 ms |

Timing uses unscaled time, so the UI remains responsive while the strategy clock is paused. No animation advances time, changes state, postpones a command or writes a save. Tracks are owned by their element and channel: replacement cancels an earlier track and detaching an element releases it. There are no permanent idle animation timers. Reduced Motion settles tracks immediately and shows final positions/opacity.

## Art source

`ArtSource/UI/Prismatic/Silex-Prismatic-Relief.blend` contains the editable, lit, beveled crystal relief. `build_relief.py` recreates it deterministically in Blender. The transparent render is `Assets/Resources/RR/UI/Prismatic/crystal-relief.png`; it is used at low opacity, away from the main reading area. Wide panels preserve its aspect ratio. Borders, text, numbers and icons are drawn at runtime.

Existing ship portraits continue to use the real game models. Rebuild those through `RR.EditorTools.UiKitBuild.PrepareAndBuild` when hull art changes.

`Prismatic.SetFaction` chooses the relief once during theme installation. Silex uses `crystal-relief`; future factions can supply `<faction>-relief.png`, otherwise they get clean surfaces. A different faction does not inherit Silex crystals automatically.

The planet-inspection camera preset offsets its framing slightly into the open viewport so the wider dossier does not hide the nearby station. Galaxy navigation, system bookmarks and manual camera controls retain their existing paths.

## Extension rules

1. Use existing semantic components instead of introducing a new panel style for each feature.
2. Preserve navy structural surfaces, inset content surfaces, restrained chamfers and consistent icon strokes.
3. Override faction color tokens in `UIThemes/<faction>.uss`. Keep danger, ownership, disabled state and battle targeting explicit; faction hue must not be the only cue.
4. Keep decoration out of hit testing. The custom frames add no raycast layer or child input targets.
5. Add motion only when it communicates an event. Use `UiMotion` channels and honor Reduced Motion. Avoid looping shine across data or decorative camera movement.
6. Keep values authoritative. Never animate a resource counter through intermediate values or delay a command until its animation ends.
7. Check 100%, 125% and 140% interface sizes; primary actions and the fixed navigation footer must remain visible. Long inventories use bounded scroll areas.

## Build and review

Create the separate Windows player with `RR.EditorTools.PrismaticBuild.PrepareAndBuild` and `-rrBuildOut Builds/Prismatic/RuleAndRuin.exe`.

Use `-rrReview <capture-directory> -rrPrismaticReview -rrMotionClip -rrUiKitOrders -rrRes 1920x1080 -rrUiScale 1 -rrSavePath <disposable-save>` for the actual-player presentation, motion, layout and order review. `-rrReviewStay` leaves the player open for direct mouse/keyboard checks. The motion capture writes actual player frames plus measured timestamps, separate from the performance measurement.

Use the existing `-rrTour <capture-directory> -rrTourPlan intercept` flow to verify galaxy navigation, orbital combat, deterministic replay and save/load. Review results and limitations are recorded in `PRISMATIC_REVIEW.md`.

For one continuous verification session, supply `-rrTourAfterReview <tour-directory> -rrTourPlan intercept -rrTourUiScale 1.25 -rrTourStay` with an explicit disposable save. After a successful review, automation restores the exact initial scenario through the real save/load path and checks its digest before starting the tour. The Stay option leaves the completed tour available for direct input checks.

Run screenshot/video reviews with a visible player window. A hidden player may skip rendering on this desktop while still running UI layout and simulation; black captures and its artificial frame-time numbers are not visual or performance evidence.

`-rrUiSmoke <capture-directory>` is a focused notification/settings test. It skips the planet-texture warmup and renders the actual galaxy HUD, checks success/warning text, caption sizing, notification expiry, animation cleanup and unchanged state. `-rrUiSmokeStay` leaves it open for direct input inspection. Normal launches retain their usual startup path.
