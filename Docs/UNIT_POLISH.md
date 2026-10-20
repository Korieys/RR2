# Close-up unit polish and inspection

Play `Builds/PolishedUnits/RuleAndRuin.exe`. This is a separate build; the previously running `Builds/Units` copy is retained.

## Inspection

- Click the ship image in a fleet card, fleet composition row, or fleet command panel.
- In a local system, click a visible ship.
- In battle, click the selected unit again, Shift-click a unit, or use its **Inspect** button. If an action is playing, inspection waits for it to finish.
- Drag to orbit, scroll to zoom, or use **Rotate**, **Closer**, and **Reset view**.
- Choose an animation, use **Slow**, **Pause**, or the **Pose** slider to examine its motion.
- **Return to game** or Escape restores the previous view. Inspection pauses the campaign and suspends an idle battle without changing its rules or history.

Fleet-card hover now fades the surface and border. It no longer launches a racing catchlight. Portraits are cropped to the visible silhouette with a small safety margin. The inspector frames actual mesh vertices and forces the highest detail level.

## Animation work

All 49 supplied assets have revised six-clip animation sets, including the alternate Monolith. The game registry contains 48 canonical entries.

The pass adds role-specific anticipation, contact, settling and recovery; separate heavy, agile, support and spacecraft timings; planted two-link infantry legs; articulated crawler/walker contacts; a rotating Facet Ram cutter; and delayed effects at the authored release point. The old faction-wide speed multiplier is removed so the authored timing reaches the player unchanged.

Independent armor plates and equipment pieces use rigid bindings. Continuous body regions use smoothed joint weights with at most four influences per vertex. Infantry rigs have 15 bones. Legged vehicles have 8 or 14, spacecraft retain two, and the Facet Ram has a separate cutter bone. Hull motion remains deliberately restrained.

The editable Blender sources are in `ArtSource/Units`; Unity FBX models and prefabs are in `Assets/RR/Art/Units`. The original source ZIP, naming records, and previous source versions are preserved.

These are authored keyframe and skin-binding edits made through Blender scripts and visual review, not motion capture or a replacement sculpt/retopology pass. The supplied generated textures and geometry remain the source of surface detail. The two provisional names and four missing canonical source models documented in UNIT_PRODUCTION.md are unchanged.

## Evidence

`Captures/UnitPolish` contains the UI checks, live animation captures, deformation measurements, and the final player verification report. The checks cover both UI scales, exact-model selection, control behavior, return to the original camera, ambient reduced-motion behavior, battle suspension, combat exercises and campaign-state preservation.

The deformation audit samples eight poses in each clip on every asset. It is diagnostic, not a blanket pass/fail certificate: continuous Silex cloth/body meshes still deform, while rigid armor components retain their edge lengths. Final-pose dissolves intentionally accompany the collapse animation.

The source animation editing helpers are retained in `Tools/UnitPolish`. To export a manually edited production Blender file, use the existing `Tools/UnitProduction/export_blends.py` workflow, then `RR.EditorTools.UnitAssetBuild.PrepareAndBuild`. Pass `-rrBuildOut Builds/PolishedUnits/RuleAndRuin.exe` when rebuilding.


## Release verification result

The release build succeeded with zero build errors. The release player reported 369 passing checks and zero failures, including all 288 live clip checks. Three ground exercises each ran 14 legal player commands. The existing 167 rules tests passed. Campaign state and the return camera were preserved. Seven short motion recordings and close-up screenshots are in Captures/UnitPolish/ReleaseReview.

Recording timestamps are preserved; screenshot capture overhead lowers recording frame rate, so these videos are visual evidence rather than a hardware-performance benchmark. The existing Unity JobTempAlloc warnings occurred during offscreen/editor capture; this work does not certify an engine-memory soak.
