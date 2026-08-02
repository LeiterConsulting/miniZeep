# Changelog

## 0.2.1 - Camera handoff and operator controls

- Fixed race chrome remaining suppressed after returning from ZeepCast
- Fixed ZeepCast-owned photo mode leaving a spectator actor at the race spawn
- Added motion-aware Chase, Lead, and Trackside follow shots on `2`, `3`, and
  `4`, with Isometric follow on `1`
- Added an `H` operator-control reference and reduced the persistent legend to
  essential controls
- Changed control configuration to ZeepSDK-compatible key entries so the mod
  settings page renders key editors without unsupported-type errors
- Kept explicit `F9` graphics available over normal racing without claiming
  program-view ownership from other HUD mods

## 0.2.0 - Spectator-first broadcast release

- Added Overview, Field, and Follow shot workflow with `V` cycling
- Added cursor-centred deep zoom, improved map pan/orbit, and shot reset
- Rebuilt the overlay as broadcast program graphics plus an independent Race
  Control console and clean-feed toggle
- Added live field totals, truthful racer status, explicit empty state,
  scrollable classification, responsive layouts, and optional racer labels
- Added reversible solid-racer presentation and a selected-racer occlusion pass
- Suppressed stock flying-camera graphics only while ZeepCast owns the program
  view and restored them on handoff
- Fixed repeat-attempt finish state, disconnect selection, non-host command
  spam, title markup, pause/return behavior, and normal-racing camera handoff
- Added a finite broadcast release contract and automated public-source hygiene
  gate

## 0.1.0 - Initial community source release

- Isometric whole-level and racer-follow camera modes
- Cursor-focused zoom, orbit, keyboard pan, and mouse-drag pan
- Live racer roster, world markers, telemetry, and current-attempt finish state
- Solid live-racer presentation with reversible material and settings changes
- Automatic HUD behavior across pause, visualization, and racing states
- Host and non-host observer support
- Public documentation, MIT licensing, and contributor guidance
