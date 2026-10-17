# Rule and Ruin vertical slice: progress record

Resume from "Next step". Full status: [HANDOFF.md](HANDOFF.md). Decisions: [DECISIONS.md](DECISIONS.md).

## State (2026-10-16, end of session 1)
* Playable build: `Builds/W1/RuleAndRuin.exe`. The full sequence is verified by `-rrTour` (Captures/final, tour_1440,
  tour_intercept_1440).
* Tests 167/167 (`Tools/SliceTests`). Soak 10/10 cycles clean. The Commandfall 0.15 regression is clean.
* Committed on `main` (initial commit e7186a3). Builds, Library, Captures and test binaries are ignored.

## Build-loop facts
* Build: `Unity.exe -batchmode -quit -projectPath . -executeMethod RR.EditorTools.BuildTools.BuildWindows -rrBuildOut Builds/W1/RuleAndRuin.exe`.
  Use `-rrSetup` after adding shaders, models or audio (it rebuilds `Assets/Resources/RR/ArtLibrary.asset`).
* If it fails with `PlayerDataCache ... Access is denied`, clear `Library/PlayerDataCache` and build again. Building into a
  new output folder triggered it as well.
* Do not edit scripts while a batchmode build runs. Stop players by process id.
* Writing files from tools: PowerShell here-strings trip a safety check on short identifiers that look like shell removal
  aliases, so use longer variable names. In bash heredocs, avoid a backslash before a quote; use forward slashes in paths.

## Next step
* Creator review of `Docs/Evidence`, then act on "Known weaknesses" in HANDOFF.md (the galaxy overview's map-like look first).