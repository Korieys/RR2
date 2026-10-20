# Fleet Operations UI kit

The current interface follows the user's Command Glass reference: deep navy dossiers and fleet cards, thin cyan edges, text-only tabs, an open scene, and a pale inner card reserved for the contextual action. Silex retains light blue, white and navy. The corner identifies the player's faction, never the game title. See [COMMAND_GLASS.md](COMMAND_GLASS.md) for the reference acceptance criteria and validation.

## Source of truth

| Responsibility | Source |
| --- | --- |
| Base layout and semantic components | `Assets/Resources/RR/RR_Style.uss` |
| Current surface roles, visual tokens and responsive composition | `Assets/Resources/RR/CommandGlass.uss` |
| Shared rounded geometry and interruptible motion | `Assets/RR/Game/UI/Prismatic.cs` |
| Silex identity tokens | `Assets/Resources/RR/UIThemes/silex.uss` |
| Theme installation and reusable primitives | `Assets/RR/Game/UI/UiKit.cs` |
| Strategic dossiers, navigation, fleet cards and order dock | `Assets/RR/Game/UI/StrategyHud.cs` |
| Battle HUD | `Assets/RR/Game/UI/BattleHud.cs` |
| Settings, battle preparation and results | `Assets/RR/Game/UI/UIRoot.cs` |
| Ship portrait generation | `Assets/RR/Editor/UiKitBuild.cs` |

## Faction themes

`UiKit.Install` loads the base Prismatic layout, then `RR/UIThemes/<player polity ID>`, then CommandGlass.uss. The final sheet defines dark/light semantic surfaces while retaining the faction accent and paper tokens. Selection of an enemy object never changes the player's chrome.

To add a faction, provide `<polity ID>.uss` beside `silex.uss` and its crest at `Resources/RR/Emblems/<polity ID>.png`. Override `--accent` for the player's identity; retain distinct resource, health, warning and hostile colors. Do not duplicate the HUD for a faction.

| Role | Component/token |
| --- | --- |
| Dark glass | `--navy`, `--navy-raised`; light `--ivory` and muted `--muted` text |
| Contextual action | `.command-card.surface-light`; explicit dark text and icon tokens |
| Faction emphasis | `--accent`; underlines, active borders, primary buttons |
| Current selection | full thin border on fleet cards; underline and pointer on tabs |
| Context-sensitive glyph | `UiKit.ContentIcon`, resolving local `--ivory` |
| Faction glyph | `UiKit.AccentIcon`, resolving local `--accent` |

Generated surfaces resolve local `--surface-*` aliases; icon elements resolve `--content-color` or `--icon-accent`. These bindings let custom UI Toolkit geometry use the same USS tokens as native text. New components must use semantic roles instead of copying literal foreground colors.

## Components and information rules

- **Identity:** faction crest, faction name, directory entry point. The top bar also holds resources with daily income, clock state and utility controls.
- **Dossier:** object name and context, fixed tabs, one scroll region, and a fixed return/directory control. Planet tabs separate Overview, Shipyard and Development. Fleet/system tabs retain access to systems and contacts.
- **Section:** numbered label, thin rule and consistent spacing, created by `UiKit.Section`.
- **Data row:** label and right-aligned value, wrapping long values. Use `UiKit.Data`; retain actual units and availability explanations.
- **Fleet card:** real flagship portrait, ship count, activity and location/arrival. Activating it selects and focuses that fleet through the existing navigation path.
- **Order panel:** selected object/project, consequence, cost/time and primary action. An active project replaces the commission button with progress and cancellation, preventing duplicate orders. Cancellation explains the half-cost refund. Unavailable actions remain visible with a reason.
- **Shipyard option:** real hull portrait, role, tier, battle cost, hull/armor, active ability and construction cost. Selecting it changes the order panel; it does not spend resources.
- **Dispatch:** latest event near the upper right, with a complete log available on demand. Pending contact has a direct review action.
- **Battle:** dark glass roster and detail panels, navy status and action chrome, shared buttons/health bars and scrollable details. Combat rules and legal cell colors are unchanged.

