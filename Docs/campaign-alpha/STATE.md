# Campaign alpha state

Source: `Rule_and_Ruin_Campaign_Alpha_Prompts_13_to_20.md` (supplied by the creator, 2026-10-20). This continues the vertical slice
([../vertical-slice/STATE.md](../vertical-slice/STATE.md), Prompts 0 to 12, whose contract, decisions and evidence stay in force).
Read this file, [DECISIONS.md](DECISIONS.md) and [ACCEPTANCE.md](ACCEPTANCE.md) at the start of every milestone.

Branch `claude/rule-ruin-prompts-integration-8d4fbe` (worktree), based on `a632cf6` (local `main` at the time: the slice, the
cinematic UI and units, the inside-out fix and the ship-name/hover pass). Engine and tools as in the slice contract: Unity
6000.6.5f1, URP, Windows x64; tests run outside the editor with the bundled dotnet (`Tools/SliceTests`).

| Milestone | State |
|---|---|
| Prompt 13: baseline and first independent session | **Skipped at the creator's request** (2026-10-20: "You can skip prompt 13 and start on prompt 14"). No independent-tester kit was prepared in this phase; the slice's [PLAYTEST_PROTOCOL](../vertical-slice/PLAYTEST_PROTOCOL.md) remains the closest thing. Baseline recorded below. |
| Prompt 14: a bounded campaign | Done and verified in tests and an automated runtime tour (see "Prompt 14 result"). Not played by a person. |
| Prompt 15: economy and supply | Done and verified in tests and an automated runtime tour (see "Prompt 15 result"). Not played by a person. |
| Prompt 16: orbital-to-planetary operations | Done and verified in tests and an automated runtime tour. Not played by a person. |
| Prompt 17: the strategic rival | Done and verified in tests and automated campaigns. Not played by a person. |
| Prompt 18: civilization progression | Done and verified in tests and the journeys. Not played by a person. |
| Prompt 19: replay variants and the content pipeline | Done and verified in tests and a content preview tour. Not played by a person. |
| Prompt 20: the private campaign alpha | Integrated, validated by automated journeys, persistence tests and a performance tour, packaged locally. **No human session has been run**; human evidence is pending. |

## Baseline (start of this phase)

* Revision `a632cf6`, clean worktree. `Tools/SliceTests`: **245 passed, 0 failed** (146 vendored Commandfall, 99 slice), 1,349 s on a loaded machine.
* Unity `Library` copied from the sibling worktree (`robocopy /E /XD PlayerDataCache Bee`); the first batch build compiled once
  before a newly added file was imported (`WarAutomation` unknown) and then succeeded in the same run.
* Player build: `Builds/VS/RuleAndRuin.exe` (local, ignored by git). Every runtime check below used `-rrSavePath` with a disposable
  file in the session scratchpad; the player's own save slot was not touched (the flag's handling was checked in `GameRoot.SavePath` first).

## Prompt 14 result: "The Passage War"

**Implemented** (`Assets/RR/Strategy/PassageWar*.cs`, decisions CA-01 to CA-06):
* A scenario configuration of the existing passage campaign: 10 active systems inside the 44-system galaxy, a friendly and a rival
  base, the contested passage at Umbra, the northern route through the Pirate Reach, and the optional Eclipse expedition at Gloam.
  Routes stay inside the scope; the freeform passage campaign and the Aurelith campaign are unchanged (F6 menu).
* The mandate (secure durable access, keep the designated worlds operational) with its exact conditions shown in the mandate window:
  victory by arms (hold through 3 consecutive settlements, every 10 days), victory by treaty (ratified at the next settlement), early
  end once the rival has no way left to contest, defeat (three failed objectives with the rival holding access, or no flagship-led
  fleet with nothing building and nothing affordable), inconclusive at day 240. What interrupts a hold is listed.
* Strategic time unchanged; an encounter stops time for both powers; settlements keyed by day.
* A strategic treaty (the negotiated route), with every factor of the verdict shown; truces now stop contact battles.
* Next-action affordances (declare, treaty, battle preparation, hangar, commission a replacement, send the fleet), a settlement report,
  a decision ledger, and an ending that recounts decisions and costs; "Continue freeform" afterwards.

