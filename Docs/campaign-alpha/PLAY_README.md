# Rule and Ruin: private campaign alpha

A private, unfinished build for invited testers. Not for redistribution. Values are provisional and art is first-pass.

## Launch

Windows 10 or 11, 64-bit, a DirectX 12 graphics card. Unzip to a short folder (for example `C:\RR\`) and run
`game\RuleAndRuin.exe`. A deeply nested folder can push the game's files past Windows' 260-character path limit, and the game
then closes at once without a message.
To keep a session's save separate (recommended for every test session):

```
game\RuleAndRuin.exe -rrSavePath C:\Playtests\CA-01\save.json
```

The scenario menu opens on launch (F6 reopens it). Choose **The Passage War** as the Pygmaei or the Inhumanus, then a starting
situation. The mandate window states your goal and every condition; the passage panel on the right keeps the essentials in view.

## Controls

| Where | Control | Does |
|---|---|---|
| Galaxy | Left click a fleet, system or world | Select and inspect |
| Galaxy | Right click a system | Order the selected fleet there |
| Galaxy | Space, 1 / 2 / 3 | Pause or run time; time speed |
| Galaxy | Mouse wheel, `+` / `−`; double-click or C | Zoom toward the cursor; fly to the selection |
| Galaxy | Tab | Strategic overlay |
| Passage panel | Mandate, Treasury, Supply, Doctrine, Treaty, Operation, Invade, Embark troops | The campaign's windows and orders |
| Battle | Click a ship, then a gold hex or a red target | Move, then attack; every attack shows its exact result first |
| Battle | O, R, B, G | Overclock (Pygmaei), turn (capital ships), board, delegate to the AI |
| Battle | Offer terms (panel) | Propose a settlement at the start of your activation |
| Anywhere | F5 / F9 | Save / load (also during a battle) |
| Anywhere | F6 | Scenario menu |

## Reset

Delete the save file you chose with `-rrSavePath` (and `playtest_metrics.jsonl` beside it). Without the flag the game uses
`%USERPROFILE%\AppData\LocalLow\Rule and Ruin\Rule and Ruin Slice\saves\slot1.json`; delete that file to start clean.

## What is in this build

The Passage War: a bounded campaign on the existing galaxy with a beginning, recovery and an ending (military, treaty,
defeat or inconclusive); an economy with upkeep, local priorities and supply; invasions that join orbital engagements and
planetary Commandfall matches; a strategic rival that plays by the same rules and information; two doctrine paths per faction;
four starting situations. See `RULES_SUMMARY.md` and `KNOWN_ISSUES.md`.

Local metrics are written beside the save (`playtest_metrics.jsonl`); nothing is sent anywhere. `-rrNoMetrics` turns them off.
