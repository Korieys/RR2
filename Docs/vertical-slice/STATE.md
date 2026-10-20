# Vertical slice state

Updated 2026-10-19, end of Prompt 12 (the whole pack; Prompt 0 audit below). Branch `claude/rule-ruin-vertical-slice-62633b`, based on `b23e354`.

| Milestone | State |
|---|---|
| Prompt 0: contract and audit | Done (this document, [PROJECT_CONTRACT](PROJECT_CONTRACT.md), [DECISIONS](DECISIONS.md), [ACCEPTANCE](ACCEPTANCE.md)) |
| Prompt 1: authoritative deterministic combat | Done and verified (see "Prompt 1 result"). |
| Prompts 2 to 5: battlefield, flagship protection, two fleets, the encounter | Done and verified (see "Prompts 2 to 5 result"). Checkpoint after Prompt 5: the tactical hook works by the rules; human playtesting has not happened. |
| Prompts 6 to 10: campaign, capital ships, negotiation and capture, consequences, the Eclipse expedition | Done and verified in tests and an automated tour (see "Prompts 6 to 10 result"). Not yet played by a person. |
| Prompts 11 and 12: presentation pass, validation and packaging | Done, honestly scoped (see "Prompt 11 result", "Prompt 12 result" and "Status at the end of the pack"). Human playtesting and premium art are blocked on people. |

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
| D-19 | The mouse wheel barely zoomed: the code divided the wheel by 120 (the old Windows range), but Input System 1.20 reports 1 per notch, so a notch zoomed about 0.13 % and closing on a ship in battle took some 860 notches. Fixed (2026-10-19, after the pack): either range is read correctly, 20 % per notch, zoom aims at the point under the cursor, `+`/`−` zoom, and double-click or C flies the camera to a ship. Check: `-rrCameraCheck` ([log](evidence/camera/tour_log.txt)): 7 notches take the battle camera from 26.5 to its closest 6.0 with the aimed ship within 65 px of the cursor; the fly-to centres a ship in 0.35 s. The physical wheel was not exercised by automation. | High (controls) | Fixed |
| D-10 | Vendored patches will diverge from Commandfall upstream (VS-02); an upstream port is not scheduled. | Medium, deferred | 12 |
| D-18 | `-rrSavePath`, documented in Prompt 0 as the way to keep automation away from the player's save, was never implemented: every automated tour this session (the Prompt 0 baseline on the main build, Prompts 1 to 10 on `Builds/VS`) saved to the real slot `%USERPROFILE%\AppData\LocalLow\Rule and Ruin\Rule and Ruin Slice\saves\slot1.json`. Whatever save was there before 2026-10-19 was overwritten and cannot be recovered; the file now holds a tour's campaign. Fixed: `GameRoot.SavePath` honours `-rrSavePath`, and local metrics follow it. | High (data loss, reported to the creator) | Fixed in Prompt 12 |

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

## Prompts 6 to 10 result (2026-10-19)

**Rules** (vendored patch vs-3, [VENDOR_PATCHES](VENDOR_PATCHES.md); decisions VS-19 to VS-26): capital-ship modules applied by the
vendored stat computation; lawful capture by boarding under a scenario policy (`VictoryReason.LeaderCaptured`); the wrecked passage
and the two expedition drifts, all through the ordinary validator. Rules version `...+vs-3`; battles recorded under vs-2 are refused.

**Strategy layer:** `SliceCampaign` (campaign start, objectives, access and toll, relations with bounded timed modifiers, ship
profiles and service records, refit, repair, rename, wrecks, salvage, history, commissioning a replacement as a new ship),
`Negotiation` (offers bound to a battle revision, the pressure-versus-cost verdict with every factor listed, settlement as one rules
command), `Expeditions` (fixed conditions from the seed, reconnaissance, withdrawal, a fogged battle), save format version 2.

