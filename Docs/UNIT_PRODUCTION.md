# Silex and Craigon unit integration

The supplied model collection is integrated into the Unity project at `E:\ClaudeCode\RR3`. The original ZIP and high-resolution models are preserved. Work is confined to presentation, unit assets, and a native ground-combat exercise mode; the Commandfall rules and strategy simulation are unchanged.

## Play and inspect

Run `Builds/PolishedUnits/RuleAndRuin.exe`. See [the close-up polish and inspection guide](UNIT_POLISH.md) for the latest controls and animation pass. Ships use the new models in the campaign's fleet and orbital-combat views. Open **Settings → Controls & companion** for **Line infantry**, **Specialists**, or **War engines**. These use the real planetary rules in Unity and leave campaign progress intact. **Inspect units and animations** opens the gallery with rotation and six animation controls.

Ground exercises are standalone proving-ground battles, not a new campaign invasion system. The existing separate Commandfall companion remains available.

## Contents

- 49 optimized models: 20 infantry, 8 ground vehicles, and 21 ships. There are 48 canonical unit identities plus an alternate Monolith Battleship.
- Three mesh levels per model: approximately 32,000 triangles for infantry or 26,000 for ships/vehicles at the highest level, then 8,000 and 2,000. The combined highest-detail geometry is reduced by about 98.3% from the supplied collection.
- Color, normal, and occlusion/roughness/metallic maps at up to 2K. Neutral fallback maps are supplied where the source has no corresponding texture. Runtime maps use compression, mipmaps, and streaming.
- Editable Blender skeletons and six clips: Idle, Move, Attack, Ability, Hit, Death. The close-up pass uses 15-bone infantry rigs, articulated vehicle legs, and rigid spacecraft rigs; see UNIT_POLISH.md. Motion remains subordinate to legal combat positions.
- Faction-colored discharge, impact, shield, healing, synaptic disruption, capture, rally, destruction, landing, and ship-drive effects. Effects have bounded lifetimes and a concurrent-instance limit, with reduced-motion behavior.
- A custom URP material retains source textures and adds surface lighting, energy accents, damage and dissolution. Unit portraits are regenerated from the new models.

## Asset locations

`Assets/RR/Art/Units/<unit_id>/` contains the Unity FBX, maps, material, prefab, and statistics. `ArtSource/Units/<unit_id>/` contains the editable `.blend`, packed textures, and statistics. `ArtSource/Units/production_manifest.json` records source counts and source-file checksums. `original_identity_manifest.json` retains the mapping back to the provided GLBs. Unity prefabs use a wrapper that converts Blender's forward direction to Unity +Z; the animation component stays on its geometry child so imported bone paths remain valid. Engine sockets are aligned to trail behind the hull.

The original naming review still has two provisional assignments: **Crown Warden** and **Royal Castellan**. They are marked in the gallery and metadata. No confidently identified supplied models cover **Resonance Adept**, **Prism Herald**, **Faultline Breaker**, or **Facet Mason**; the integration does not invent replacements for those identities. The second Monolith model is retained as an alternate asset and does not override the primary roster entry.

The close-up pass adds authored timing, planted leg chains, rigid equipment and armor components, and smoothed joint weights. It preserves the supplied triangle topology and surface textures; it is not a replacement sculpt or retopology pass. These are not bespoke motion-capture or facial-animation rigs. Optimization uses mesh decimation with the original UVs, not new quad retopology or newly baked sculpt detail. No minimum-hardware performance certification is claimed.

## Update an edited Blender source

Keep the six action names and the `Body_LOD0/1/2`, `Rig`, and socket objects. From the project directory:

```powershell
& 'C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe' -b --factory-startup --python Tools/UnitProduction/export_blends.py -- --project 'E:\ClaudeCode\RR3' craigon_carapace_drone
& 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe' -batchmode -quit -projectPath 'E:\ClaudeCode\RR3' -executeMethod RR.EditorTools.UnitAssetBuild.PrepareAndBuild -rrBuildOut Builds/Units/RuleAndRuin.exe -logFile Captures/UnitProduction/rebuild.log
```

Omit the final unit ID to export all Blender sources. Texture files alongside the `.blend` are the runtime texture sources; update them when painting new textures. `UnitAssetBuild.Prepare` rebuilds materials/prefabs and verifies imported skeletal motion. It excludes the alternate Monolith from the canonical registry. Later normal project setup also preserves these prefabs.

For a fresh optimization from the renamed high-resolution package, run `Tools/UnitProduction/process_models.py` inside Blender with `--source <renamed-package-folder> --output <new-output-folder>`. The input folder must include `asset_manifest.json`. A source index can select individual models, and `--force` regenerates an existing result. Preserve the already-corrected production sources unless intentionally rebuilding.

## Validation

See `Captures/UnitProduction/import-validation.txt`, the final player review's `unit-validation.txt`, and the campaign review log. The automated review checks all six clips for actual bone motion, three detail levels, finite effect cleanup, legal ground-combat commands, and an unchanged campaign digest. Captures are from the actual Unity player. Recorded clips preserve real capture timestamps; PNG capture overhead means they are visual evidence, not frame-rate benchmarks.

The existing engine/slice suite passed 167 tests with no failures. The final art build passed 288 live animation checks across all 48 canonical prefabs, all three ground exercises, effect cleanup, and campaign-state preservation. The importer additionally checked the alternate ship, closed idle/movement loops, and forward-facing sockets on all 49 assets. Final evidence is in `Captures/UnitProduction/VerifiedReview/`.

The campaign regression completed a 103-command orbital battle, reproduced its authoritative result from replay, and restored the same state after save/load. This campaign run preceded the final facing, loop, lighting and particle presentation polish; the final art package then passed the full unit/ground review. No strategy or combat-rule code changed.

Unity emitted JobTempAlloc lifetime warnings during offscreen capture, also noted in the project's earlier motion review. The final automated checks reported zero failures and no effects left alive. This does not replace a long hardware-performance soak or human animation review.
