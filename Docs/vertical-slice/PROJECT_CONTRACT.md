# Vertical slice project contract

This contract governs Prompts 0 to 12 of `Rule_and_Ruin_Vertical_Slice_Prompt_Pack.md` (supplied by the creator, 2026-10-19).
It extends the existing project documents and does not replace them: [README](../../README.md), [HANDOFF](../HANDOFF.md),
[PROGRESS](../PROGRESS.md), [DECISIONS](../DECISIONS.md) and the uncommitted documents in the main checkout remain in force.
Read this file, [STATE.md](STATE.md), [DECISIONS.md](DECISIONS.md) and [ACCEPTANCE.md](ACCEPTANCE.md) at the start of every
milestone.

## The loop being proven

Explore the galaxy, prepare a named flagship, contest a trade passage, outmanoeuvre the enemy, resolve the conflict, return to a
changed galaxy, and refit for the next encounter.

The proof: a player can say "I changed my ship, created a tactical opening, forced an outcome, and returned to a galaxy that
reflected what I did."

## Signature rule

"Generals don't roll dice." Combat has no random hit, critical, dodge, damage or kill rolls. In a leader-elimination battle, a
legal lethal attack on the nominated enemy leader ends the battle immediately after that action resolves. Numerical superiority
grants no immunity. Protection comes only from implemented rules and positioning: blocked firing lines, armour and shields,
range denial, legal screening, or an explicitly registered interception ability. There is no proximity shield and no automatic
ban on exposing a leader.

## Scope

In scope: Pygmaei and Inhumanus, 4 to 5 orbital classes each, one repeatable flagship encounter at a trade passage, a small
campaign loop, persistent capital ships, negotiated outcomes, and one limited Eclipse Sector expedition.

Preserved without expansion: Silex and Cräigon (the current campaign factions, with their art and the 49 integrated unit
models in the main checkout), Meeples, Space Pirates, the Elven Front, ground gameplay (the Unity ground exercises and the
external planetary Commandfall build), and the galaxy, camera and approved faction map v2 layout.

Out of scope: playable Iamni, Kallamori or Brutum; unrestricted diplomacy; a separate economy simulator; freeform ship
construction; a crew RPG; networking services; public deployment; paid purchases; engine upgrades.

## Sources of truth, in order

1. **Confirmed by the creator.** Faction map v2 as the canonical layout; the Eclipse heart disc removed (phase 2 skipped);
   the statements in this prompt pack that are phrased as requirements (the signature rule, the protection rules, the scope,
   no tripling of an already enlarged map, at most two dead ends).
2. **Implemented behaviour.** The vendored Commandfall rules (0.15.0, Full Roster ruleset, content `cf-content-1.3`), the
   slice strategy layer (`RR.Strategy`, save schema `rr.slice.galaxy` version 1), and verified tests. Implemented is not the
   same as approved: the roster values are a draft.
3. **Historical proposals.** `Rule_and_Ruin_Commandfall_Core_Six_Units.json`, codex 0.1 ("implementation design draft",
   2026-10-14). Its own note says combat values, movement rules and abilities are new proposals, not canon. Names and faction
   identities come from the Lore Sheet; numbers do not become canon by being implemented.
4. **Slice defaults.** New mechanics and numbers from this pack and from [DECISIONS.md](DECISIONS.md). Each is reversible,
   documented, and marked experimental until the creator approves it.

A lower source never silently overrides a higher one. Every conflict goes into the register in [DECISIONS.md](DECISIONS.md).

## Technical facts (verified 2026-10-19)

| Item | Value |
|---|---|
| Engine | Unity 6000.6.5f1, URP 17.6, D3D12, Mono player, Windows x64 |
| UI | UI Toolkit (`UnityEngine.UIElements`, USS in `Assets/Resources/RR`) |
| Entry scene | `Assets/RR/Scenes/RuleAndRuin.unity`, the only scene; `RR.Game.GameRoot` is the composition root |
| Assemblies | `Commandfall.Core`, `Commandfall.AI` (vendored), `RR.Strategy` (engine-free), `RR.Game`, `RR.Editor`, two test assemblies |
| Rules | Commandfall 0.15.0 Full Roster, vendored unmodified from Commandfall commit `5a31053` (repository `C:\Users\jacob\Documents\ClaudeCodeProj\Commandfall`, branch `claude/rule-ruin-commandfall-units-e88455`) |
| Roster source | codex 0.1, generated into `Assets/RR/Vendor/Core/Roster/RosterCatalog.g.cs` (156 units); executable parameters in `RosterSpecs.*.cs` |
| Orbital battlefields | `orbital_gate`, `orbital_wreckfield`: 115 hexes each, validated by `TopologyValidator` on registration |
| Save | `rr.slice.galaxy` version 2 JSON: campaign and engagement state, an optional battle in progress (`battle`, its record; VS-09) and the passage campaign (`slice`; VS-20). Version 1 saves still load. |
| Networking | None in this project. The vendored core keeps its host-authority fields (`ExpectedRevision`, `Nonce`, `Seat`). Online play exists only in the separate Commandfall game. |
| Machine | RTX 5060 8 GB, i5-14400F, 32 GB, Windows 11 26200; both displays 1080p (1440p only offscreen) |

