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
| Concrete next step for Prompt 1 | Pass | [STATE](STATE.md#next-step-prompt-1) |
| Enlargement and dead-end requirements recorded | Pass | CR-03 (already tripled, do not repeat), CR-04 (validator exists, all authored maps pass) |

## Prompt 1: authoritative deterministic combat

| Gate item | Status | Evidence |
|---|---|---|
| Tests: movement, blocked paths, range and LOS, defences, activation limits, cooldowns, invalid commands, round changes | Pending | |
| Replay of the same start and commands reproduces the same authoritative state | Partly existing | Slice test `Battle_IsDeterministic...`; tour replay identity. Needs the orbital matrix. |
| Preview equals commit, including after save and load | Partly existing | `Preview_EqualsResolution_NoDiceAnywhere`; no mid-battle save yet (D-07). |

## Prompt 2: battlefield and controls

| Gate item | Status | Evidence |
|---|---|---|
| Map validator passes | Existing for current maps | `EveryAuthoredMap_PassesTopologyValidation_AsAuthored`, `DeadEnds_AtMostTwo_AndAllIntentional` |
| Relevant classes reach objectives | Pending | |
| A local battle completes with ordinary controls | Pending (automation exists; needs a human-control check) | |
| Camera and effects do not change legal actions | Pending | |
| Runtime views: central route, flank, threat preview | Pending | |

## Prompt 3: flagship protection

Fixtures, each without a tutorial exception: exposed flagship loses at once; insufficient damage fails; real cover blocks a shot;
a valid guard intercepts once; an exhausted guard fails; a legal flank defeats protection; simultaneous destruction resolves
consistently (rule C6). Status: pending. Existing partial coverage: `LeaderElimination_EndsTheBattle...`,
`DefendedLeader_ADeclaredGuardInterceptsTheLethalShot`, `GuardRules_AreSpecific...`, `ProximityAlone_IsNotProtection`.

## Prompt 4: two fleets

Both factions playable; tests for thermal boundaries, contract accounting, arrival restrictions, activation interactions, roster
legality; two materially different legal compositions per faction with tradeoffs; no strictly superior class. Status: pending.

## Prompt 5: flagship encounter and AI

Deployment to result to rematch; at least two viable plans; a failed unsupported rush; a defensive response; AI mirror runs for
crashes and exploits (not called human playtesting); duration observations. Status: pending. **Checkpoint after this prompt:
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
