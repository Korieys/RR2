# Vertical slice state

Updated 2026-10-19, end of Prompt 0 (audit). Branch `claude/rule-ruin-vertical-slice-62633b`, based on `b23e354`.

| Milestone | State |
|---|---|
| Prompt 0: contract and audit | Done (this document, [PROJECT_CONTRACT](PROJECT_CONTRACT.md), [DECISIONS](DECISIONS.md), [ACCEPTANCE](ACCEPTANCE.md)) |
| Prompt 1: authoritative deterministic combat | Done and verified (see "Prompt 1 result"). |
| Prompts 2 to 5: battlefield, flagship protection, two fleets, the encounter | Done and verified (see "Prompts 2 to 5 result"). Checkpoint after Prompt 5: the tactical hook works by the rules; human playtesting has not happened. |
| Prompts 6 to 12 | In progress. The creator authorised working through the whole pack on 2026-10-19; CR-01 is still open, so presentation changes stay small and are listed for the eventual merge. |

## Verified baseline

**Tests, this worktree (2026-10-19 06:27):** `Tools/SliceTests` built and ran: **167 passed, 0 failed** (146 vendored Commandfall,
21 slice). The test project file had to be copied in from the main checkout because `*.csproj` is gitignored (defect D-02, fixed by
committing it).

**Runtime, newest build (2026-10-19 06:29):** `E:\ClaudeCode\RR3\Builds\Units\RuleAndRuin.exe` (built 05:42 from the main checkout
with its uncommitted work), run with `-rrTour` and a disposable save at 1920x1080 windowed, vsync on. Observed in the tour log
([baseline/tour_log.txt](baseline/tour_log.txt)):
* Galaxy, Aurelith system, planet inspection and fleet views captured; UI input capture checks true.
* Raid contact on day 16 at Aurelith; battle line of 6 Silex ships at 29 of 30 points.
* Orbital battle: Silex won by leader elimination ("CommanderEliminated") after round 10 and 114 commands. Replaying the battle
  from its setup and command log gave an identical final state. "Animated combat replay is identical to authoritative final
  state" passed.
* Result applied once (applied battles 1); the orbit stayed Silex, ownership unchanged, conquest stage None.
* Save digest `c21758289e4600fe`, after load `c21758289e4600fe`: equal.
* Frame times were capped by vsync at 144 Hz (median about 6.9 ms) and are **not** a performance measurement.

Curated frames: [galaxy](baseline/g01_galaxy_overview.jpg), [hero system](baseline/s01_hero_system.jpg),
[battle preparation](baseline/b01_battle_preparation.jpg), [battle start](baseline/b02_battle_start.jpg),
[battle, activation 6](baseline/b04_battle_6.jpg), [result](baseline/b07_result.jpg),
[galaxy after battle](baseline/r02_after_battle_galaxy.jpg). The committed tree (`b23e354`) has its own evidence in
`Docs/Evidence/final2__*` and `Captures/final2` in the main checkout; no new build of the committed tree was made (it would only
duplicate that evidence and cost a cold Library import).

## Implementation map

What exists today, against what each prompt needs. File paths are under `Assets/RR` unless stated.

