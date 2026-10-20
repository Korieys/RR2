# Campaign alpha acceptance

Each gate from the pack, what satisfies it and the evidence. "Automated" means tests or scripted tours; human validation is
listed separately and is **pending** throughout unless real observations are recorded.

## Prompt 13

Skipped at the creator's request. No independent session, no tester kit; human validation pending.

## Prompt 14: a bounded campaign

| Gate | Status | Evidence |
|---|---|---|
| The scenario begins and ends through a military route | Met (automated) | `PassageWarTests.MilitaryRoute_WinAndHold...` |
| ... and through a negotiated route, using implemented rules | Met (automated) | `TreatyRoute_WithoutAnyBattle...`, `ATreaty_AfterAVictory...` |
| A nonterminal battle loss and coherent recovery | Met (automated, and runtime) | `ALostBattle_IsNotTerminal...`; tour [p14](evidence/p14/tour_log.txt) |
| Reloads cannot reset holds, duplicate rewards or change eligibility | Met (automated) | `Reloads_CannotResetHolds...` (one run reloaded every day ends identically) |
| Defeat only from explicit conditions | Met (automated) | `Defeat_ComesOnlyFromDeclaredConditions...` |
| Inconclusive or negotiated end defined | Met | Inconclusive at day 240 (`TheMandateExpires...`); treaty victory |
| Exact requirements and interruptions displayed | Met (runtime frame) | [mandate](evidence/p14/w01_mandate.jpg) |
| Later dependencies marked incomplete | Met | Mandate line on planetary operations (until Prompt 16) |
| 60 to 90 minute first run | **Not measured** | One automated run took 100 strategic days and 532 s of automated time; not a human session |

## Prompt 15: economy and supply

| Gate | Status | Evidence |
|---|---|---|
| Working route, announced disruption, treaty access, blocked route, rerouting, exhausted stores, recovery | Met (automated) | `EconomySupplyTests` (10) |
| Supply loss changes a preparation or campaign decision | Met (automated) | A cut route stops the hold counting (`ADisruption...`); a cut dock is not restocked (`TheForwardDock...`); convoy income stops (`ConvoyIncome...`) |
| Two affordable plans with different trade-offs | Met (automated) | `TwoAffordablePlans...` |
| Lawful recovery after a setback | Met (automated) | arrears recovery, dock restock after the blockade lifts, rerouting |
| Objectives consume real supply state | Met | `PassageWar.HoldBlocker` reads `Supply.Of` |
| Insolvency explained, never negative, no free purchases | Met (automated) | `Upkeep_And_Insolvency...`; the treasury preview states the consequence |
| Idempotent settlement and production | Met (automated) | `Settlements_AreIdempotent...`, double start refused |
| AI obeys the same accounting | Met (automated) | `TheRival_PaysTheSameUpkeep...` |
| Overlay leaks nothing hidden | Met by construction | The strategic map has no hidden information in this project (CA-08) |