**Game:** the passage campaign from the F6 menu (either faction); a compact campaign panel (passage, objective, actions; details on
request); the hangar with a live 3D view of the actual hull and its modules, rename, repair, refit with preview; battle preparation
with the campaign's stakes, the named flagship and its modules, and "Offer terms instead"; terms in battle (Offer terms, once per
round) and boarding mode (B); the campaign result with the decisive action and every resulting history event; history and service
records; salvage; the expedition briefing and its fogged battle (unseen ships are not drawn, their cards say "out of sight", every
preview and the threat analysis use the fogged view). Modules are separate meshes fitted to each hull from measured data
([DESIGN_REFERENCE](DESIGN_REFERENCE.md)).

**Defects found and fixed while verifying:**
* D-13 (fixed): the slice AI seeded its planner from the full state's hash, so under fog of war an unseen ship's position could change
  its decision. Hidden ships are now parked on one hex in the AI's view (`SliceAI.Known`); `ExpeditionTests.UnderFog_...` covers it.
* D-14 (fixed): `Negotiation.Decide` read the full state when the other side offered to yield; it now reads the decider's view.
* D-15 (fixed): the top bar kept the previous player's name after a new campaign; key systems listed the Aurelith systems; a new
  campaign announced "Loaded the save"; the hangar name field and the terms dropdown were unreadable (light text on a light field).
* D-16 (fixed): modules were sized for the hull's bounds, so they floated off the narrower Pygmaei spine and stood off the Inhumanus
  rings; they are now fitted per hull from ray-cast measurements (`ShipMounts.g.cs`).
* D-17 (fixed): the campaign result repeated the decisive action inside the battle's history line; the campaign panel overlapped the
  fleet outliner and stayed up behind open windows.

