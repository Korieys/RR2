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
| Prompts 16 to 20 | Not started at the time of this entry. |

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

## Full suite

After Prompts 14 and 15: **266 passed, 0 failed** (146 vendored, 120 slice), 476 s.

## Known gaps after Prompt 15

* No person has played any of this. Pacing (the 60 to 90 minute target), readability, balance and fun are unmeasured.
* The rival is still the slice's simple actor (it answers a declaration and counter-attacks); Prompt 17 replaces it.
* Supply has no fog of war and no transport capacity; the strategic map shows every fleet.
* Treaty and supply values are provisional and untested against people.
