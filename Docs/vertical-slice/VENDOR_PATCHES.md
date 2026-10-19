# Vendored rules patch log

`Assets/RR/Vendor` holds Commandfall 0.15.0 (commit `5a31053`). Decision VS-02 allows changes there when a prompt requires them.
Every change is listed here with its reason and the test that covers it, and `RulesVersion.SliceRevision`
(`Assets/RR/Strategy/BattleSession.cs`) is incremented with each rules-affecting change, so battles recorded under the old rules
are refused rather than replayed differently.

| Slice revision | Date | Files | Change | Reason | Tests |
|---|---|---|---|---|---|
| vs-1 | 2026-10-19 | none | Baseline: the vendored rules are unmodified. | Prompt 1 needed no rules change; the battle session, record and replay checks live in `RR.Strategy`. | All 146 vendored tests pass unchanged. |

Upstream port: nothing to port yet.