**Tests:** 235/235 before the Prompt 12 additions (146 vendored, 89 slice). New in `CampaignTests`: `PassageCampaignTests`
(start from the existing galaxy; success and failure galaxy to battle to galaxy with saves at every stage; a bounded recovery route;
retreat), `CapitalShipTests` (rename, refit, fight, return, repair, save, deploy again as the same ship; illegal and unaffordable refits;
destruction keeps the record, never duplicates modules, and a replacement is a new ship), `NegotiationTests` (accepted settlement
applied atomically and once; stale, impossible and invalid-custody offers refused; a rejected offer followed by combat with the
verdict explained; the AI conceding under pressure; lawful capture's preconditions; each ending changing the campaign differently),
`ConsequenceTests` (two endings from separate saves; finite, previewed, reload-proof salvage; bounded once-only reactions),
`ExpeditionTests` (conditions fixed at creation; reconnaissance changes knowledge, not odds; withdrawal; the fogged battle resolves
once; the AI plans only from what it can see).

**Runtime** (`Builds/VS`, 1920x1080, `-rrCampaignTour`; [log](evidence/p6-10/tour_log.txt), 0 exceptions): a new Pygmaei campaign;
the hangar before and after fitting afterburners (hull 20 to 18, module visible); war declared and the fleet sent; contact at Umbra on
day 29 with the stakes in battle preparation; the terms form; the campaign battle on `trade_passage` with the capture policy and the
afterburners in the army; save and load after 10 orders restored the same battle; the Executive Flagship destroyed by an overclocked
Skirmish Frigate (the decisive action); access to the Pygmaei, the objective secured, standing −20 (flagship loss, 60 days), the wreck
of Fiduciary Dawn at Umbra, four history events; strategic save and load gave an identical digest; the fleet sailed to Gloam, scouted
the drift and fought the fogged expedition (3 enemy ships on the board, 2 visible to the player, exactly 2 drawn) and secured the
derelict (a spare Ablative plating). Frames: [start](evidence/p6-10/c01_campaign_start.jpg),
[hangar](evidence/p6-10/c03_hangar_refitted.jpg), [battle preparation](evidence/p6-10/c04_battle_prep_stakes.jpg),
[terms](evidence/p6-10/c05_terms_form.jpg), [battle](evidence/p6-10/c06_campaign_battle.jpg), [result](evidence/p6-10/c07_campaign_result.jpg),
[after](evidence/p6-10/c08_after_battle.jpg), [history](evidence/p6-10/c09_history.jpg), [expedition](evidence/p6-10/c10_expedition_briefing.jpg),
[fog](evidence/p6-10/c11_expedition_fog.jpg), [expedition result](evidence/p6-10/c12_expedition_result.jpg). These frames predate
the D-15 to D-17 fixes; the Prompt 11 evidence shows the result.

**Checkpoint after Prompt 8** (encounter, persistent ship and political consequences as one experience): in the tour, the refit the
player chose (afterburners) went into the battle, the battle's decisive action is the one the result screen and the history both
quote, and the galaxy afterwards shows access, standing with its cause, and a wreck that can be salvaged once. The chain
holds mechanically. Whether a person experiences it as one story is unverified (no human playtest).

**Known gaps:** captures and settlements are exercised in tests, not in the automated tour (the tour's battle ended by destruction);
the AI never proposes terms itself (it only answers); the in-battle offer is lost if the battle is saved and reloaded in the same round
(one extra offer becomes possible); a ship that fires under fog reveals where it fired from, and an order into a hex an unseen ship
holds is refused (VS-26); fog has no hex shading, only hidden ships; the fleet's voyage to Umbra takes 29 days, which is slow at
the default speed.

## Prompt 11 result (2026-10-19): presentation pass, honestly scoped

The pack asks for a premium pass. What was done is a **targeted pass on the slice's own surfaces**, driven by defects seen in runtime
frames, not a new design system: the existing UI kit and the uncommitted Codex UI work in the main checkout are left alone while
CR-01 is open, so a shared typography and spacing overhaul would collide with that work. The art remains first-pass.

**Changed in the game:**
* The battlefield stays dominant: the campaign panel is compact by default (passage, objective, actions; details on request) and
  hides while a window is open; the encounter's stakes moved into battle preparation, where the decision is made.
* A skippable **decisive-action camera** (about two seconds; click, Space, Escape or Enter skips it; reduced motion skips it): it
  frames the already resolved decisive action, with the rules' own description, before the result screen. It never changes state.
* Legibility: form inputs (names, terms) are light on dark; the result no longer repeats the decisive action; the top bar, key
  systems and toasts say what is true for the current campaign; access wording says "you" or "them".
* Modules are fitted to each hull from measured data, so a refit visibly changes the silhouette without parts floating.
* Ownership cues stay colour-independent (dashed enemy rings, rim tint, cards); under fog, unseen ships are absent rather than
  dimmed, and their cards say so.

**Ship-design reference package:** [DESIGN_REFERENCE](DESIGN_REFERENCE.md): silhouettes to scale (top, side, and at 96 px),
shaded views, both flagships with each module, configuration identifiers, hull and module separation, and what could inform
miniatures. Rendered from the shipped FBX files by `Tools/Blender/rr_design_sheet.py`; not print- or manufacturing-ready.

**Measured performance** (final build, `-rrCampaignTour -rrUncapped`, vsync off, no frame cap; RTX 5060, i5-14400F, 32 GB,
Windows 11 26200, Direct3D 12, 1920x1080 windowed, quality Ultra, render scale 1.0; [log](evidence/p11-12/campaign_tour_log.txt)):

| Scene | Median | p95 | p99 | Max | Notes |
|---|---|---|---|---|---|
| Galaxy, idle | 2.62 ms | 5.54 ms | 5.86 ms | 7.01 ms | 3 gen-0 GCs in 300 frames |
| Galaxy with the campaign panel | 2.56 ms | 5.43 ms | 5.53 ms | 7.15 ms | 2 gen-0 GCs |
| Campaign battle, player's turn | 1.65 ms | 5.43 ms | 6.15 ms | 6.17 ms | no GC |
| Expedition battle under fog | 1.65 ms | 5.38 ms | 6.12 ms | 6.15 ms | no GC |

The provisional target (stable 60 FPS at 1080p, 16.7 ms) is met on this machine with a large margin in these scenes; that is one
machine, idle-frame measurements, not a claim for lower-end hardware. The passage battle with vsync on held 144 Hz (6.94 ms median),
the same as before the pass. Not measured: frames during heavy combat effects, the AI's worker-thread decisions (about 2 s at worst,
measured in Prompt 5), memory over a long session (the existing `-rrSoak` was not rerun). No optimisation was needed or done.

**Visual regression check:** before and after pairs of the same tour steps:
[passage start](evidence/p11-12/compare_p01_passage_start.jpg), [threat view](evidence/p11-12/compare_p05_threat_view.jpg),
[passage result](evidence/p11-12/compare_p07_result.jpg), [campaign panel](evidence/p11-12/compare_c01_campaign_panel.jpg),
[campaign result](evidence/p11-12/compare_c07_campaign_result.jpg). The battle views are unchanged apart from the new delegate buttons;
the campaign panel is smaller; the campaign result no longer repeats the decisive action. No regression found by eye. (The passage
tour's turn-preview line is empty in both runs: the tour samples it during the enemy's activation; a tour weakness, not a game change.)

### Asset gaps

| Gap | Severity | Note |
|---|---|---|
| All ten slice classes, the escort token and the modules are first-pass generated art, unapproved | High for a premium slice | Silhouettes separate the factions at 96 px; within a faction, cruiser, destroyer and frigate share one outline family ([DESIGN_REFERENCE](DESIGN_REFERENCE.md)) |
| Modules are generic, not faction-styled; on the Inhumanus flagship they ride outboard of the gravity rings | Medium | Needs per-faction module art |
| No destruction or wreck effect for a flagship; the decisive-action camera arrives after the ship has vanished | Medium | A wreck model and a break-up effect would make the decisive moment legible |
| No fog-of-war hex shading; only unseen ships are hidden | Medium | Players cannot see the edge of their sensors |
| No hangar backdrop or lighting rig; the preview renders on a flat colour | Low | |
| No sound for refits, terms, capture or the decisive camera | Low | Existing UI sounds are reused |
| The emblems and the faction colours are the existing provisional ones (D-09) | Low | |
| No licensed third-party ship assets were used or bought | — | Pack: no paid purchases |

## Prompt 12 result (2026-10-19)

**The complete journey, through the game's own entry points** (final build, `-rrCampaignTour`; [log](evidence/p11-12/campaign_tour_log.txt),
0 exceptions): select the Pygmaei; inspect and refit the named flagship (afterburners); commit to the passage objective; battle
preparation shows the stakes; a mid-battle save and load restores the same battle; the rest of the battle is **delegated**; the
decisive-action camera, then the result (Fiduciary Dawn destroyed, access won, standing −20, a wreck); history; a strategic save and
load with an identical digest; the optional Eclipse expedition under fog (secured, a spare module); home to the shipyard (repair
refused correctly: the hull was whole) and the spare module fitted free; the Inhumanus **counter-offensive** (it commissioned a
replacement flagship, a new ship, and declared war to retake the passage); a **second encounter** at Umbra on day 87, lost on the
round limit, and access passing to the Inhumanus. Frames: [delegated](evidence/p11-12/c06b_delegated.jpg),
[decisive camera](evidence/p11-12/c06c_decisive_camera.jpg), [hangar after the campaign](evidence/p11-12/c13_hangar_after_campaign.jpg),
[second encounter](evidence/p11-12/c15_second_encounter_prep.jpg), [second result](evidence/p11-12/c16_second_result.jpg).
The player's orders came from the slice AI (a pilot), not a person.

