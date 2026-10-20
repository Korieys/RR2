# Gameplay HUD refinement

This pass extends Prismatic to the main gameplay surfaces, retaining the Silex navy, white and light-blue palette and the existing Blender crystal relief.

The dossier now leads with a dark telemetry panel. Fleet telemetry shows actual ship count, battle points and movement/combat status. World telemetry shows population, shipyard availability and operational or blockade status. World and fleet headings identify the dossier type instead of using the same generic label.

Fleet composition uses individual ship cards with the flagship distinguished by its rail and role label. Hull values and health bars sit alongside the portrait and name. The fleet strip explicitly marks the selected fleet and separates location from operational state. Fleet commands explain why orders are unavailable during contact, and give a destination-selection instruction during targeting.

Planet infrastructure, daily production and orbital status have distinct visual groups. The construction list uses horizontal portrait/name rows, compact costs, and the selected hull's detailed ability text. Other hulls retain their ability descriptions as tooltips. Selection still only selects; only the existing command button spends resources.

Resource income in the top bar now includes `/d` to distinguish daily flow from the stockpile. All authoritative values come from the existing galaxy state and rules. No new simulation state is introduced.

Reusable components: `UiKit.Summary`, summary text roles in `Widgets`, Prismatic card rendering, and the gameplay rules appended to `Prismatic.uss`. Both normal and compact layouts are styled. Existing hover, focus, selection-reveal and Reduced Motion handling remain shared.

Verification uses the real player with `-rrReview`, `-rrHudReview`, `-rrPrismaticReview`, and `-rrUiKitOrders`, plus a disposable save. Captures and logs live in `Captures/GameplayPolish`. The new fleet checks submit actual UI selection events, compare summary values against game state, check roster completeness and width, verify one selected fleet, and compare the authoritative state digest. Existing review checks cover galaxy/system/world navigation, 100/125/140% scaling, motion lifecycle, input hit testing, and commission/cancel/development transactions.

This is visual inspection and engineering verification, not a human usability study or a new full combat campaign playthrough.

Final result: 525 runtime checks passed, 0 failed, 21 actual player captures. The first run caught orbital-copy overflow; the final run verifies the corrected density. Deterministic suite: 167 passed (146 Commandfall, 21 slice).


## Spacing follow-up — 19 October 2026

The latest build adds explicit command padding, stable button heights, 12-unit fleet-command gaps, caption-sized single-line tabs and vertical-scroll consistency. The complete motion pass and new final validation supersede this document's original pass counts: see [MOTION_REVIEW.md](MOTION_REVIEW.md).
