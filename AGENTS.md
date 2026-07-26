# Agent Environment Guide

This repository is public. Treat every tracked file and every commit message as
publishable to the Zeepkist community.

## Objective

Develop the `ZeepCast` plugin inside the `miniZeep` repository while preserving
multiplayer safety, reversible Unity state, and a clean public source tree.

## Repository boundaries

Allowed public content:

- Original miniZeep/ZeepCast source code
- Build metadata that references a user's local game installation
- Original documentation, tests, and scripts
- Small original media assets with clear licensing, after human review

Never add:

- Zeepkist, Unity, Steam, BepInEx, or mod binaries
- Decompiled game or third-party source
- `bin/`, `obj/`, logs, crash dumps, caches, or generated packages
- Local configuration, absolute user paths, credentials, tokens, or cookies
- Real Steam IDs, private chat, lobby codes, or unreviewed screenshots
- Material copied from unrelated private workspaces or other mods

If a task appears to require any prohibited material, stop and ask for a
public-safe alternative.

## Setup

Requirements:

- .NET SDK capable of targeting `netstandard2.0`
- A legitimate local Zeepkist installation
- BepInEx 5 installed into that game

The default Windows Steam location is configured in `Directory.Build.props`.
For another location, set `ZEEPKIST_INSTALL_DIR`, or create an ignored
`Directory.Build.props.user`:

```xml
<Project>
  <PropertyGroup>
    <ZEEPKIST_INSTALL_DIR>D:\Games\Steam\steamapps\common\Zeepkist</ZEEPKIST_INSTALL_DIR>
  </PropertyGroup>
</Project>
```

Build:

```powershell
dotnet build miniZeep.sln -c Release
```

Before handing work back:

```powershell
dotnet build miniZeep.sln -c Release
git diff --check
git status --short
```

## Architecture map

- `src/ZeepCast/Plugin.cs`: BepInEx entrypoint and configuration
- `src/ZeepCast/Core/BroadcastDirector.cs`: lifecycle, camera, racer state
- `src/ZeepCast/Core/RacerSnapshot.cs`: UI-facing racer data
- `src/ZeepCast/Rendering/SolidRacerPresentation.cs`: reversible racer visuals
- `src/ZeepCast/UI/BroadcastHud.cs`: broadcast UI and interaction
- `src/ZeepCast/UI/UiFactory.cs`: programmatic uGUI construction

Read `docs/ARCHITECTURE.md` before changing lifecycle or multiplayer behavior.

## Engineering rules

1. Current features are observer-side and local-only.
2. Do not make the entire mod host-only.
3. Guard only the specific future operations that mutate lobby state.
4. Never send chat commands as an implementation shortcut.
5. Capture and restore camera, cursor, renderer, material, and settings state.
6. Stop broadcast camera input outside the active visualization.
7. Avoid treating persistent leaderboard results as current-attempt state.
8. Keep host and non-host testing in the validation checklist.
9. Update the user guide and roadmap when behavior changes.

## Public-release check

Before staging, inspect every new file. Search for likely secrets and local
paths, verify that no binary is tracked, and stage explicit paths rather than
the whole surrounding workspace.