**Added:**
* **Bounded delegation** (the panel's Delegate buttons, G): the same `SliceAI`, the same rules and the same fogged knowledge play the
  player's side until the end of the round or the battle; the player can take command back at any decision; the disclosure is shown
  whenever it is active. No autoresolve and no casualty promises exist.
* **The rival's counter-offensive** (`SliceCampaign.Rival`, experimental): 20 days after losing the passage in battle the loser comes
  back, commissioning a replacement flagship first if it must (and can pay); announced in advance in the campaign panel; prevented by
  a truce; bounded by the three-attempt limit; keyed so it happens once.
* **Local playtest metrics** (`PlaytestMetrics`): `playtest_metrics.jsonl` beside the save, labelled `automation` or `player`; time to
  the first meaningful decision, battle duration and orders, invalid actions, previews inspected, refits, result type; `-rrNoMetrics`.
* `-rrSavePath` now works (D-18).
* **Cold-playtest protocol:** [PLAYTEST_PROTOCOL](PLAYTEST_PROTOCOL.md).

**Failure, abuse and incentive checks** (kept as automated regressions; `ValidationTests`, 10): damaged saves (empty, truncated,
wrong schema, newer version) refused with a reason and never an exception; a tampered battle record refused while the untouched record
restores exactly; repeated result submission applied once; rapid repeated refits and declarations take effect once and are paid once;
depleted resources refuse refits cleanly; contract and module duplication refused; overclocking cannot be sustained (after the first
two, at most one activation in two, each paid for with an overheated activation); terms are not a free win at the start of a battle; a
second encounter after a win; no counter-offensive during a truce. Already covered elsewhere: leader defeat, failed boarding
(`LawfulCapture...`), legal retreat, rejected negotiation, simultaneous leader destruction, mid-battle reloads, salvage that cannot
loop, guard interception once per round, the unsupported flagship rush losing to a careful defence. No dominant strategy was found in
these checks; that is not a balance claim (see below).

**Tests:** 245/245 (146 vendored Commandfall, 99 slice), run outside the editor with the command in [PROJECT_CONTRACT](PROJECT_CONTRACT.md).

**Packaging:** `Builds/Release/RuleAndRuin_VerticalSlice_<revision>.zip`, built through the project's pipeline from a committed
revision, with launch instructions, controls, reset instructions, a rules summary, known issues, test results and the source revision.
Kept local; nothing was uploaded or published.

## Status at the end of the pack (2026-10-19)

**Implemented and verified** (tests and automated runtime tours): authoritative deterministic battles with records, replay and
mid-battle saves (Prompt 1); the passage battlefield, capital arcs, flagship protection and the Command Threat analysis (2, 3); two
asymmetric fleets with overclock and contracts (4); the encounter, its variants and the slice AI (5); the passage campaign loop with
saves at every stage (6); named, refitted, repaired flagships with service records (7); negotiation and lawful capture (8); wrecks,
salvage, standing and history (9); the fogged Eclipse expedition (10); the decisive-action camera, the compact panel, module fitting and
legibility fixes (11); delegation, the counter-offensive, local metrics, abuse regressions and packaging (12).

**Not verified by a person (blocked on testers):** usability, readability at play distance, whether players identify leaders and
understand previews, whether the campaign reads as one story, balance, and session length. No independent tester has played the slice;
[PLAYTEST_PROTOCOL](PLAYTEST_PROTOCOL.md) is ready. Automated tours are technical verification only.

**Blocked:**
* CR-01: the main checkout's uncommitted Codex work (UI kit, unit models, ground exercises) is still outside any commit; merging this
  branch with it needs the creator's decision. Ground gameplay exists only in that uncommitted work, and online play only in the separate
  Commandfall game, so neither has smoke tests on this branch.
* Premium art: approved Pygmaei and Inhumanus ship designs, emblems and module art.

**Deliberately deferred:**
* An interactive deployment screen and contract choice in campaign battle preparation (curated or automatic deployment only).
* The AI proposing terms itself; capture and settlement shown in an automated runtime tour (both are covered by tests).
* Fog-of-war hex shading; an uncontested rival fleet taking access without a battle (it waits at the passage).
* Porting the vendored rules patches (vs-2, vs-3) upstream to Commandfall (D-10).
* A shared design-system pass over every surface (after CR-01).

**Known issues:** listed in [KNOWN_ISSUES](KNOWN_ISSUES.md) and shipped with the package.

**Checkpoint after Prompt 12, the whole demonstration:** the central proof the pack names, "I changed my ship, created a tactical
opening, forced an outcome, and returned to a galaxy that reflected what I did", is mechanically complete in one continuous run: the
refit is in the battle, the decisive action is named and shown, and the galaxy afterwards shows access, standing with its cause, a wreck,
history and a rival who comes back. What is not established is that a person would say that sentence: the slice has not been played by
anyone outside development, the art is first-pass, and balance rests on AI and scripted pilots. This slice is complete as a
demonstration; the flagship game is not.

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
