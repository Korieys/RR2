# Vendored rules patch log

`Assets/RR/Vendor` holds Commandfall 0.15.0 (commit `5a31053`). Decision VS-02 allows changes there when a prompt requires them.
Every change is listed here with its reason and the test that covers it, and `RulesVersion.SliceRevision`
(`Assets/RR/Strategy/BattleSession.cs`) is incremented with each rules-affecting change, so battles recorded under the old rules
are refused rather than replayed differently.

| Slice revision | Date | Files | Change | Reason | Tests |
|---|---|---|---|---|---|
| vs-1 | 2026-10-19 | none | Baseline: the vendored rules are unmodified. | Prompt 1 needed no rules change; the battle session, record and replay checks live in `RR.Strategy`. | All 146 vendored tests pass unchanged. |
| vs-2 | 2026-10-19 | `Roster/SliceRules.cs` (new) | All slice rules in one file: capital firing arcs and turn orders, Pygmaei overclock heat, Inhumanus fleet contracts, slice orders for the legal-action list. | Prompts 2 and 4 (VS-10 to VS-12). | `PassageBattlefieldTests`, `DoctrineTests` |
| vs-2 | 2026-10-19 | `Roster/RosterDefs.cs` | `RStatusType.Facing` (26) and `Heat` (27). | Facing and heat ride on roster statuses, so they are serialized, hashed, cloned and replayed with no format change. | as above |
| vs-2 | 2026-10-19 | `Commands/Command.cs` | Events `FacingChanged`, `HeatChanged`, `UnitArrived`, `ArrivalDelayed` (appended). | Presentation and narration read them; nothing decides from them. | as above |
| vs-2 | 2026-10-19 | `Roster/RosterRules.cs` | Move option 2 is a turn (validated and applied); move and attack option 1 is an overclock; capital ships face their last step; heat cools at the end of the ship's own activation; contract arrivals at the start of a round; army-entry tokens copied to units. | as above | as above |
| vs-2 | 2026-10-19 | `Roster/RosterCombat.cs` | The arc check in `ResolveBasic` (so previews, validation, the AI and the threat analysis all see it); an overclock adds +2 raw with a breakdown line; an overheated target loses 2 armour. | as above | as above |
| vs-2 | 2026-10-19 | `Roster/RosterMovement.cs` | `Reachable(s, u, extra)` for the overclocked move. | as above | as above |
| vs-2 | 2026-10-19 | `Rules/LegalActions.cs`, `Roster/RosterAbilities.cs` | Slice orders added to `EnumerateRoster`; an optional first-pick width for ability enumeration (default 12, unchanged). | The command-threat analysis must not miss a distant lethal area strike that the AI's narrow search skips (Prompt 3). | `SimultaneousLeaderDestruction...` asserts the analysis finds an area strike three hexes away |
| vs-2 | 2026-10-19 | `Roster/RosterArmy.cs`, `Rules/ArmyRules.cs` | Contract tokens validated and discounted; at most two; `ArmyReport.Contracts`. | VS-12 | `ContractAccounting...` |
| vs-2 | 2026-10-19 | `Rules/RulesEngine.Abilities.cs` | Deployment excludes contracted escorts; automatic deployment keeps their entry hexes clear. | Without it the auto-deployed flagship sat on the centre entry hex and blocked its own escort. | `ContractedEscorts...`, `ABlockedEntryHex...` |
| vs-2 | 2026-10-19 | `Board/MapDefinition.cs` | The `trade_passage` orbital battlefield, registered through the ordinary validator. | Prompt 2 (VS-14). | `TradePassage_PassesTheValidator...` and the vendored `EveryAuthoredMap...`, `DeadEnds_AtMostTwo...` |
| vs-3 | 2026-10-19 | `Roster/SliceRules.cs` | Capital-ship modules (`ModuleDef`, `ApplyModules`, `ModuleMove`; `module:` army tokens, flagship only); lawful capture by boarding (`policy:capture`, attack option 3, `ValidateBoarding`, `BoardingPreview`, `ApplyBoarding`); boarding orders in the legal-action list; token validation for module and policy tokens. | Prompts 7 and 8 (VS-21, VS-23). | `CapitalShipTests`, `NegotiationTests.LawfulCapture...` |
| vs-3 | 2026-10-19 | `Data/Enums.cs` | `VictoryReason.LeaderCaptured` (9), appended. | A captured flagship is its own ending in records, narration and the campaign. | `NegotiationTests.EachEnding...` |
| vs-3 | 2026-10-19 | `Roster/RosterArmy.cs`, `Roster/RosterMovement.cs` | `Recompute` applies fitted modules; the movement budget adds the module change, never below 1. | Modules must change the same stats every preview, the AI and the result read. | `CapitalShipTests.RenameRefitFight...` |
| vs-3 | 2026-10-19 | `Roster/RosterRules.cs` | Units created with tokens are recomputed (and start at the new maximum hull); `Validate` and `Apply` route attack option 3 to boarding. | as above | as above |
| vs-3 | 2026-10-19 | `Board/MapDefinition.cs` | `trade_passage_wrecked` (the passage after a flagship died there) and the expedition drifts `eclipse_drift_a`, `eclipse_drift_b`, all through the ordinary validator. | Prompts 9 and 10 (VS-24, VS-25). | `ConsequenceTests`, `ExpeditionTests`, vendored `EveryAuthoredMap...`, `DeadEnds_AtMostTwo...` |

Upstream port: nothing to port yet.
