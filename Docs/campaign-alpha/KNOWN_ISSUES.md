# Known issues (private campaign alpha)

None of these blocks a campaign from start to ending. The authoritative list is `Docs/campaign-alpha/STATE.md` at the packaged
revision; the slice's own list (`Docs/vertical-slice/KNOWN_ISSUES.md`) still applies.

**Not tested by people.** Every playthrough so far was automated (scripted steps, battles flown by the slice AI). Readability,
pacing (the 60 to 90 minute target), balance, difficulty and fun are unmeasured. Treat all of them as open questions.

**Balance and pacing (automation only).**
* Income outgrows costs after the first month; by day 240 a passive Pygmaei holds about 1,800 energy, 2,400 materials and 1,100
  credits. Budget choices bind early and after a lost flagship, not later.
* A defender that wins without damage and holds the centre with seven ships has beaten every rebuilt Pygmaei force in the
  automated runs on the round limit.
* After losing an orbital engagement, an attacker can be left with two frigates; the operation continues legally but is
  usually lost. That is the rule working, but it may feel abrupt.

**Performance (measured on one machine: RTX 5060, i5-14400F, 1080p Ultra, uncapped).**
* The slice AI's battle decisions take a median of about 1.6 s and up to about 18 s in the worst positions measured (55 decisions,
  .NET outside the player). They run off the frame, so the game stays responsive, but the player waits for the enemy's turn.
* The Command Threat panel fills in a moment after a battle opens (its analysis now runs off the frame; see CA-28).
* Entering a battle still has one long frame (534 ms worst measured, down from about 3.5 s); its cause is not yet measured.
* Frame times were measured only on this machine; lower-end hardware is untested.

**Art.** Pygmaei and Inhumanus ground units have no models; planetary matches show placeholder hulls. Ship and module models
are first-pass. The extended-magazines module reuses the heavy battery's mesh.

**Interface.**
* The windows are dense text: the mandate, treasury, operation and doctrine windows have not had a readability pass.
* The operation window shown after a final settlement can open faint over the arriving system view and is hard to read
  until the view settles ([frame](evidence/p20_pygmaei_final/a14_settled.jpg)).
* Pressing "Fight" in the operation window while the camera is still arriving shows a "view is still moving" notice.
* Supply labels on the galaxy can overlap the dispatch toast near the Pygmaei sector.

**Rules and campaign.**
* The rival never launches invasions itself; it defends, raids supply lines, contests the passage and negotiates.
* Fleets arriving at a system during another operation hold position; they do not join it.
* The strategic map has no fog of war: every fleet is visible to both sides (supply and the rival read only that).
* Transport capacity is a provisional rule (flagship two units, other ships one); troops beyond capacity after a loss are lost.

**Install.** Unzipped into a deeply nested folder (paths over 260 characters), the player closes at start-up without a message
("Unable to load mono library" in its log). Unzip to a short folder such as `C:\RR\`.

**Saves.** Battles saved under earlier rules (`vs-3` to `vs-5`) are refused by this build (`vs-6`), as designed; campaign saves
without a battle in progress load. Starting a new Passage War is recommended for testing.
