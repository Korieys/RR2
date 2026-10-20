# Command Glass UI kit

Reference: [the user-supplied Silex screenshot](../ArtSource/UI/CommandGlass/reference.png).

## Acceptance criteria

- Deep navy translucent dossiers, fleet dock, dispatch, dialogs, settings and battle panels. Small rounded corners and thin cyan edges; no crystalline relief or chamfered decoration.
- White/light-blue typography on dark surfaces. A pale card reserved for the contextual primary action, with dark typography and a blue CTA.
- Text-only tabs with an underline and small active pointer; separate hit areas and full captions.
- World overview follows the reference: Sovereign, Population, Development, Shipyard, Crystal Foundries, three production columns and unboxed orbital status. Context title is hidden during world inspection.
- Wider fleet cards place a ship portrait beside counts/status/location. The command heading sits outside its pale inner card.
- A shared semantic system applies to all gameplay views and dialogs. Faction accent comes from UIThemes; surface-light explicitly switches text and icon colors.
- Retain command insets of at least 16 logical units, 12-unit fleet-action gap, 8-unit tab gap, responsive layout, keyboard/pointer behavior and reduced-motion support.
- Verify in actual player captures at 720p/1080p and 100%/125%/140% UI scale. Exercise settings, planet tabs, fleet orders, navigation and a full deterministic battle/save-load loop.

## Implementation

CommandGlass.uss loads last after the faction sheet. Existing component and motion APIs are retained. Prismatic.cs now renders rounded glass geometry without relief textures. UiKit.ContentIcon resolves the local text token so the same component works on either surface. Strategy and Commandfall rules are unchanged.

## Validation

Completed on 19 October 2026. The 1080p review passed 939 assertions, the 720p review 937, the full campaign 674 additional assertions, and the final contrast/settings smoke 24; all had zero failures. Full battle replay and save/load retained deterministic state. The final build is `Builds/CommandGlass/RuleAndRuin.exe`.

See [the validation record](../Captures/CommandGlass/VALIDATION.md) for captures, animation recording, build provenance and limitations. [UI_KIT.md](UI_KIT.md) defines the shared components and expansion rules.
