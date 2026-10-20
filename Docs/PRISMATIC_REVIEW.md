# Prismatic implementation review

Direction B is implemented in `Builds/Prismatic/RuleAndRuin.exe` with the reusable system described in [PRISMATIC_UI.md](PRISMATIC_UI.md). The approved image and editable Blender source are in `ArtSource/UI/Prismatic`.

## Verified

- Unity Windows build succeeded. Runtime assembly SHA-256: `41c9437b6d81dd6872799e640f52a57035852c96fa8bf306809eb64a59b7e7a6`.
- Existing rule suite: **167 passed, 0 failed** (146 Commandfall, 21 slice).
- Final notification/settings regression: **10 checks passed**, zero failures, with actual player captures of readable success and warning messages.
- Actual-player presentation review: **392 checks passed**, zero failures; **17 screenshots**.
- Layout at 100%, 125% and 140%; tabs, project selection, exact-once commission cost, cancellation/refund and foundry construction.
- Real mouse input checked in the player. Decorated controls block world input; open space remains available to navigation.
- Twelve rapid tab replacements keep live animation tracks bounded; all settle to zero. Closing a modal mid-entry releases its animation. Reduced Motion settles active motion and shows final positions/opacity.
- Production overview shows its full orbital explanation at normal scale. Short button captions retain their required width. Planet framing remains beside the dossier after tracking settles.
- The campaign tour starts from the exact initial scenario, restored through the real save/load path after the disposable order tests. It covers galaxy navigation, interception, orbital Commandfall, replay and save/load, with **50 captures** at 125% interface size and Reduced Motion.
- No modifications to strategy rules, vendored Commandfall, their tests, packages or project settings. The normal player save remains unchanged: `7E65361205DC3CABB1BBC5CC70DEF580C7051C2856C19112EA99A3ACE0EA89E8`.

## Actual runtime evidence

![World inspection](../Captures/Prismatic/release/review_ui_scale_1.00.png)

![Enlarged shipyard](../Captures/Prismatic/release/review_shipyard_1.40.png)

![Orbital Commandfall](../Captures/Prismatic/intercept/b03_ship_selected.png)

[Motion preview](../Captures/Prismatic/Prismatic-motion.mp4) is encoded from actual player frames and their measured timestamps. It shows dossier/tab transitions and modal entry. Recording overhead is excluded from the separate idle frame-time sample.

```text
perf Prismatic UI settled: median 6.94 ms (144 fps), p95 7.05 ms, p99 7.16 ms, max 13.26 ms over 119 frames; gen0 GCs 0; managed heap 11.4 MB
ui capture: pointer over inspector blocks world input = True; open space passes through = True
battle over: winner seat 0 (CommanderEliminated) after round 8, 103 commands
replay of the battle from its setup and command log: identical final state
save digest 95d58cc865d8455b, after more days 4a429e13ca70889d, after load 95d58cc865d8455b: load restored the saved state
applied battles after load: 1
```

Full logs: `Captures/Prismatic/release/tour_log.txt`, `intercept/tour_log.txt`, `ui-smoke/tour_log.txt`, `release-player.log`, `build-verified.log` and `build-delivery.log`. `verification-manifest.json` records the delivery assembly and evidence counts.

## Iteration and limits

Capture inspection found and corrected caption overdraw, short-button sizing, disabled-state ghost text, excessive wide-panel relief stretching, overview overflow, and planet tracking undoing the shot composition. The final surfaces use vector geometry and restrained Blender relief; numbers and text remain live UI.

After the full campaign tour, an isolated notification-layer correction restored native text above a simple navy surface. That correction and its enter/expire behavior were verified in the final focused runtime check; the campaign tour was not repeated for this notification-only correction. The earlier battle capture therefore shows the empty notification strip that led to the fix. [Corrected notification capture](../Captures/Prismatic/ui-smoke/notifications.png).

The earlier `hidden-window-run` produced black frames and invalid frame-time numbers. It is not used as visual or performance evidence. The final review uses a visible player window.

This was technical testing and direct visual/input inspection, not an external human playtest. The existing cold-start planet bake remains; this UI work does not optimize that generator. W1, Premium and FleetOperations builds are preserved.

Direct delivery-build mouse checks: the Log button opened Command Dispatches; the modal Close button returned to the galaxy. The test session was then closed without saving over the normal slot.
