# Vertical slice acceptance

Each gate is quoted in short form from the prompt pack. A gate passes only with evidence: tests actually run, runtime behaviour
actually observed, or both, as named in the evidence column. "Pending" means not yet attempted; "gap" means it could not be
verified and why.

## Prompt 0: contract and audit

| Gate item | Status | Evidence |
|---|---|---|
| Verified baseline | Pass | 167/167 tests in this worktree; a full `-rrTour` of the newest build with leader elimination, replay identity, exactly-once result and equal save digests ([STATE](STATE.md#verified-baseline), `baseline/`). |
| Implementation map | Pass | [STATE](STATE.md#implementation-map) |
| Conflict register | Pass | [DECISIONS](DECISIONS.md#conflict-register), 20 entries |
| Concrete next step for Prompt 1 | Pass | [STATE](STATE.md#prompt-1-plan-as-written-at-the-end-of-prompt-0) |
| Enlargement and dead-end requirements recorded | Pass | CR-03 (already tripled, do not repeat), CR-04 (validator exists, all authored maps pass) |

## Prompt 1: authoritative deterministic combat

| Gate item | Status | Evidence |
|---|---|---|
| Tests: movement, blocked paths, range and LOS, defences, activation limits, cooldowns, invalid commands, round changes | Pass | `OrbitalRulesMatrixTests` (7), `BattleSessionTests.Stale_Duplicate_Unauthorized_AndIllegalCommands...`; 183/183 |
| Replay of the same start and commands reproduces the same authoritative state | Pass | `AFullBattle_ReplaysFromItsRecord...`, `TheSameStartAndCommands_GiveTheSameBattle...` (hash after every command); tour: replay identical, hash at order 24 identical across two runs |
| Preview equals commit, including after save/load | Pass | `MidBattleSave_ReloadsTheSameBattle_AndPreviewsStillEqualResults`; tour: F5/F9 mid-battle, previews identical for 12 hexes ([log](evidence/p1/tour_log.txt)) |

## Prompt 2: battlefield and controls

| Gate item | Status | Evidence |
|---|---|---|
| Map validator passes | Pass | `TradePassage_PassesTheValidator...` (0 dead ends, 3 approaches, 2 cross-links each), vendored `EveryAuthoredMap...`, `DeadEnds_AtMostTwo...` |
| Relevant classes reach objectives | Pass, with a recorded rule finding | `EverySliceClass_CanReachEveryObjective...`; DIAG movers reach a given objective from a third of the deployment hexes (`DiagonalMovers_AreConfined...`) |
| A local battle completes with ordinary controls | Pass (automated through the interface entry points); not yet verified by a person | Passage tour: selection, modes, hover previews, `Submit`, result screen, rematch, restart |
| Camera and effects do not change legal actions | Pass by construction and test | Every order goes through `BattleSession.Submit`; the facing that matters is rules state (`ApplyFacings` follows it; the fire animation's turn is cosmetic); replay from the record is identical |
| Runtime views: central route, flank, threat preview | Pass | `evidence/p2-5/p02`, `p03`, `p05`, `p06` |

## Prompt 3: flagship protection

Fixtures, each without a tutorial exception: exposed flagship loses at once; insufficient damage fails; real cover blocks a shot;
a valid guard intercepts once; an exhausted guard fails; a legal flank defeats protection; simultaneous destruction resolves
consistently (rule C6). **Status: pass** (`FlagshipProtectionTests`, 8 tests; the flank fixture also shows the threat analysis finding
the move-then-strike line). The defeat screen shows the decisive action and the sequence (`evidence/p2-5/p07_result.jpg`). Existing partial coverage: `LeaderElimination_EndsTheBattle...`,
`DefendedLeader_ADeclaredGuardInterceptsTheLethalShot`, `GuardRules_AreSpecific...`, `ProximityAlone_IsNotProtection`.

## Prompt 4: two fleets

Both factions playable; tests for thermal boundaries, contract accounting, arrival restrictions, activation interactions, roster
legality; two materially different legal compositions per faction with tradeoffs; no strictly superior class. **Status: pass for
everything testable** (`DoctrineTests`, `SliceRosters_AreLegal...`); "no strictly superior class" rests on the authored counters and the
roles (each class loses something: armour, range, speed or hull), not on measured balance.

## Prompt 5: flagship encounter and AI

Deployment to result to rematch; at least two viable plans; a failed unsupported rush; a defensive response; AI mirror runs for
crashes and exploits (not called human playtesting); duration observations. **Status: pass, with honest limits**: the two viable plans
win against specific defensive mistakes (a forward flagship; a flagship in the east corner) and lose against the careful AI defender;
durations are automated-play observations only (STATE). **Checkpoint after this prompt:
evaluate the tactical hook before adding strategic depth.**

## Prompt 6: campaign loop

Galaxy to battle to galaxy for success and failure; save and load before commitment, during battle, and after results without
duplicated fleets, lost identities or changed outcomes. Status: pending.

## Prompt 7: capital ships

Rename, fit a sidegrade, fight, return with the right condition and history, repair, save and load, deploy again with a stable
id; illegal and unaffordable refits fail cleanly; destruction neither duplicates modules nor erases history. Status: pending.

## Prompt 8: negotiation and capture

Destruction, accepted settlement, rejected offer then combat, lawful capture; insufficient funds, invalid custody, stale offers,
repeated confirmation, save and load; each ending changes campaign state differently. Status: pending. **Checkpoint: encounter,
persistent ship and political consequences form one connected experience.**

## Prompt 9: consequences

Two outcomes from separate saves give different histories, markers, salvage and political consequences; both reload; every
visible change has a supporting event; exhausted salvage stays exhausted. Status: pending.

## Prompt 10: Eclipse expedition

Reconnaissance changes knowledge or planning, not a hidden percentage; success and withdrawal both return coherent outcomes;
reloads preserve the encounter; optional and never blocking the primary slice. Status: pending.

## Prompt 11: premium pass

Genuine before and after runtime evidence across the loop, a visual regression check, measured performance where available,
an honest asset-gap list. Status: pending.

## Prompt 12: validation and build

No known blocker in the primary loop; results and saves consistent; every claimed differentiator demonstrable; STATE updated with
implemented, verified, blocked and deferred items. Status: pending. **Checkpoint: assess the whole demonstration, not a feature
count.**
