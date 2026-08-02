# Broadcast Release Contract

This document defines the finite target for the spectator-first ZeepCast
release. It is a completion contract, not an open-ended roadmap. Work outside
these slices belongs in `ROADMAP.md` after this release is accepted.

**Release status:** implemented for 0.2.0. The automated release gate and a
local end-to-end spectator smoke test pass. Multiplayer racer, join/leave, and
non-host behavior remain part of the community compatibility matrix because
they require a live multi-client session.

## Product outcome

ZeepCast turns one local Zeepkist client into a dependable race-director
station for streams, cups, and community events. The operator can frame the
whole course, the active field, or one racer while viewers receive legible,
truthful broadcast graphics. The feature set remains useful to hosts and
non-host observers and never changes race, lobby, chat, or scoring state.

## Slice 1 — product and safety contract

Success criteria:

- The release has a fixed six-slice definition and explicit non-goals.
- Program graphics and operator controls are treated as separate surfaces.
- Every game object, camera, renderer, material, cursor, and setting changed by
  ZeepCast has a documented restoration boundary.
- Player identity remains keyed by Steam ID; display names are presentation.

## Slice 2 — spectator projection

Success criteria:

- A single snapshot per racer supplies identity, classification position,
  speed, attempt/result time, checkpoints when available, points, and status.
- Classification distinguishes racing, finished, crashed, spectating, damaged,
  boost, fan, and brake without inventing track progress or time gaps.
- A previous result does not leave a racer permanently finished after a new
  attempt begins.
- Selection survives reordering and changes predictably to the next available
  racer after a disconnect; zero racers produces an explicit empty state.
- Session totals used by the UI come from the same snapshot collection.

## Slice 3 — broadcast camera workflow

Success criteria:

- `V` cycles three named shots: `OVERVIEW`, `FIELD`, and `FOLLOW`.
- Overview fits the loaded course; Field continuously fits the live racer
  group; Follow tracks the selected racer using Isometric, Chase, Lead, or
  repositioning Trackside composition selected directly with `1`–`4`.
- Selection keys and UI selection retain Steam-ID identity while order changes.
- Zoom is cursor-centred, deep enough for close work, and works in every shot.
- Pan, orbit, precision/fast modifiers, reset, and smoothing remain available.
- Camera state and Zeepkist's spectator controller state are restored on exit,
  scene unload, pause transitions, and plugin shutdown.

## Slice 4 — broadcast graphics

Success criteria:

- Program graphics consist of a session header, live classification, selected
  racer lower third, and optional world markers.
- The director console adds field totals, shot/target information, hotkeys, and
  interactive selection; it can be hidden independently for a clean feed.
- `F9` still controls the complete overlay. The separate director-console key
  does not hide program graphics.
- The hierarchy remains legible over bright and dark tracks using translucent
  surfaces with strong text contrast; primary information is never printed
  directly over gameplay.
- The roster is scrollable and exposes a visible scroll affordance.
- Layout profiles cover 1280×720, 16:10, 16:9, ultrawide, and 4K-scaled output
  without clipping the header, classification, lower third, or help strip.

## Slice 5 — racer visibility and lifecycle

Success criteria:

- Remote racers use their live cosmetic materials while the visualization is
  active, including at whole-track camera distances.
- The selected racer receives a local visibility treatment that remains useful
  behind course geometry and is removed immediately when selection or camera
  ownership changes.
- Pausing hides program and operator graphics by default. Returning to normal
  racing does not leave the overlay enabled unless the user explicitly presses
  `F9`.
- A joining/leaving racer, missing renderer, unsupported visibility shader, or
  unavailable spectator camera degrades locally without breaking the session.
- No host command, chat command, gameplay packet, or retry loop is introduced.

## Slice 6 — release acceptance

Success criteria:

- Release build completes with zero compiler warnings and errors.
- `git diff --check` passes and public-repository hygiene finds no committed
  proprietary binary, local build output, log, credential, lobby code, or real
  Steam ID.
- User, developer, architecture, roadmap, and changelog documents describe the
  shipped behavior and controls.
- Manual acceptance covers activation/exit, all shot families and follow
  styles, clean feed, roster scrolling, repeat attempts, pause/return, host and
  non-host use, join/leave, and exact local-state restoration.

## Explicit non-goals for this release

- Automatic director decisions, battle detection, or inferred race gaps
- Event scoring, penalties, flags, heat formats, or lobby mutation
- Replay recording, highlight editing, or cross-session telemetry storage
- A second window, browser source, OBS plugin, or network telemetry server
- Per-track authored camera presets or geometry transparency mutation
- Replacing Zeepkist's networking, chat, results, or gameplay systems

Those ideas remain valid future work, but none is required to declare this
release complete.
