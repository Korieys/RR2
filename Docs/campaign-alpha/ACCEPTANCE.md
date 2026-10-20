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

## Prompt 16: orbital-to-planetary operations

| Gate | Status | Evidence |
|---|---|---|
| Planetary 2-0 and 2-1 | Met (automated) | `OperationTests.EarlyStop_ThenA20Series...`, `ForcedSecondDefence..._AndA21Series` |
| Orbital early stop, forced second defence, voluntary follow-up (won and lost) | Met (automated) | `EarlyStop...`, `ForcedSecondDefence...`, `VoluntaryFollowUp...` |
| No deployable successor (orbit and ground), legal withdrawal | Met (automated, runtime) | `NoDeployableSuccessor...`, `AGarrisonWithout...`; operations tour: the attacker with no units left conceded |
| Accepted settlement | Met (automated) | `AcceptedTerms_TransferTheWorld...` |
| Save and load between engagements (and mid-battle) | Met (automated, runtime) | `SaveAndLoad_BetweenEngagements...`, `AfterTheOperationSettlement...`; journey tour reload between series rounds |
| Draws and exhausted rounds under a documented policy | Met | CA-15; `ExhaustedRounds_NeverDraw...` |
| Ownership only through the declared final outcome | Met (automated, runtime) | assertions after each match; the operations tour (defender held 2-0) and the journey |
| Both players can inspect every obligation and risk | Met (runtime frame) | the operation window ([frame](evidence/p16/o03_operation_window.jpg)); `BothSides_CanInspectEveryObligation` |
| Near five-minute ground battles | **Not measured with people**; automated matches took 64 s of AI play | operations tour log |

## Prompt 17: the strategic rival

| Gate | Status | Evidence |
|---|---|---|
| Defence of critical access | Met (automated) | `RivalAITests.ItDefendsCriticalAccess...` |
| Exploitation of a known overextension; different reactions to different plans | Met (automated) | `ItExploitsAKnownOverextension_AndReactsDifferently...` |
| Lawful recovery after loss | Met (automated) | `ItRecoversLawfully...` (paid replacement, paid repair at its yard) |
| Treaty-based resolution; honouring and violating terms | Met (automated) | `TreatyResolution...`, `AStatusQuoTruce...` |
| No hidden information | Met (automated) | `ItCannotReadHiddenInformation` (destination beyond the lane, treasury, stores, modules changed: same decision) |
| No spam, no oscillation, a legal terminating action | Met (automated) | one proposal per interval; `ItDoesNotOscillate...`; status-quo truce and mandate end |
| Seat swap preserves budgets, deployment, supply and targeting rules; automated runs check errors and softlocks | Met (automated) | `AutomatedCampaign_BothSidesPlanned...` for each seat; `AWholeCampaign_ReloadedEveryFiveDays...` |
| Fun or human difficulty | **Not established** | automated runs only |

## Prompt 18: civilization progression

| Gate | Status | Evidence |
|---|---|---|
| The same faction with two valid plans, different decisions and costs | Met (automated) | `CampaignDoctrineTests.TheSameFaction_TwoPlans...` |
| Research duplication, invalid prerequisites, free respec, incompatible modules, overspending caught | Met (automated) | `PathCommitment...`, `Overspending...`, `TacticalSidegrades...` |
| One choice changes routes or economy; another unlocks a tactical sidegrade | Met (automated) | forward logistics, foundry quotas; furnace doctrine in a campaign battle (`AnAdoptedSidegrade...`) |
| Satisfaction or legitimacy policy | **Deferred** | no such system exists; policy effects are economic (CA-23) |
| Winnable through more than one policy | Met in part (automated) | treaty and military victories with different paths in tests; not established with people |

## Prompt 19: replay variants and the content pipeline

| Gate | Status | Evidence |
|---|---|---|
| Three variants playable and persisted | Met (automated) | `ReplayContentTests.EveryVariant_PassesValidation...`, `EveryVariant_ReachesAnEncounter...` |
| Two starting plans produce different decisions | Met (automated) | `TwoStartingSituations_DifferentDecisions...`, `TheVariants_Change...` |
| A content author adds the sidegrade without rewriting simulation code | Met (automated, runtime) | `TheContentPipeline_AddsASidegrade...`; content preview tour |
| The exported ship manifest matches in-game data | Met (automated) | `TheShipManifest_MatchesTheRegistry`; [manifest](manifest/ship_manifest.md) |
| Human replay observations | **Pending** | none |

## Prompt 20: the private campaign alpha

| Gate | Status | Evidence |
|---|---|---|
| A complete military campaign | Met (automated) | [Pygmaei, final build](evidence/p20_pygmaei_final/tour_log.txt): invasion won 2-0 (orbital and planetary), occupation, Victory by arms on day 130; [Inhumanus conquest](evidence/p20_inhumanus_war/tour_log.txt): invasion lost 0-2, Inconclusive on day 240 |
| A complete negotiated campaign, with no conquest step | Met (automated) | [Inhumanus journey](evidence/p20_inhumanus/tour_log.txt): Victory by treaty on day 40, no battle |
| Recovery from defeat | Met (automated) | [Pygmaei run 1](evidence/p20_pygmaei_run1/tour_log.txt): the invasion failed, then passage rights by treaty and Victory; [Prompt 15 tour](evidence/p15/tour_log.txt): a lost battle, then a rebuilt force; `PassageWarTests.ALostBattle_IsNotTerminal_AndRecovery...` |
| Series and persistence invariants | Met (automated) | `OperationTests` (12, one added for the stalled operation), `AlphaPersistenceTests` (4), the journeys' save-and-load checks (digest, ships, units, history) |
| Two materially different plans with one faction | Met (automated) | Pygmaei run 1 and the final run (treaty after a failed invasion; conquest and occupation); Inhumanus treaty and conquest journeys; `CampaignDoctrineTests.TheSameFaction_TwoPlans_ChangeDifferentDecisions` |
| Performance on available hardware, whole session | Met (one machine) | [perf tour](evidence/p20_perf/tour_log.txt); STATE "Prompt 20 result"; one defect fixed (CA-28), the remaining 534 ms entry frame listed |
| Private build, manifest, launch/reset, controls, rules, known issues, backlog | Met | `Builds/Release` zip (local), [PLAY_README](PLAY_README.md), [RULES_SUMMARY](RULES_SUMMARY.md), [KNOWN_ISSUES](KNOWN_ISSUES.md), [BACKLOG](BACKLOG.md), [manifest](manifest/ship_manifest.md) |
| Test kit for 10 to 20 strategy players | Prepared | [PLAYTEST_KIT](PLAYTEST_KIT.md); no one recruited or contacted |
| Human observations (understanding a loss, another plan, voluntary replay) | **Pending** | none: no session has been run |
