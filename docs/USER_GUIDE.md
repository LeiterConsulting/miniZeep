# User Guide

## What ZeepCast is

ZeepCast turns Zeepkist's local spectator camera into an isometric event view.
It is intended for streamers, commentators, cup organizers, camera operators,
and anyone who wants to watch a multiplayer track as a tiny moving world.

It works for the lobby host and for non-host observers when the lobby's normal
photo-mode rules allow spectating.

## Installation

1. Install BepInEx 5 for Zeepkist.
2. Create `BepInEx/plugins/ZeepCast/` inside the Zeepkist installation.
3. Put `ZeepCast.dll` in that folder.
4. Start Zeepkist.

The first launch creates a BepInEx configuration file for the plugin.

## Starting and leaving

While a level is running:

- Press `F6` to enter ZeepCast.
- Press `F6` again to release the director camera.
- Press `Escape` to open Zeepkist's menu. The broadcast HUD hides by default.
- Choose Return to Racing to leave the visualization and keep the HUD hidden.
- Press `F9` if you intentionally want the broadcast HUD over normal gameplay.

If the lobby disables photo mode for the local player, ZeepCast reports that it
cannot activate instead of trying to bypass the lobby setting.

## Camera controls

| Input | Action |
| --- | --- |
| `V` | Cycle Overview, Field, and Follow shots |
| `[` / `]` | Select the previous or next racer |
| Click racer tile or marker | Select and follow that racer |
| `W A S D` | Pan relative to camera heading |
| Middle mouse drag | Drag the map |
| Right mouse drag | Orbit and change pitch |
| Mouse wheel | Zoom toward the cursor |
| `Shift` | Make the active camera input more precise |
| `Ctrl` | Make the active camera input faster |
| `R` | Reset zoom, orbit, and pan |

Overview fits the course. Field follows the bounds of racers still competing.
Follow tracks the selected racer; when no racer is available, the camera skips
that shot instead of showing a misleading empty follow view.

The mouse wheel scrolls the roster instead of zooming when the pointer is over
the racer list.

## Interface

- The left roster shows live racers, position, speed, attempt time, checkpoints,
  and status.
- Clicking a racer changes to follow mode.
- On-track labels identify racers in the overview.
- The bottom card contains expanded information for the selected racer.
- Backquote (`` ` ``) hides Race Control and the help strip while retaining
  viewer-facing program graphics.
- `M` controls racer labels independently.
- `F9` hides or restores the entire broadcast interface.

ZeepCast suppresses Zeepkist's stock flying-camera labels only while it owns
the isometric program view and restores their prior state on release.

A previous leaderboard result does not permanently mark a racer as finished.
Finish state resets when that racer begins another attempt.

## Configuration

Open the generated BepInEx configuration file for ZeepCast to change:

- Director, interface, camera-mode, and racer-selection keys
- Broadcast title
- Starting pitch and yaw
- Follow view size
- Overview padding
- Live-field padding
- Camera smoothing
- Automatic photo-mode entry
- Solid live-racer presentation
- Selected-racer occlusion highlighting
- Whether Race Control is visible on activation

## Troubleshooting

### ZeepCast says photo mode is unavailable

The lobby or level currently prevents the local player from entering photo
mode. ZeepCast follows that rule. Try again when spectating becomes available.

### The mod does not open

Confirm that:

- The DLL is inside a folder under `BepInEx/plugins/`
- BepInEx loads when Zeepkist starts
- A level is running
- The configured director key is not shared with another mod

### Racer models disappear

Check that solid racer presentation is enabled. Report the track, player count,
graphics settings, and whether the issue affects overview, follow, or both.

### A command or host-authority message appears

The current release does not send chat commands or mutate lobby state. Report
the exact message, the installed mod list, and redacted surrounding log lines.
Do not post lobby codes, chat history, or Steam IDs.
