# Architecture

## Design goals

miniZeep should be:

- Useful to streamers and event hosts without changing race outcomes
- Safe for host and non-host observers
- Reversible when the director closes
- Understandable enough to become a foundation for other community mods
- Conservative about network authority and proprietary game content

## Runtime flow

1. `ZeepCastPlugin` registers configuration and creates one persistent
   `BroadcastDirector`.
2. The director waits for explicit activation in `GameScene`.
3. It uses Zeepkist's allowed local photo-mode transition and captures camera,
   cursor, spectator UI, renderer, material, and graphics-setting state.
4. It calculates level bounds from loaded block metadata.
5. It reads the existing network player list into `RacerSnapshot` objects.
6. `BroadcastHud` renders the roster, markers, header, and selected-racer card.
7. The director moves only the local spectator camera.
8. On exit or scene unload, captured state is restored.

## Components

### Plugin

`Plugin.cs` is the BepInEx entrypoint and configuration surface. Keep it thin.

### BroadcastDirector

`Core/BroadcastDirector.cs` owns:

- Activation and cleanup
- Overview/follow camera modes
- Camera input and framing
- Level bounds
- Racer discovery and ordering
- Current-attempt finish-state tracking
- Pause/photo-mode transitions
- Camera culling-distance adjustments

The director deliberately separates "director active" from "visualization
active." This permits F9 to show telemetry over normal gameplay while camera
input and world markers remain disabled outside the isometric view.

### RacerSnapshot

`Core/RacerSnapshot.cs` is the UI-facing view model. It references the current
network player and remote racer representation but contains no network logic.

### SolidRacerPresentation

`Rendering/SolidRacerPresentation.cs` reverses Zeepkist's translucent remote
ghost presentation for live racers. It caches every renderer, material, fader,
and relevant setting before changing it.

Remote multiplayer racers are represented by
`NetworkedZeepkistGhost.ghostModel`; there is not a second independent "live
racer" object to discover.

### BroadcastHud and UiFactory

`UI/BroadcastHud.cs` builds a resolution-independent uGUI interface in code.
`UI/UiFactory.cs` contains the small construction helpers and shared colors.

## Multiplayer and authority

Current features are local/read-only:

- Reading player and result state
- Moving the local spectator camera
- Drawing local UI
- Changing local materials and graphics settings

Do not make the entire director host-only. If a future feature changes lobby
state, scoring, player permissions, level rotation, or server-visible data,
guard that specific operation with `ZeepkistNetwork.IsMasterClient`, expose a
disabled UI state for non-host observers, and never retry a rejected command
every frame.

## Attempt state

Zeepkist's result/leaderboard entries can survive a racer reset. A persistent
result is therefore not proof that the current attempt is finished.

The director keeps a small per-racer state machine:

- A changed result latches a finish.
- A restarted active runtime clears that finish.
- Leaving players are removed from the state cache.

Preserve this distinction when adding sectors, split times, or automatic
camera selection.

## Rendering distance

The whole-track orthographic camera can sit far outside normal gameplay camera
distances. Zeepkist configures finite per-layer cull distances, so raising only
the far clip plane is insufficient. The director temporarily raises cull
distances for layers occupied by racer renderers and restores the original
camera values afterward.

## Performance expectations

- Cache scene objects at activation.
- Refresh network-derived UI state at a modest interval.
- Avoid scene-wide searches and material cloning every frame.
- Restore stale racer presentation entries when players leave.
- Profile large tracks and full lobbies before adding additional render passes.
