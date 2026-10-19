# Cold playtest protocol (Prompt 12)

For people who have never seen Rule and Ruin. The aim is to **observe** what they understand and do, not to ask whether they
enjoyed it. Automated tours and tests are technical verification only; they are never reported as human evidence.

**Status: no independent tester has played the slice yet.** Usability and balance claims stay unverified until sessions under
this protocol have been run and recorded (STATE).

## Setup (10 minutes before the session)

1. Use the packaged build (`RuleAndRuin.exe` in the release folder) on a 1080p display, windowed or full screen.
2. Start with a disposable save so the player's own slot is untouched:
   `RuleAndRuin.exe -rrSavePath C:\playtest\p01\save.json`. Local metrics are written next to it as `playtest_metrics.jsonl`
   (nothing leaves the machine). Delete the folder afterwards if the tester asks.
3. Have the controls card (README) on paper, face down. Hand it over only if the tester asks how to do something twice.
4. One observer, one note sheet per tester. Record the time at each step. Do not explain rules unless the tester is stuck for
   more than two minutes; note every explanation you give.

## Script

Say only this at the start: "This is a space strategy game in development. Please think aloud. I can't help much, but tell me
whatever you notice or find confusing."

| Step | Ask the tester to | Observe and record |
|---|---|---|
| 1 | Press F6 and start "Campaign: the passage" as either faction. | Which faction and why (in their words). Time to first click. |
| 2 | Find their flagship and look it over. | Do they find the Hangar? Do they read a module's tradeoff aloud? |
| 3 | "Change the ship in some way you think will help." | Which refit, and the reason they give. Did they notice the cost? Did they rename it? |
| 4 | "Go after the passage." | Do they find Declare and send the fleet? Confusion about days and time. |
| 5 | At battle preparation: "Decide what to do." | Do they read the stakes? Do they consider terms or withdrawal? |
| 6 | Play the battle without help. | Can they point to **their** leader and **the enemy** leader when asked mid-battle? Do they open a preview before a lethal or risky order? Do they use the Command Threat panel? Invalid-action attempts (metrics count them). |
| 7 | When it ends: "What decided that battle?" | Can they explain the decisive action in their own words, matching the result screen? |
| 8 | Back in the galaxy: "What changed?" | Do they notice access, standing, the wreck, history? Do they open History? |
| 9 | "Repair or replace what you need to, then pick your next move." | Repair, salvage, or replacement choices; any confusion about locations or cost. |
| 10 | (Separate, optional) The Eclipse expedition. | Do they scout first? Do they understand they cannot see every enemy ship? |

## Questions afterwards (open, in this order)

1. "Which ship was the leader on each side, and how did you know?"
2. "Tell me about a moment where the game showed you what an order would do before you gave it."
3. "Why did you win or lose?"
4. "How were the two fleets different?"
5. "If you played this battle again, what decision would you change?"
6. "Was there anything you wanted to do and could not?"

## What to record

* Timestamps for steps 1 to 9; total session length.
* Every moment of confusion: what they were trying to do, what they expected, what happened (quote them).
* Every explanation the observer gave.
* Whether they identified both leaders (yes / no / with help), understood a lethal preview (yes / no), explained their defeat or
  victory correctly (yes / partly / no), named a faction difference (which), made a purposeful refit (which and why).
* Their answer to question 5 verbatim.
* The `playtest_metrics.jsonl` lines for the session (time to first decision, battle duration, invalid actions, previews
  inspected, refits, result type).

Do not summarize as "they liked it". Report behaviour and quotes.