**Defect found and fixed:** D-20. Each objective record carries the running count of failures, but three places summed the records
(the slice's own `DeclareObjective` and counter-offensive since Prompt 6/12, and the new mandate), so a second lost battle counted as
three and the runtime tour ended in defeat after two battles. `SliceCampaign.FailedAttempts` now takes the largest; regression test
`TwoFailedObjectives_CountAsTwo_NotThree`; the recovery test also caught it independently.

**Tests** (`PassageWarTests`, 12): scope and freeform; military route to victory with the ending; a lost battle that is not terminal
and a recovery that is paid for, built over its declared time and coherent whether the rematch is won or lost; reloads that cannot
reset holds, repeat settlements or change outcomes (two runs, one reloaded every day, end identically); a treaty route with no battle
at all (northern route, blockade of Dividend, payment, ratification); a treaty after a victory; truces; defeat only from declared
conditions with a warning first; the inconclusive end; time stopped during an encounter; D-20.

**Runtime** (`-rrWarTour`, [log](evidence/p14/tour_log.txt)): the scenario from the menu entry point; the mandate; a declined day-0
treaty with its factors; a deliberately lost first battle (the player's side passed every order); the mandate after the loss with the
warning and the replacement-flagship affordance; recovery through it; a second battle flown by the slice AI, also lost; the Defeat
ending with the ledger (that run predates the D-20 fix, which is why it ended after two losses); save and load after the ending
identical; time stays stopped after the ending. Frames: [mandate](evidence/p14/w01_mandate.jpg),
[after the loss](evidence/p14/w05_mandate_after_loss.jpg), [ending](evidence/p14/w09_ending.jpg).

**Observations (automation, not people):** a defender that won without damage, holding the centre with seven ships, beat a rebuilt
Pygmaei force twice (the second time with a replacement flagship and one frigate; in tests also with five rebuilt escorts) on the
round limit. That is a balance concern for the rematch, not a rules defect. Starting yields are generous (by day 29 the Pygmaei held
352 energy, 479 materials, 334 credits without spending), which is why Prompt 15 adds upkeep.

## Prompt 15 result: economy and supply

**Inventory of the existing economy:** four resources (energy, materials, credits, influence); daily world yields (blockaded worlds pay
nothing); Crystal Foundries; one project per world (commission a ship, build a foundry); passage tolls; repairs and refits paid in
resources; no upkeep, no supply, no trade before this milestone. The scenario exposes credits (upkeep, trade, treaties), materials
(repairs, stores, seizure) and energy; influence keeps its one use (reconnaissance).

**Implemented** (`Economy.cs`, `Supply.cs`, decisions CA-07 to CA-11): fleet upkeep at settlements with arrears instead of negative
balances (projects pause; repairs, refits, commissions and new projects are refused with the reason; income pays arrears first; the AI
is charged the same way); six local priorities in the existing project system with effect, enabled options and opportunity cost; a
forward dock with finite stores; a supply network derived from routes, access, truces, blockades and fleets holding position, with
announced threats and cuts; the mandate's hold requires supply to reach Umbra; convoy income requires supply to the free port; the
treasury window (settlement preview with inflows, upkeep, completions, restock and known threats, projected stock and the insolvency
consequence; the budget choice with what each option leaves) and the supply overlay on the galaxy.

**Tests** (`EconomySupplyTests`, 10): a working route and access; an announced disruption (the threatened segment and day), the cut,
rerouting by the northern route, a fully blocked route that stops the hold and says why, and recovery; treaty access and truces; a
blockaded own world and its recovery; the forward dock (offered with its opportunity cost, started once, empty at first, restocked only
while supplied, repairs from stores, exhausted stores, recovery); upkeep and insolvency (never below zero, arrears pause work and refuse
new projects, keyed settlements, recovery); the rival under the same accounting; two affordable plans with different trade-offs that
the budget cannot both cover (repair the veteran and build a forward dock, or another hull and a convoy office), both carried out from
the same save; convoy income depends on a real route; idempotent settlements across reloads.

**Runtime** (`-rrWarTour` on the Prompt 15 build, [log](evidence/p15/tour_log.txt)): 0 exceptions. The treasury window on day 0 (stock, the
settlement preview with upkeep of 52 credits for 26 fleet points, the projected stock, and the budget: a Rift Interceptor, the forward
dock, the ore drive and the convoy office, each with what it leaves) and the supply overlay (supplied lanes from Forgefall through
Cinderhold and Gloam and by the northern route to Shoal Haven; Umbra cut because the passage is contested; Dividend cut by the escort
holding it). A deliberately lost first battle; recovery through the affordances (a replacement flagship, then four Rift Interceptors,
each paid and built over 8 days, upkeep rising with the fleet from 6 to 34 credits a settlement); a second battle flown by the slice
AI, also lost (the rebuilt line of interceptors lost its flagship); the D-20 fix at runtime ("1 attempt left" after two losses);
no flagship afterwards but the means to build one, so no defeat, and the mandate ran out on day 240 (Inconclusive); save and load after
the ending identical; time stayed stopped. Frames: [treasury](evidence/p15/w02b_treasury.jpg), [supply overlay](evidence/p15/w02c_supply_overlay.jpg),
[recovered](evidence/p15/w06_recovered.jpg), [second battle](evidence/p15/w07_battle2_result.jpg), [inconclusive ending](evidence/p15/w09_ending.jpg).

**Found and fixed after the run:** upkeep entries at every settlement (including zero) filled the ending's decision list; upkeep is now
one summary line, and empty upkeep is not recorded. Two text glitches in the treasury ("Repair ...: ." for a whole hull; "supplied from
Forgefall via Forgefall").

**Balance concern (automation, not people):** income outgrows every cost. With upkeep the Pygmaei still held about 1,800 energy, 2,400
materials and 1,100 credits by day 240 without a fleet to spend them on, so after the first month the budget choice stops binding. The
choice binds early (the treasury on day 0 cannot cover the dock, the convoy office and an escort together) and whenever a flagship is
lost. Retuning yields or costs without people playing would be guesswork; it is the first economy question for a playtest.

## Prompt 16 result: war operations

**Implemented** (`Operations.cs`, `Operations.Battles.cs`, `GameRoot.Operations.cs`; decisions CA-12 to CA-19; rules patch vs-4):
ground forces as real roster units (garrisons and embarked troops, transport capacity); invasions launched from battle
preparation ("Invade ...") or from a system the fleet holds; the orbital supply defence (one defender win in up to two; early
stop, forced second chance, elected follow-up with provisional stakes; seizure on a 2-0); landing; the best-of-three planetary
series with lineups, reserves and map rotation; command succession by an acting commander and the concession policy;
withdrawal; terms in which the defender yields the world; one final settlement that alone transfers ownership; prisoners;
strategic time stopped for both powers during an operation; saves between engagements and during a battle. The operation window
shows the stakes, every remaining obligation for both sides, what has happened and the next deployment.

**Tests** (`OperationTests`, 11): 2-0 after an early stop; forced second defence with seizure, succession and a 2-1 series;
voluntary follow-up won (attacker supplies seized) and lost (protection kept); no deployable successor in orbit and on the ground;
withdrawal; accepted terms; save and load between engagements and mid-battle; round-limit ties going to the defender; the acting
commander in the rules; transport capacity; obligations for both sides.

**Runtime** (operations tour, [log](evidence/p16/tour_log.txt), 0 exceptions): the encounter at Dividend became an invasion from
battle preparation ([frame](evidence/p16/o02_prep_with_invade.jpg)); the operation window ([frame](evidence/p16/o03_operation_window.jpg));
two orbital engagements won by the attacker, the defender naming its Parallax Cruiser acting flagship after its flagship fell;
supplies seized; six units landed (two lost with transports); the first planetary match lost when the attacker's commander fell
with every landed unit; the attacker had no one left and conceded; the defender held 2-0, ownership unchanged
([frame](evidence/p16/o06_settled.jpg)). The journey tour below shows the other outcome: a conquest won 2-0.

**Defects found and fixed in runtime:** the operations tour script asked for a battle while the camera was still arriving, so nothing
started and it crashed on an empty list (tour bug; the game now also says why "Fight" did nothing in that moment); planetary
matches showed the Eclipse expedition's announcement (the shared battle-start routine always made it); the threat panel said
"ships" in ground battles.

**Known gaps:** Pygmaei and Inhumanus ground units have no models (placeholder hulls on the planetary boards); the rival never
launches operations; fleets arriving during an operation do not join it.

## Prompt 17 result: the strategic rival

**Implemented** (`RivalAI.cs`; CA-20 to CA-22): the bounded planner with its belief state, doctrine priorities, scored plans,
commitment, the developer log; proposals to the player (passage rights for credits, a status-quo truce) answered in the mandate
window; recorded truce violations; AI moves announced by their next system only.

**Tests** (`RivalAITests`, 9): defence of critical access when the player approaches; exploitation of an overextension (a raid on
Shoal Haven that cuts the player's supply and hold) and a different reaction to a guarded route; paid replacement and paid repair;
a commercial rival proposing rights, the player accepting, the truce honoured, the player's violation recorded and answered;
a status-quo truce as the negotiated inconclusive end; no hidden information read; no oscillation and reasons kept out of the
dispatch; automated campaigns with the planner on both sides, for each seat, ending without errors or softlocks.

**Observed in runtime:** the rival commissioned an eighth ship before the operations tour's invasion; the Pygmaei rival contested and
the Inhumanus rival preferred treaties, as their doctrines say. The pass also found a campaign blocker (see Prompt 20).

## Prompt 18 result: civilization progression

**Implemented** (`Doctrines.cs`, `GameRoot.Doctrine.cs`; CA-23; rules patch vs-5): two paths per faction, six projects each, with
prerequisites, prices, time, commitment and foreclosure, provisional faction lines, and effects through existing systems (routes,
forward dock refits, yields, build times, tolls and treaty weight, convoys, repair costs, operation reserves) and four disclosed
tactical sidegrades. The doctrine window; doctrines in force shown in battle preparation for both sides; the rival researches its
own path. No satisfaction or legitimacy system exists; broader politics are deferred.

**Tests** (`CampaignDoctrineTests`, 8): two plans for the same faction changing different decisions; commitment, delays, foreclosure,
prerequisites, cancellation at half, no duplicate research; overspending and arrears refused; sidegrade values, exclusivity and
faction checks; a furnace-doctrine shot previewed and resolved at +3 for 3 heat in a campaign battle; economy and operation effects;
the rival using doctrines through the same commands.

## Prompt 19 result: replay variants and the content pipeline

**Implemented** (`Variants.cs`, `Assets/RR/Content/slice_content.json`, `Tools/Content/rr_content.py`, `SliceContent.g.cs`,
`ShipManifest.cs`, `ContentAutomation.cs`; CA-24 to CA-26; rules patch vs-6): four authored starts chosen after the faction; the
data-driven module and loadout pipeline with validation at generation and at start-up; the extended-magazines sidegrade as data
only; the content fingerprint including modules; the read-only [ship manifest](manifest/ship_manifest.md); a content preview path.

**Tests** (`ReplayContentTests`, 6): every variant passes its validation for both factions and persists; the variants change routes,
knowledge, objectives and affordable forces; the blockade-and-treaty route exists only where the northern route does; every variant
reaches an encounter through ordinary commands; the pipeline adds a sidegrade with no rules code and changes the fingerprint; the
manifest matches the registry.

**Runtime** (content preview, [log](evidence/p19/tour_log.txt), 0 exceptions): all four modules fitted to both flagships in the hangar,
the new one shown with the heavy battery's mesh as its content says ([Pygmaei](evidence/p19/cp_pygmaei_extended_magazines.jpg),
[Inhumanus](evidence/p19/cp_inhumanus_extended_magazines.jpg)).

## Prompt 20 result: the private campaign alpha

**Journeys through ordinary controls** (`-rrAlphaTour <dir> -rrJourney <faction>`: the game's own menus, windows and commands;
battles flown by the slice AI for both seats; every step logged PASS, FAIL or NOTE):

| Run | Faction, plan | Steps | Outcome |
|---|---|---|---|
| [Inhumanus](evidence/p20_inhumanus/tour_log.txt) | Exchange convoys, toll brokerage, a refused offer, then passage rights by treaty | variant menu, priority, supply, hangar refit, doctrine, treaty refused then accepted, reloads | Victory by treaty on day 40, **no battle**: the negotiated route needs no conquest step |
| [Pygmaei 1](evidence/p20_pygmaei_run1/tour_log.txt) | Ore drive, foundry quotas, invasion of Dividend | orbital defence won 2-0 (supplies seized), first planetary match lost with every landed unit, operation failed | **Recovered from the setback** by a different plan: passage rights by treaty, Victory on day 50 |
| [Pygmaei 2](evidence/p20_pygmaei_run2/tour_log.txt) | Same plan, the conquest won | orbital 2-0, planetary series 2-0, Dividend I transferred at the final settlement | Inconclusive on day 240: found a **blocker** (see below) |
| [Pygmaei 3](evidence/p20_pygmaei_run3_stalled/tour_log.txt) | Same plan, after the occupation fix | orbital 2-0, first planetary match won, reload between matches; the second match **never started** | Operation stalled; found a second **blocker** (see below) |
| [Pygmaei, final build](evidence/p20_pygmaei_final/tour_log.txt) | Same plan, after both fixes | orbital 2-0 (supplies seized), match 1 won, reload, match 2 conceded by the defender (no commander left that could fight), world transferred at the final settlement; occupation of Umbra on day 100 | **Victory by arms on day 130** |
| [Inhumanus, conquest](evidence/p20_inhumanus_war/tour_log.txt) | Exchange convoys, underwritten repairs (the protection path), invasion of Ironmouth | uncontested orbit, landing, match 1 lost (**setback**), reload between matches, match 2 lost; the Pygmaei held Ironmouth 2-0; the rival then held the passage | Inconclusive on day 240 (a valid ending; the treaty after the failed invasion was refused) |

Every journey in the table ran with 0 exceptions. Two materially different plans with one faction: the Pygmaei's run 1 (a
treaty after a failed invasion) against the final run (conquest, then occupation), and the Inhumanus' treaty journey against
their conquest journey (different doctrine paths, priorities and endings); also `CampaignDoctrineTests.TheSameFaction_TwoPlans...`.
The journeys' battles are flown by the slice AI for both sides, so who wins is not a statement about balance for people. In the
Inhumanus conquest journey the first attempt to start match 1 came while the view was still arriving and did nothing (logged
"no battle"); the next attempt started it (a script timing, the game shows a notice in that case).

**Defect found and fixed: the occupation blocker** (CA-27). In Pygmaei run 2 the invasion succeeded and the defender's fleets were
gone, but access through Umbra could change hands only through a battle at Umbra, which could no longer happen; the war ended
inconclusive on day 240. Occupation now gives access to a power at war over the passage whose flagship-led fleet holds Umbra
unopposed at a settlement (`PassageWarTests.Occupation_...`); an unfiled run on that build reached Victory by arms, and so did
the final run (occupation on day 100).

**Defect found and fixed: an operation that could never continue.** In Pygmaei run 3 the defender's commander and every unit
it deployed fell in match 1; its supplies had been seized, so its reserves were cut off. Succession named an acting commander
from those cut-off reserves, the lineup then refused it ("could not form a legal army"), and the operation could neither
continue nor end; strategic time stayed stopped. A new probe (`SliceTests probe <save>`) reproduced it from the journey's
save. Succession, the lineup, the obligations and the terms now read one pool of units that may fight
(`Operations.GroundFor`); with nobody left in it the defender concedes the match, as the rules say. The stalled save itself
now continues (the probe shows the concession). Regression test: `OperationTests.CutOffReserves_NeverProvideTheSuccessor...`
(with a deployed survivor, and without one; each also after a reload).

**Persistence** (`AlphaPersistenceTests`, 4, plus the journeys' save-and-load checks at every boundary they reached, each comparing
a digest of the whole campaign and counting ships, units and history): pending production, priorities and doctrine complete once
whether or not the game is reloaded; interrupted access reloaded before the settlement counts the same; nothing can be applied
twice after an operation's final settlement; a whole campaign reloaded every five days ends identically to one never reloaded.
Mid-operation saves, between series matches and with an acting commander are covered by `OperationTests` (Prompt 16).

**Performance** (one machine: RTX 5060, i5-14400F, 32 GB, Windows 11; 1920x1080, Ultra, native render scale, uncapped; `-rrPerfTour`,
[run 1](evidence/p20_perf_run1/tour_log.txt), [after the fix](evidence/p20_perf/tour_log.txt); engine probe `SliceTests perf 240`):

| Scene | Median | p95 | Max |
|---|---|---|---|
| Galaxy, quiet (campaign start) | 2.9 ms | 3.5 ms | 6.0 ms |
| Galaxy, crowded (supply overlay, labels, campaign panel) | 2.8 ms | 3.3 ms | 4.9 ms |
| Treasury, mandate, doctrine windows open | 2.9 ms | 3.5 ms | 5.7 ms |
| Galaxy after a 200-day automated campaign; history window | 2.8 ms | 3.4 ms | 4.8 ms |
| Fleet travelling at speed 3 toward the Eclipse | 2.8 ms | 3.6 ms | 8.7 ms |
| Transition into an orbital battle | 2.2 ms | 4.6 ms | **534 ms** (was 3,492 ms) |
| Orbital battle, idle on the player's turn | 2.1 ms | 4.1 ms | 6.3 ms |
| Orbital battle, AI turns and animations | 2.1 ms | 3.1 ms | 8.7 ms (was 49 ms) |

Strategic day in the player (economy, supply, rival planner, doctrines, settlement) over 200 days: median 0.45 ms, p95 2.7 ms,
max 28 ms. The 200-day history (116 ledger entries, 93 dispatch lines) saves in 23 ms (188 KB). Outside the player (`SliceTests perf
240`): strategic day median 0.33 ms; save 25 ms, load 41 ms; the slice AI's battle decisions median 1.6 s, p95 8.1 s, max 17.9 s
over 55 decisions (off the frame in the game, but the player waits for them). The first run's medians were within 0.2 ms of these. Frame times on other hardware are unmeasured.

**Defect found and fixed: a 3.5 s freeze entering every battle.** The probe timed the Command Threat analysis at a battle's opening
position at 2,020 ms (3,191 orders checked) on the main thread; a second assessment ran after each animation. The analysis now runs
on a worker thread on a copy of the state (CA-28); the worst battle-entry frame fell to 534 ms, which remains a known issue. An
earlier attempt (baking the battle's backdrop planet in the background) did not change the freeze, added stutter on the galaxy (p95 97 ms) and was reverted.

**Package** (`python Tools/Package/rr_package.py`): writes `Builds/Release/RuleAndRuin_CampaignAlpha_<revision>.zip` (local, ignored by git) from the committed revision; a
build from modified sources is named `..._modified`. The script keeps the player's own `ScriptingAssemblies.json` and
`RuntimeInitializeOnLoads.json` (an earlier draft dropped every `.json`, which would have broken the player) and leaves out saves,
metrics, logs and Unity's debug-symbol folder. It contains the player, [PLAY_README](PLAY_README.md) (launch, reset,
controls), [RULES_SUMMARY](RULES_SUMMARY.md), [KNOWN_ISSUES](KNOWN_ISSUES.md), [PLAYTEST_KIT](PLAYTEST_KIT.md), the ship manifest
and `VERSION.json` (revision, rules version, content fingerprint). No licensed tools, font sources, credentials or local saves are
bundled; nothing was uploaded or published. Save compatibility: campaign saves load; battles saved under earlier rules are refused
(a deliberate rules-version boundary, as in the slice).

**Private test kit** ([PLAYTEST_KIT](PLAYTEST_KIT.md)): tasks, a non-leading interview guide, the observation sheet (time to first
purposeful action, unassisted launch, preview understanding, explanation of a defeat, supply decisions, recovery attempts, accepted
settlements, chosen progression, completion, voluntary replay or faction switch) and the finding classes. **No session has been run;
no tester has been contacted.**

**Human evidence: none.** Every result above is automated. Whether a player understands a loss, sees another plausible plan and
chooses to continue or replay is unobserved.

## Full suite

After Prompts 14 and 15: **266 passed, 0 failed** (146 vendored, 120 slice), 476 s.

After Prompt 20 (occupation and the succession fix included): **306 passed, 0 failed** (146 vendored, 160 slice), 1,097 s.
The run before the succession fix: 305 passed, 0 failed. The background threat analysis (CA-28) is in the game layer,
which these tests do not load; it was checked by the performance tour and the journeys.

## Known gaps after Prompt 15 (superseded by KNOWN_ISSUES.md)

* No person has played any of this. Pacing (the 60 to 90 minute target), readability, balance and fun are unmeasured.
* The rival is still the slice's simple actor (it answers a declaration and counter-attacks); Prompt 17 replaces it.
* Supply has no fog of war and no transport capacity; the strategic map shows every fleet.
* Treaty and supply values are provisional and untested against people.
