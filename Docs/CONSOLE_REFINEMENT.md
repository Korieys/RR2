# Prismatic command console refinement

The settings interface now uses the same faction palette, crystal relief, cut-corner surfaces and animation system as the gameplay HUD. It replaces the earlier default controls and dense shortcut paragraph.

- Preferences group display comfort and audio with live percentage values and explicit ON/OFF indicators. Changes use the existing Settings persistence and Audio APIs.
- Controls & companion presents a scan-friendly shortcut reference and player-facing Commandfall availability, without exposing an installation path.
- Header, page navigation and Done remain fixed while content scrolls. The panel is bounded to 90% of the viewport and supports enlarged interface scales.
- Native slider and toggle controls retain keyboard interaction. Visual focus styling uses faction tokens.
- Modals now own keyboard input: Tab, Home, F5 and F9 cannot trigger map or save commands behind them. Escape dismisses non-engagement modals; pending engagements retain their existing explicit decision flow.

Implementation: UIRoot.cs (console and preference controls), Widgets.cs (font roles), Prismatic.uss (console layout and styling), GameRoot.Input.cs (modal keyboard boundary), Automation.cs (focused regression coverage).

Validation artifacts are in Captures/ConsoleRefinement/final. The runtime smoke checks exercise preference callbacks, persisted volume reload, page navigation via UI submit events, footer visibility at 100% and 140%, notification lifecycle and unchanged authoritative game-state digest. A disposable save is used. The deterministic suite passed 167 tests (146 Commandfall, 21 slice). The normal campaign save hash remained unchanged.

This pass does not change combat rules, navigation rules, planet materials or ship assets. The full campaign tour was not repeated for this focused console refinement. This is engineering and visual inspection, not a human usability study.
