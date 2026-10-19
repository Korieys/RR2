# Known issues (vertical slice, 2026-10-19)

None of these blocks the primary loop. The authoritative list is `Docs/vertical-slice/STATE.md` at the packaged revision.

**Not yet tested by people.** Usability, readability, balance and session length are unverified; every playthrough so far was an
automated pilot or a script. Treat difficulty and pacing as provisional.

**Art.** Ships, the escort token and modules are first-pass generated models pending approval. Classes within a faction look alike
apart from size. Modules are the same for both factions and sit outboard of the Inhumanus flagship's rings. A destroyed flagship
vanishes rather than breaking up, so the decisive-action camera shows the place, not the wreck.

**Battle.**
* There is no interactive deployment screen: deployment is curated (passage scenarios) or automatic (campaign battles).
* Under fog of war (the Eclipse expedition), a ship that fires reveals where it fired from, and an order into a hex an unseen ship
  occupies is refused. Sensor range is not shaded on the board; only unseen ships are hidden.
* When a battle ends on the round limit, the result's "Decisive action" is the last order given; the sentence after it says
  correctly that objectives and surviving strength decided it.
* An in-battle offer of terms is forgotten if the battle is saved and reloaded in the same round (one more offer becomes possible).
* Local metrics for a battle restart from a reload, so orders before the reload are not counted.

**Campaign.**
* The AI answers terms but never proposes them.
* Contracts (Inhumanus) cannot be chosen in campaign battle preparation; they are part of the army data.
* A rival fleet that reaches the passage unopposed waits there; it does not take access without a battle.
* Fleet voyages are long at the default speed (29 days from Forgefall to Umbra); use speed 3 or press Space to unpause.
* In the system view the inspector can keep showing the system you were looking at after your fleet has left it.

**Saves.** Before this build, `-rrSavePath` was ignored, and development tours overwrote the default save slot. Saves made with this
build honour it.
