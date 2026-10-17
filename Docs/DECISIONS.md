# Decisions, conflicts and provisional interpretations

Everything marked **provisional** is my interpretation pending the creator's approval.

## Repository and integration boundary
* **No galaxy/strategy implementation exists on this machine.** The brief describes a "current Codex implementation" that
  reads as a star chart over a space background. I searched the user profile, Documents, Desktop, D:, E:, Unity Hub,
  Codex session working directories and the Antigravity folder. I found Codex *prompts* for a Unity and a Godot strategy
  game (`Documents/Codex/2026-10-07/cab/outputs`), an empty Antigravity Unity shell, and Commandfall. Nothing playable
  at strategic scale. If that implementation lives on another computer, this slice is not a comparison against it.
* Per the brief's fallback, this is an **isolated Unity slice** in `E:\ClaudeCode\RR3`, the folder the session was opened in.
* **Commandfall is untouched.** Its engine-free `Core` and `AI` assemblies and its EditMode tests are vendored, unmodified, from
  commit `5a31053` (branch `claude/rule-ruin-commandfall-units-e88455`) into `Assets/RR/Vendor`. Orbital battles in the slice
  run on exactly those rules. Upstream changes must be re-vendored; nothing here forks the rules.
* Planetary Commandfall stays its own game: Settings has "Open planetary Commandfall", which launches the existing 0.15 build.
  A regression run of that build (`-cfRosterTour`) passed with 0 problems (Captures/commandfall_regression).
* Engine and pipeline: Unity 6000.6.5f1 with URP 17.6 (the same as Commandfall). No HDRP migration; nothing required it.

## Architecture
* `RR.Strategy` (engine-free): data model, scenario, rules, battle bridge, save format. Presentation reads it and submits
  commands; it never mutates strategic state directly.
* `RR.Game`: one composition root (`GameRoot`) owns scale, transitions, selection, the clock and command submission. There is one
  `CameraRig`, which owns the camera. The views are `GalaxyPresentation`, `SystemPresentation` and `BattlePresentation`, each
  rebuilt from state on demand.
* Coordinates: strategic data is in light years (`StarSystem.X/Y/Z`). `GalaxyScale` maps 10 ly to 1 unit. Local systems use
  their own scale (`SystemScale`: orbit unit 34, planet size 5, ship 0.85). The battle uses hex size 1 on a 2D rules plane.
  Distances stay well inside float precision, so no floating origin was needed.
* Stable ids (`aurelith`, `silex_first_crown`, ship uids, `battle_eng_N_dD`) are shared by every view, the outliner,
  battle preparation, results and saves.

## Strategic rules (provisional; no approved campaign ruleset was available)
* Time model: pausable real time advancing whole days (speeds 1, 2.5 and 5 days per second). It holds automatically during an
  engagement, a transition or a battle. Based on the creator's earlier prompts ("real time with adjustable speed and pause").
* Resources: energy, materials, credits and influence, as integers. Yields per planet per day.
* Management: commission a ship at a shipyard (tier I to III, so it stays legal under the Balanced unlock tier), or raise a
  Crystal Foundry (+4 materials per day). One project per world. Costs and build times are provisional numbers in
  `ShipCatalog`.
* Movement: lanes with travel days computed from map distance. Hostile fleets meet on arrival, or in transit when they cross.
* Engagement: a fleet without a flagship cannot form a Commandfall battle line and withdraws.
* Battle line: Commandfall's own army rules (budget 30, 8 ships, exactly one leader).
* Battle consequence: losses, captures and carried damage persist. The loser withdraws. An orbital victory over an enemy
  system grants **orbit and blockade only** (conquest stage 1 of 3: orbit, landing, ground); the planet's owner never changes
  in this slice. A result is applied exactly once, keyed by battle id.
* Battle setup: auto-deployment for both sides (manual deployment is out of scope). The arriving fleet activates first.
  The opponent is Commandfall's Tactical `RosterAI`. Its only randomness is in the Beginner level, which is not used.

## Conflicts and how they were resolved
| Topic | Sources | Resolution |
|---|---|---|
| Shattered Veil vs The Eclipse Sector | Faction map v1 (Shattered Veil, lower right edge) vs the brief (Eclipse Sector, central region) | **Resolved by faction map v2** (`Docs/Reference/faction_map_v2.webp`, 2026-10-16): the Eclipse Sector is the centre of the galaxy, with the eclipse (the galactic heart) north-east of its middle, and Meeples moved to the south-east where the Veil was. The creator confirmed v2 as the canonical layout. The earlier south-central placement is retired. |
| Barlow vs Borglow the Vicious | Lore sheet (Borglow) vs the brief and poster "Barlow the Vicious" | Barlow (the latest approved). Not otherwise used in the slice. |
| Craigon vs Cräigon spelling | Codex JSON (Cräigon) vs earlier docs (Craigon) | Display name Cräigon (codex). Stable id `craigon`. |
| Pygmaei colour | Faction map (purple) vs character art (red and black) | The territory uses the map palette. Pygmaei units are not in the slice. |
| Pirate accent | Brief: yellow accents | Pirate Reach uses the map's gold. |
| Codex combat values | JSON says "proposed, not canon" | Used as-is through the vendored engine; their draft status is preserved. |

## Provisional names
All system and planet names not printed on the approved map are provisional and flagged `ProvisionalName` in data (the inspector
says so): Aurelith and its worlds, Lithar, Spore Drift, Cinder Verge, Umbra and the rest, plus the systems added for map v2: Gloam, Nocturne, Penumbra, The Quiet, Vesper and The Hollow Star in the Eclipse, and Lantern, Accord and Bastion in Meeples. Positions follow nodes drawn on map v2. Meeple Core, Unity, Hope,
Commonweal, Hearth and the sector names are from the approved map. Fleet names ("First Crown Fleet", "Brood of the Gift")
are provisional.

## Art direction choices (provisional, pending approval)
* Ship shapes are **new art interpretations** of the reference sheets.
  * Silex: granite keels, stacked shard plates, crystal spires, blue crystal. The Throne Ark alone carries royal gold.
  * Cräigon: an arched, segmented carapace with glowing seams, a forward claw maw, side pincers, a neural crest, and violet
    bioluminescence.
* The Eclipse Sector palette: near-black violet dust, faint indigo veils, a cold pale-blue rim, and its own population of dim red
  and violet stars. No faction, lore event or gameplay penalty is assigned to it.
* Hero world Aurelith Prime: a Silex crystal world, with pale stone continents, crystal fields that glint, cyan-white city
  networks and banded cloud systems.

## Faction map v2 adoption (2026-10-16)
* Sector centres, radii and oval aspect for Meeples, the Eclipse and the Elven Front are traced from map v2 at its own pixel scale
  (1,000 ly = 175 px). Silex, Pygmaei, Pirate Reach, Inhumanus and Craigon are unchanged on the map and in data.
* Things the map draws that are not sectors, systems or lanes are stored as `MapFeature` data (presentation only): the galactic heart,
  the Craigon origin vortex, two disputed zones and three Craigon incursion routes. The incursion routes are traced roughly and are
  **provisional**.
* Routes between the west and the Meeples now cross the unexplored Eclipse (map v2 draws routes through it). The raid route
  (Spore Drift, Cinder Verge, Aurelith) is unchanged, so contact days are unchanged.
* Saves: the format stays version 1. Sector aspect and map features are optional fields; older saves load with their own
  (v1) geometry and no features.
* Faction emblems are provisional until the creator sends the artwork.