## Typography, spacing and interaction

Use the bundled Barlow family. Display titles use Barlow SemiBold; body and numeric labels remain readable sans serif. Dossier titles are 34 logical pixels (26 compact); body 14; supporting text 12; eyebrows 10. Avoid adding paragraph-sized all-caps text or decorative serif labels to gameplay chrome.

Use 4/8/12/16/24 spacing and one-pixel dividers. Primary commands are 46 logical pixels tall (40 compact). Keep at least 16 units of inset inside the pale action card, 12 units between fleet actions and 8 units between dossier tabs. Hover, pressed, focus, selected and disabled states are defined centrally. Tabs and selectable cards use real UI Toolkit Buttons so navigation submit and pointer activation share callbacks. Reduced Motion removes button transitions as well as retaining the existing game motion preference.

At 1920 x 1080 and default interface size, the dossier is 420 logical pixels wide and the dock has a 238-pixel minimum height. Below 1550 logical pixels, the compact composition uses a 310-pixel dossier and a 224-pixel minimum dock, smaller portraits and closer resource spacing. UI size remains controlled by the existing Settings slider. Scroll regions absorb additional lists. The command dock sizes to its action content so controls cannot cross the frame; preserve the open world view when adding new content.

Keep the central viewport free of picking surfaces. Declare decorative overlays `PickingMode.Ignore`; retain the existing pointer-over-UI guard for panels and controls. New UI must not issue camera or fleet commands through a panel click.

## Cinematic finish

The shared lighting and motion finish is documented in [CINEMATIC_UI.md](CINEMATIC_UI.md). `CinematicUi.cs` supplies the surface catchlights, fine rims, portrait backing and dialog vignette; `Prismatic.cs` supplies the existing faction palette and finite motion channel. New screens inherit these effects through the existing component roles. Keep effects inside component bounds and ship images above their decorative backing. Honor Reduced Motion and avoid permanent animation loops.

## Art pipeline

Portraits under `Assets/Resources/RR/UI/Ships` are transparent renders of the project's existing ship models and materials, not concept art or external game assets. `RR > UI Kit > Render ship portraits` regenerates them. The editor renderer restores temporary shader globals and cleans up the preview scene. `RR.EditorTools.UiKitBuild.PrepareAndBuild` renders portraits and then builds; ordinary `RR.EditorTools.BuildTools.BuildWindows` reuses committed portraits. Unknown hulls receive a vector fleet glyph.

## Expansion guardrails

All authoritative data comes from `GalaxyState` or Commandfall `MatchState`. UI actions call existing `GameRoot`, `StrategyRules` or `BattlePresentation` entry points. Do not calculate alternative production legality or combat outcomes inside widgets. Do not alter authoritative IDs, resources, build times, fleet limits, replay commands or serialization for a visual feature.

Use the existing tab, data, section, cost, portrait, progress, button and scroll components for new screens. Add semantic roles to the shared sheet before adding one-off inline colors. Preview every new long label and unavailable state at 1.0 and 1.4 interface size. Verify return navigation, pointer blocking, and focus as well as screenshots.

## Verification entry points

- Existing mechanics suite: `Tools/SliceTests`; 146 Commandfall tests and 21 slice tests.
- `-rrReview <capture directory>`: live galaxy/system/world/fleet captures, bounds checks, three interface sizes and authoritative-state digest protection.
- `-rrUiKitOrders` with `-rrReview` and an explicit disposable `-rrSavePath`: submits actual UI Toolkit tab and production-button events, checks exact costs and refund behavior, and captures an active project.
- `-rrTour <capture directory> -rrTourPlan intercept`: full campaign, battle, replay and save integration tour.

Use a disposable save path for every automated run. Generated concepts are design references; only runtime captures count as implementation evidence. Automated checks establish correctness and layout bounds, not player preference or long-term usability.
