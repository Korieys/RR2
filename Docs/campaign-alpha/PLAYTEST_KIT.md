# Campaign alpha private test kit

For the creator to run sessions with **10 to 20 relevant strategy players** once the basic controls pass. This is a recruitment
target for the creator, not evidence that anyone took part. Nothing here contacts anyone; recruitment is the creator's decision.
It extends the slice's [PLAYTEST_PROTOCOL](../vertical-slice/PLAYTEST_PROTOCOL.md) (keep using its consent wording).

## Before a session

1. Build under test: record the revision (`git rev-parse --short HEAD`), the rules version and content fingerprint shown in
   `Docs/campaign-alpha/manifest/ship_manifest.md`, and the package name. Results from different builds are kept apart.
2. A fresh, disposable save for each session:
   `RuleAndRuin.exe -rrSavePath C:\Playtests\<session-id>\save.json`. Local metrics go to `playtest_metrics.jsonl` beside it
   (no network; `-rrNoMetrics` turns them off).
3. Session id: a short local code (for example `CA-07`). **No names, emails, ages or other personal details** in any file.
4. Consent, read aloud: what is recorded (the local metrics file, the observer's notes, and only if the tester agrees separately,
   a screen recording); that they can stop at any time without giving a reason; that the build is unfinished and private.
   Record "consent: yes/no, recording: yes/no" on the sheet. Without a yes to recording, do not record.

## Tasks (read as written; do not demonstrate)

1. "Start the game and begin The Passage War as either faction." (F6 if the menu is not open.)
2. "Tell me what you are trying to achieve." (Observe: do they find the mandate panel? Can they say the goal in their words?)
3. "Get your fleet ready for a fight." (Observe: hangar, refit, rename, treasury.)
4. "Play until something important happens." Then: "What happened, and why?"
5. In the first battle, before an attack the tester chooses: "What will this attack do?" (Observe: do they read the preview,
   the lethal-line panel, the 'battle ends' line?)
6. After a loss of any kind: "What would you do differently?" (Do they name a concrete change: route, supply, refit, doctrine,
   negotiation?)
7. Free play, up to the session limit (90 minutes suggested). At the end: "Would you start another campaign? With which side?"

Allow a tester to stop. Do not rescue: write down every hint you had to give, word for word.

## Observation sheet (one per session)

| Measure | How | Notes |
|---|---|---|
| Time to first purposeful action | Launch to the first declared objective, treaty, priority, refit or move order (also in the metrics file) | |
| Unassisted launch | Started the scenario without help? | |
| Understood the immediate goal | Their words for task 2 | |
| Read a lethal preview correctly | Task 5: predicted result matched the preview | |
| Explained a defeat | Task 6: cause named | |
| Supply decisions | Opened the supply overlay or treasury unprompted? Acted on a cut? | |
| Recovery attempt | After a setback, what did they do? | |
| Settlements and treaties | Proposed, accepted or refused, and why | |
| Progression choice | Which doctrine, and the reason given | |
| Campaign completion | Outcome, day, real minutes | |
| Voluntary replay or faction switch | Asked to play again? Which side? | |
| Hints given | Verbatim | |

## Short interview (after play; neutral wording)

1. "Describe what you were doing in the last ten minutes."
2. "Was there a moment you did not know what to do? What did you do then?"
3. "Tell me about a decision you made that you would change."
4. "What, if anything, would you want to see next time?"

Do not ask whether they liked the named systems (supply, doctrines, operations, generals don't roll dice). Do not explain
features before or during play. A positive opinion alone is not evidence of replay value; voluntary replay is.

## Classifying findings

Each finding gets exactly one class and keeps its session id and build:
**implementation defect** (the game did something its rules or text say it should not), **readability problem** (it did the
right thing but the tester could not tell), **balance concern** (a choice is dominant, useless or unfair in play),
**pacing problem** (waiting, travel, repetition, or a session that cannot end in the time), **untested hypothesis** (an
expectation nobody has exercised yet). Fix the top blockers first and repeat the affected tasks on the new build. Small samples
do not justify market claims.

## Raw data

Keep the sheets, the metrics files and any recordings in a private folder per build. Summaries cite session ids. Delete
recordings when the creator decides they are no longer needed.