## Commands

Build (from the project root; add `-rrSetup` after adding shaders, models or audio). If it fails with
`PlayerDataCache ... Access is denied`, delete `Library/PlayerDataCache` and build again.

```bash
"/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod RR.EditorTools.BuildTools.BuildWindows -rrBuildOut Builds/VS/RuleAndRuin.exe -logFile Logs/build.log
```

Tests (146 vendored rules tests plus the slice tests, outside the editor):

```bash
cd Tools/SliceTests && "/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe" build -c Release && "/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe" bin/Release/net8.0/SliceTests.dll run
```

Runtime evidence. Always pass a disposable save so the player's own slot is untouched. `-rrTour` needs an absolute directory,
and the first shot arrives about two minutes after launch.

```bash
./Builds/VS/RuleAndRuin.exe -rrTour <absolute dir> -rrSavePath <absolute disposable.json> -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
```

Slice tours (same rules: absolute paths, a disposable save). `-rrSavePath` is honoured only from commit `991aafa` on (D-18); before that, tours wrote the default slot.

```bash
./Builds/VS/RuleAndRuin.exe -rrCampaignTour <absolute dir> -rrSavePath <absolute disposable.json> -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
```

```bash
./Builds/VS/RuleAndRuin.exe -rrPassageTour <absolute dir> -rrSavePath <absolute disposable.json> -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
```

Add `-rrUncapped` (vsync off, no frame cap) for frame-time measurements and `-rrNoMetrics` to suppress `playtest_metrics.jsonl`. Ship-design renders and module fits: `Tools/Blender/rr_design_sheet.py`, `Tools/Blender/rr_mounts.py` ([DESIGN_REFERENCE](DESIGN_REFERENCE.md)).

## Working rules

* **One integration owner** for shared simulation and saves: `Assets/RR/Vendor/Core`, `Assets/RR/Strategy` (in particular
  `SaveGame`, `BattleBridge`, `StrategyRules`). Parallel agents may take isolated work (Blender ship art, audio, documentation)
  but never overlapping rewrites of those files, or of `BattlePresentation*`, `BattleHud`, `UIRoot` and `StrategyHud`.
* **Rules changes are logged.** The project treated `Assets/RR/Vendor` as unmodified. The pack requires rule extensions, so
  from Prompt 1 on every change there is recorded in `Docs/vertical-slice/VENDOR_PATCHES.md` with its reason, and all vendored
  tests keep passing (decision VS-02).
* **One combat engine.** Previews, AI, legal-action generation, threat overlays and committed results all call the same
  functions (`RulesEngine.Validate/Apply`, `LegalActions`, `RosterCombat`, `Overlays`). There is no second damage formula.
* **Presentation never decides outcomes.** Physics, animation timing and camera motion never change legal actions or results.
* **Protect uncommitted work.** Never reset, stash, check out over or overwrite the main checkout (`E:\ClaudeCode\RR3`), which
  holds uncommitted work by another agent. Use the shared git stash only by tagged entry, never bare.
* **Evidence, not claims.** Completion means working implementation, tests actually run, runtime behaviour actually observed,
  the modified files, and remaining gaps. Compilation is not playability. A missing tool is a recorded verification gap; no
  screenshot or result is ever fabricated. AI mirror matches are not human playtests.
* Reversible, documented defaults for noncritical open choices; no silent override of a confirmed rule; no unrelated refactors.
* At every milestone: read these four documents, inspect current code, reuse working systems, implement, validate, and update
  [STATE.md](STATE.md) and [ACCEPTANCE.md](ACCEPTANCE.md).
