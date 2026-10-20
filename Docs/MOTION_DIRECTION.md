# Premium motion implementation and completion gates

Objective: improve animation throughout the playable Rule and Ruin slice, with authored Blender assets where useful, verified in real gameplay. Preserve deterministic Commandfall, galaxy navigation, immediate command acknowledgement, saved state and Reduced Motion.

Scope and evidence required before completion:

1. Camera: controlled focus/dolly arrival and settle; no stale damping after snap/transition. Record galaxy-system-world-return and verify final poses, cancellation, Reduced Motion.
2. Strategic fleets: smooth facing/arrival presentation and readable travel effects while retaining exact authoritative route positions and ETA. Record a real move order through arrival; compare strategy digest/replay.
3. Local scenes: coherent slow planetary/cloud/station animation and subtle formation suspension. Record system/planet dwell and Reduced Motion.
4. Combat: thrust anticipation, eased path movement, heading/bank recovery, distinct jump/weapon/shield/impact/destruction/objective beats. Author reusable faceted rail/ejecta meshes in Blender. Record actual combat; prove visual transforms land on legal hexes, animation speed works, effects terminate, replay hash and outcome remain identical.
5. Interface: bounded staggered dossier/list reveals, clear action/selection emphasis and restrained notifications. Test rapid selection, tab replacement, detach/cleanup and Reduced Motion; record actual UI motion.
6. Delivery: build, standalone gameplay MP4 clips from timestamped captured frames, source .blend + generator, implementation/timing guide, runtime and deterministic test results. Do not infer animation quality from still screenshots alone.

Motion language: short anticipation, directional execution, readable contact, deliberate settle. Preserve the tactical board and camera stability during combat; ship-local recoil instead of camera shake. No constant UI shimmer. Decorative motion must yield to Reduced Motion immediately.

Completed 19 October 2026. All six gates above are covered by the final standalone build, timestamped captures and runtime checks. See [MOTION_REVIEW.md](MOTION_REVIEW.md) for the exact scope, results, authoring guide and limitations.
