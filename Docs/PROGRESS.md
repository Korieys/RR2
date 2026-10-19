# Rule and Ruin vertical slice: progress record

Resume from "Next step". Full status: [HANDOFF.md](HANDOFF.md). Decisions: [DECISIONS.md](DECISIONS.md).

## State (2026-10-16, end of session 1)
* Playable build: `Builds/W1/RuleAndRuin.exe`. The full sequence is verified by `-rrTour` (Captures/final, tour_1440,
  tour_intercept_1440).
* Tests 167/167 (`Tools/SliceTests`). Soak 10/10 cycles clean. The Commandfall 0.15 regression is clean.
* Committed on `main` and pushed to https://github.com/Korieys/RR2 (`origin`). Initial commit e7186a3; the session 2 galaxy work
  is 32dbab1. Builds, Library, Captures and test binaries are ignored.

## Build-loop facts
* Build: `Unity.exe -batchmode -quit -projectPath . -executeMethod RR.EditorTools.BuildTools.BuildWindows -rrBuildOut Builds/W1/RuleAndRuin.exe`.
  Use `-rrSetup` after adding shaders, models or audio (it rebuilds `Assets/Resources/RR/ArtLibrary.asset`).
* If it fails with `PlayerDataCache ... Access is denied`, clear `Library/PlayerDataCache` and build again. Building into a
  new output folder triggered it as well.
* Do not edit scripts while a batchmode build runs. Stop players by process id.
* Writing files from tools: PowerShell here-strings trip a safety check on short identifiers that look like shell removal
  aliases, so use longer variable names. In bash heredocs, avoid a backslash before a quote; use forward slashes in paths.

## Next step
* Galaxy view: [GALAXY_FIDELITY_PLAN.md](GALAXY_FIDELITY_PLAN.md) is complete (phase 2 skipped by the creator). Next: creator
  review of `Docs/Evidence/final2__*` and the side by side, then the remaining known weaknesses in HANDOFF. Emblems stay provisional until the creator sends the artwork (drop-in PNGs, see HANDOFF).
* Measure at true 1080p: launch with `-screen-fullscreen 1 -screen-width 1920 -screen-height 1080` (the player otherwise keeps
  its last window size; check the second line of the tour log).
* Creator review of `Docs/Evidence`, then act on "Known weaknesses" in HANDOFF.md (the galaxy overview's map-like look first).

## Vertical slice prompt pack (from 2026-10-19)
The Pygmaei and Inhumanus vertical slice (Prompts 0 to 12) is tracked in [vertical-slice/STATE.md](vertical-slice/STATE.md), with its
contract, decisions and acceptance gates beside it. Read those four files first when resuming that work.
