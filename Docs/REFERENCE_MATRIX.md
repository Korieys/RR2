# Reference matrix

Observed properties come from the files actually opened (listed at the end). The interpretation column is mine.

| Aspect | Observed in references | Interpretation in the slice |
|---|---|---|
| Composition | The faction map: seven glowing sector clouds packed edge to edge, dashed route lines, labels inside regions, dark gaps between them. HUD mockups: map-first layouts with panels at the screen edges. | The galaxy keeps the map's regional layout (system positions sampled from it), drawn as a 3D nebular volume with dark gaps; panels sit at the edges, and the centre stays the universe. |
| Camera angle | Map: top-down. Ship and character art: low three-quarter heroic angles. | Galaxy: 28 to 82 degree pitch with orbit. System: about 22 degrees, with free orbit. Battle: 34 degrees with a planetary crescent. |
| Spatial depth | The map is flat. The ship art has deep atmospheric perspective (moons, mist, waterfalls). | Finite-depth layers: ambient stars with parallax, a ray-marched nebula with dust lanes, actionable markers, fleets in 3D. The skybox is distant and dim. |
| Planet treatment | Large banded moons and gas giants appear in the backgrounds of the ship art. | Baked cubemap surfaces, an atmosphere computed from each ray's true closest approach, cloud shadows, ring shadows both ways, night-side city networks. |
| UI density | Mockups: dense data tables, small type, charcoal panels, thin metal borders, gold highlights, faction accents. | Charcoal and near-black blue panels, a fine metal top border, gold section headings, ivory text, Cinzel headings and Barlow body (both OFL). Moderate density, with inspectors only on selection. |
| Lighting | Ship art: cold key light on white stone, internal crystal glow, rim lighting from mist. | One star per system lights everything. The terminator stays attached to the star. Crystal and bio emission, plus a faint rim for silhouettes. |
| Faction identity | Silex: pale granite, crystal spires, blue, gold or violet crystal variants, cathedral silhouettes. Cräigon: biomechanical crustacean-insectoid forms, segmented plates, claws, glowing seams, many colour variants. Pygmaei: red and black, industrial, gritty. Pirates: gold accents. | Silex and Cräigon ships modelled to those languages. Other factions appear as territory and labels only. |
| Motion | Not observable in stills. | Damped camera (0.09 s), 0.6 to 1.1 s scale transitions, ships ease into exact hexes, a slow swirl in the Eclipse dust, reduced-motion mode. |

## Files inspected
* `Rule and Ruin_ Faction Map.png`, `map.png` (approved map; Shattered Veil at the lower right).
* Silex ship, colossus and character sheets (Oct 14 and 15 ChatGPT images), Cräigon character and ship sheets, Pygmaei sheets,
  `Cathedral Leviathan Over the Alien Shore.png`, `Pygmaei Industrial War Machine Infographic.png`.
* HUD mockups: `Stellar Dominions_ Galactic Strategy Interface.png`, `A · Imperial Frame — Galaxy HUD (Silex)`,
  `B · Bridge Glass — Map-first HUD (Meeples)`, `Fleet Command (Space Pirates)`, `Logistics & Supply Lines (Inhumanus)`.
* `Rule & Ruin_ Crystal Thronefall.png`, `Commandfall_ Crystal Forge Battle.png`, `Barlow the Vicious_ Freedom Burns.png`,
  `Space Pirates_ Shoal Warbands Dossier.png`, the gothic R&R emblems.
* `Rule and Ruin_ Lore Sheet (1).docx`, `Rule_and_Ruin_Commandfall_Core_Six_Units.json` (codex 0.1),
  `Rule-and-Ruin-UI-Overhaul-Codex-Goal-Mode.md`, the Codex Unity and Godot goal prompts.
* Not available: Endless Space 2 and Stellaris gameplay were not inspected as video. The experiential targets come from the
  brief and general knowledge of those games; no assets, interfaces or terminology were copied.