| Prompt | Exists and is reusable | Gaps to build |
|---|---|---|
| 1 Rules foundation | One command type for every issuer (`Vendor/Core/Commands/Command.cs`); `RulesEngine.Validate/Apply`; `LegalActions.EnumerateRoster`; integer combat with a breakdown (`RosterCombat`, `HitResult.Breakdown`); structured `GameEvent` outcomes; stale-revision (`ExpectedRevision`) and duplicate (`Nonce`, memory 64) rejection; seat checks; `MatchSession.Replay`; FNV-1a state hash over deterministic serialization (decision A6); `ReplayFile`; no randomness in rules; Full Roster activation model (one unit per activation, move then one action, once per round, alternating). Slice tests: determinism, preview equals resolution, repeated input. | A single rules-version id stamped in battle setup, saves and replays (VS-03). A battle snapshot in the save (CR-15) so a mid-battle reload cannot reroll and previews still match. A divergence inspector (first differing command index by per-step hashes). An orbital-specific test matrix for the categories the gate lists. Confirm the state hash excludes cosmetic data. |
| 2 Battlefield and controls | 115-hex orbital boards (already three times the original 37; CR-03); `TopologyValidator` with geometric dead-end classes and a two-dead-end cap (CR-04); terrain rules explicit (B8); centre objective; deployment territories; `BattlePresentation.Input` (selection, gold moves, red targets, hover preview, ability key, wait, threat map toggle); `RulesEngine.ExplainUnreachable`; `Overlays.FirstBlocker`. | A contested passage layout for the trade-passage scenario, validated; reachability per movement pattern to objectives; facing decision (CR-11); manual deployment (CR-12); ownership cues beyond colour; stale controls after battle end (D-05); runtime captures of centre, flank and threat preview. |
| 3 Flagship protection | Immediate end on leader death (`RosterRules.CheckLeaders`, C6 simultaneous rule); automatic guard interception (R4) with specific eligibility (slice test `GuardRules_AreSpecific...`); threat overlay for "could attack at its next activation" (`Overlays.ThreatsTo`, P34); flagship warning toast; `ProximityAlone_IsNotProtection`. | A lethal-threat overlay with a stated activation horizon from the same generator; interception range, eligibility and remaining uses shown; attack preview from raw damage to result including interception and "battle ends"; decisive-action inspection on the result screen (absent today); the fixture set in the gate. |
| 4 Two fleets | Codex 0.1 has 10 orbital classes each for Pygmaei and Inhumanus, generated into the catalog with executable specs (`RosterSpecs.Pygmaei.cs`, `RosterSpecs.Inhumanus.cs`) and covered by roster tests; army validation (30 points, 8 ships, one leader). | Pick 4 to 5 classes each; heat doctrine (CR-09) and contract doctrine (CR-10); faction cards and counters; load-time content validation for the slice roster; two legal compositions per faction. **No ship models exist for either faction** (D-04). |
| 5 Encounter and AI | `RosterAI` (Tactical) built on the legal-action generator, route-aware (A8); auto-deployment. | Immediate-win and avoidable-threat recognition, protection use, stable tie-break (CR-14), non-blocking decision; the curated trade-passage scenario; a training variant; local control of either faction; rematch, restart, scenario select, decisive-sequence review. |
| 6 Campaign loop | Galaxy with 44 systems and 8 sectors, lanes, fleets, pausable day clock, engagements, battle preparation, `BattleBridge`, exactly-once result keyed by battle id, orbit and blockade separate from ownership, retreat, carried damage, versioned save. Pygmaei (Forgefall, Ironmouth, Cinderhold) and Inhumanus (Ledgerhold, Pale Exchange, Dividend) systems exist. | A Pygmaei and Inhumanus scenario (today `SliceScenario` hard-codes Silex against Cräigon); passage access as its own state; declared war objective; enemy survivors' behaviour after leader defeat; failure recovery route; save migration. |
| 7 Persistent capital ships | Stable ship uids, carried hull damage, losses persisted. | Names, modules and slots, refits, repairs, service records, hangar view, destroyed-ship records. |
| 8 Negotiation and capture | Captured and Boarded statuses in battle; *Asset Tow* (BOARD) for corvettes and frigates; captures counted in results. | Offer panel, revision-bound offers, deterministic AI acceptance, capture-enabled scenario policy, atomic settlement. |
| 9 Consequences | Battle wrecks during a battle (B7); a wreck hulk model; `MapFeature` data. | Persistent wreck records, salvage, event history, bounded political reactions, markers from authoritative state. |
| 10 Eclipse expedition | The Eclipse Sector: 7 systems, its own look, routes through it. No gameplay is attached to it. | Reconnaissance choice, persisted conditions, authored encounter chain, sensor limits. Lore source to read first: `C:\Users\jacob\Downloads\Rule and Ruin_ Lore Sheet (1).docx`. |
| 11 Premium pass | Main checkout (uncommitted): Command Glass, Prismatic and Cinematic UI, UI kit, motion system, 49 Silex and Cräigon unit models with LODs and animation, ship effects. | Pygmaei and Inhumanus ships; integration of that work with the slice surfaces; ship-design reference package. |
| 12 Validation and build | Tour, soak, ship sheet, review automation; stress fixture. | Full-journey automation for the new loop; local playtest metrics (Commandfall's T1 design exists to port); cold-playtest protocol; package. |

## Outstanding defects and risks

| ID | Defect or risk | Severity | Owner prompt |
|---|---|---|---|
| D-01 | About 80 uncommitted files in the main checkout (2026-10-17 to 19) are in no commit. They could be lost, and this branch diverges from them (CR-01). | Blocker for Prompt 2 | Creator decision now |
| D-02 | `Tools/SliceTests/SliceTests.csproj` was gitignored, so a fresh clone cannot run the tests. | Fixed in Prompt 0 (VS-06) | 0 |
| D-03 | `../DECISIONS.md` says the AI's only randomness is the Beginner level; Tactical adds a seeded jitter (CR-14). | Low | 5 |
| D-04 | No ship models for Pygmaei or Inhumanus. | High for Prompts 4, 11 | 4 (temporary), 11 |
| D-05 | After the battle ends, the action bar still shows enabled ability buttons and the "Your flagship is within reach" toast stays up (`baseline/b07_result.jpg`). | Medium | 2/3 |
| D-06 | The result screen cannot show the decisive action. | Gate item | 3 |
| D-07 | No mid-battle save (CR-15). | Fixed in Prompt 1 | 1 |
| D-11 | Inspector shows an active as "ready" while the ship is DISABLED. | Low | 2/3 |
| D-12 | AI recreated after a mid-battle reload may break ties differently (CR-14). | Low | 5 |
| D-08 | The generated catalog repeats the `LEADER` tag on both flagships (`RosterCatalog.g.cs`, Pygmaei and Inhumanus flagship entries). Harmless today; content validation should catch it. | Low | 4 |
| D-09 | HANDOFF known weaknesses remain (Inhumanus title under the inspector at the default overview framing, provisional emblems, transition double image). | Low | 11 |
| D-10 | Vendored patches will diverge from Commandfall upstream (VS-02); an upstream port is not scheduled. | Medium, deferred | 12 |

## Milestone dependencies

```
P0 audit
 └─ P1 rules foundation ─┬─ P2 battlefield and controls ─┐   (P2 needs CR-01 settled)
                         └─ P3 flagship protection ──────┤
                                                          └─ P4 two fleets ─ P5 encounter and AI  [checkpoint: tactical hook]
                                                                              └─ P6 campaign ─ P7 capital ships ─ P8 negotiation  [checkpoint: connected campaign]
                                                                                                                  └─ P9 consequences ─ P10 Eclipse
P11 premium pass: after P9 for integration; Pygmaei and Inhumanus ship modelling can run in parallel from the end of P4 (isolated Blender work).
P12 validation and build: last.
```

Parallel work that is safe: Blender ship art (after P4 fixes the classes), audio, documentation. Not safe in parallel: anything in
`Vendor/Core`, `Strategy`, `SaveGame`, `BattleBridge`, `BattlePresentation*`, `BattleHud`, `UIRoot`, `StrategyHud`.

## Prompt 1 result (2026-10-19)

**Implemented.** `Assets/RR/Strategy/BattleSession.cs`: the authoritative battle session (VS-07) with `Submit(command, controller)`,
`Preview(command)`, the accepted command log and per-command state hashes, `Snapshot()`, `Restore()` (replay under the current rules,
refused on any rules, content or hash difference), `FindDivergence()` (first differing command) and `DescribeLog()`. `RulesVersion`
(VS-03): `commandfall-0.15.0+cf-content-1.3+cf-roster-0.1+vs-1` plus a content fingerprint. `BattleSetup.Deployment` stores explicit
deployment. The save carries a battle in progress under an optional `battle` key (VS-09). The battle view, the AI and the tour submit
through the session; F5 and F9 work during a battle. No vendored rules file changed ([VENDOR_PATCHES](VENDOR_PATCHES.md), vs-1).

**Tests.** 183/183 (146 vendored, 37 slice). New: `OrbitalRulesMatrixTests` (movement and blocked paths, range and line of sight,
armour, shields and cover exactly, one move and one action per activation with no second attack through an ability, cooldowns,
round changes, no dice) and `BattleSessionTests` (full replay, identical hashes after every command in two independent games, divergence
at the right index for a tampered hash, a forged command and a changed setup, mid-battle save and load with previews equal before and
after and equal to the committed result, stored deployment, stale, duplicate, wrong-seat, host-only, off-board and enemy-unit commands
rejected without changing state or log, refusal of other rules, other content, damaged and orphaned records, exactly-once clearing).

**Runtime** (`Builds/VS`, 1920x1080 windowed, disposable save; [log](evidence/p1/tour_log.txt)): raid contact on day 16; F5 then F9
after 24 orders restored the same battle (hash `276538a3860c7eab`, identical in two separate runs) with identical previews for all 12
occupied hexes ([frame](evidence/p1/b06_after_midbattle_load.jpg)); leader elimination in round 9 after 105 commands; replay from the
record identical; result applied once; save and load digests equal. A first run reported a digest mismatch and "applied battles 0":
the tour itself kept a reference to the galaxy state that the mid-battle load replaced. The tour now re-reads it; the game was right
(the applied count after load was 1 in both runs).

**Found, not yet fixed.** D-11: the battle inspector shows an active ability as "ready" while the ship is DISABLED
([frame](evidence/p1/b06_after_midbattle_load.jpg), Throne Ark). D-12: after a mid-battle reload the opponent AI is recreated, so its
seeded tie-break may choose differently than without the reload (state never diverges; CR-14, Prompt 5).

## Prompts 2 to 5 result (2026-10-19)

**Rules** (vendored patch vs-2, [VENDOR_PATCHES](VENDOR_PATCHES.md); decisions VS-10 to VS-18): the `trade_passage` battlefield at
Umbra; capital firing arcs with a turn order; Pygmaei industrial overclock with visible heat; Inhumanus fleet contracts with disclosed
arrivals; the slice roster of five classes per faction with two compositions each, authored counters and load-time validation
(`SliceRoster.Validate`, shown as an error at start-up if anything is wrong). The enumerator gained a width parameter so the
command-threat analysis never misses a distant area strike.

**Strategy layer:** `CommandThreat` (lethal lines for the next enemy activation, round focus fire, `AttackExplainer`), `SliceAI`,
`PassageScenario` (challenge, training, either faction, hotseat), `BattleNarrator` and `BattleSession.Narration`/`Decisive`.

**Game:** the Command Threat panel, overclock (O) and turn (R) modes, arc fan, facing on the model, heat and contract labels, entry
markers, dashed enemy rings (ownership by pattern and colour), an inspector that shows heat, facing, contract, "Protected by" and
intercepts left; the AI on a worker thread; F6 scenario menu with fleet cards and doctrines; a skirmish result screen with the decisive
action, the inspectable sequence, rematch, swap sides, scenario menu and return. Generated models for the ten classes and the escort
token, with provisional palettes. D-05 (controls enabled after the battle) and D-11 ("ready" while DISABLED) are fixed; D-12 is fixed
by the stable AI seed.

**Tests:** 214/214 (146 vendored). New: `PassageBattlefieldTests` (validator, per-pattern reachability, the DIAG finding, arcs
and turns), `FlagshipProtectionTests` (the seven Prompt 3 fixtures plus "disabling is not an instant win" and "the threat analysis never
claims safety or touches the board"), `DoctrineTests` (heat boundaries, cooling, exploitability, contract accounting, arrival and
blocking, roster legality), `PassageEncounterTests` (immediate win, defensive response, stable AI, the rush countered, two winning lines
against specific mistakes, AI mirror replay, training, narration).

**Runtime** (`Builds/VS`, 1920x1080, `-rrPassageTour`; [log](evidence/p2-5/tour_log.txt)): the menu-driven challenge as Pygmaei;
the flank frigate's normal and overclocked reach with the heat preview; the capital arc; the threat panel (5 ships, 1,223 orders
checked); an attack preview equal to the committed result; save and load in the middle of the skirmish restored the same battle; play
to the end with the decisive action and the last five orders narrated, and the replay from the record identical; swap sides, restart,
training and two players at one machine (orders for both seats, the AI never acted). Frames: [start](evidence/p2-5/p01_passage_start.jpg),
[overclock](evidence/p2-5/p03_overclock_reach.jpg), [arc](evidence/p2-5/p04_capital_arc.jpg), [threat](evidence/p2-5/p05_threat_view.jpg),
[preview](evidence/p2-5/p06_attack_preview.jpg), [result](evidence/p2-5/p07_result.jpg), [training](evidence/p2-5/p09_training.jpg).

