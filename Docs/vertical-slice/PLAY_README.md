# Rule and Ruin: the Umbra passage (vertical slice)

A playable slice of Rule and Ruin, a space strategy game: two powers, the industrial **Pygmaei** and the corporate **Inhumanus**,
contest a trade passage on the edge of The Eclipse Sector. A development build for internal play, not a release. It is not for
public distribution.

Source revision: see `SOURCE_REVISION.txt` beside this file. Windows 10 or 11, 64-bit; a DirectX 12 GPU.

## Start

Run `RuleAndRuin.exe`. Press **F6** for the scenario menu:

* **Campaign: the passage** (as the Pygmaei or the Inhumanus): the full slice. Refit your flagship, declare war over the passage,
  fight or negotiate, and live with the consequences; an optional expedition waits at Gloam.
* **Battle as the Pygmaei raiders / the Inhumanus escort**: the passage battle on its own.
* **Two players at this machine**: the passage battle, each side taking turns at the same controls.
* **Training: three lessons**: the passage battle against an enemy that holds still, with lessons on the rules.

To keep your own save untouched (for a playtest, say), start with a separate save file:
`RuleAndRuin.exe -rrSavePath C:\somewhere\save.json`.

**Reset a scenario:** start it again from F6. A campaign in progress is only replaced when you save over it. To clear everything,
delete the save file (by default `%USERPROFILE%\AppData\LocalLow\Rule and Ruin\Rule and Ruin Slice\saves\slot1.json`).

## Controls

| | |
|---|---|
| Left click | select a ship, fleet, system or planet; in battle, give the order shown in the preview |
| Mouse wheel; + and −; right or middle drag; W A S D; Q E | zoom toward the cursor; zoom; turn the camera; pan; rotate |
| Home | recentre the camera |
| F5 / F9 | save / load (also during a battle, between actions) |
| F6 | scenario menu |
| Space; 1, 2, 3 | galaxy: pause, game speed. Battle: hold position (wait) |
| Esc | back; in battle, undo the last ability pick |
| Tab | galaxy overlays |
| **Battle** | |
| T | threat map: every hex the enemy could strike next activation |
| O | overclock (Pygmaei): +2 movement or +2 damage for +2 heat |
| R | turn order (capital ships: aim the main battery) |
| B | board the enemy flagship (only where capture is allowed, and only when it is disabled and crippled) |
| 3, Enter | use the selected ship's ability, confirm its targets |
| G | delegate this round to the AI (again to take command back) |
| F | animation speed |
| Double-click your ship, or C | fly the camera to the selected (or hovered) ship |

## The rules in brief

* **Generals don't roll dice.** Every result is fixed by the rules and shown before you commit: hover a hex with a ship selected.
* **The flagship is everything.** Destroying (or capturing) a side's flagship ends the battle at once, whatever else survives.
  The Command Threat panel lists every way the enemy's next activation could kill yours, and the opportunities you have now.
* **Protection is real or absent.** Cover, guards that intercept, firing arcs and range protect a ship; standing near friends does not.
* **Capital ships** cannot fire their main battery into the 120° arc astern; turning in place spends the move.
* **Pygmaei overclock** for more speed or damage, at the price of heat; at 4 heat a ship is overheated (−2 armour, no overclock).
* **Inhumanus contracts** hire up to two escorts cheaply; they arrive in round 3 at an entry point both sides can see.
* **The campaign:** winning the passage gives access (credits each day), not ownership. Terms can end a battle without
  destruction; a disabled, crippled flagship can be boarded and taken. Wrecks can be salvaged once. History remembers.
  A power that loses the passage in battle comes back for it about 20 days later; a truce from a settlement stops that.
* **Delegation** hands your orders to the same AI your opponent uses. It plays by the same rules with what your side can
  see, but its choices are its own, not guaranteed best play.

## Local playtest metrics

The game writes `playtest_metrics.jsonl` next to the save file: when you first made a meaningful decision, battle durations and
results, invalid actions, previews inspected, and refits. It never leaves the machine. Start with `-rrNoMetrics` to turn it off.

## Known issues

See `KNOWN_ISSUES.md` beside this file (the same list as the project's STATE document at this revision).