**Balance evidence (AI and scripted pilots, not people):** carried hull 11, any flagship position: raiders won in round 1. Hull 17,
flagship central: AI defender beat the AI attacker (round 6), the simplistic rush (round 3) and four scripted raider plans (west flank,
east flank, passage push, hold then strike); a defender driving its flagship forward lost in round 2. Hull 17, flagship in the east
corner: the east-flank plan won in round 4. Automated battles last 40 to 90 orders (about 3 minutes of automated play with animations);
human duration is unmeasured.

**Known gaps:** no interactive deployment screen (curated or automatic deployment only); contracts are chosen through army data, with
no campaign battle-preparation control yet; the slowest AI decision measured about 2 seconds (worker thread, the interface stays live);
training lessons are guidance, not enforced steps; ship art is first-pass and unapproved; human playtesting has not happened.

## Prompt 1 plan (as written at the end of Prompt 0)

Most of the rules foundation exists; Prompt 1 is verification and gap-filling, not a new engine.
1. Add the rules-version id (VS-03) to `MatchConfig`-adjacent setup, battle snapshots and the save, and reject a snapshot whose
   version differs.
2. Persist the battle setup and accepted command log in the save; reload rebuilds state by replay and checks the stored hash
   (CR-15). Keep save format compatibility (version 1 files without a battle still load).
3. Add a divergence report: replay a log step by step and name the first command whose post-state hash differs.
4. Write an orbital test matrix with orbital units on `orbital_gate`: movement and blocked paths, range and line of sight, armour,
   shields and cover, one action per activation (no second attack through an ability), cooldowns, stale, duplicate, wrong-seat and
   illegal commands leaving state unchanged, round change, replay identity, and preview equal to commit before and after a
   snapshot reload.
5. Confirm the state hash covers only authoritative data; record any vendored change in `VENDOR_PATCHES.md`.

Precondition for Prompt 2: CR-01 settled (the main checkout's work committed and this branch rebased onto it).
