# Development Setup

## Requirements

- Git
- A .NET SDK capable of building `netstandard2.0`
- A legitimate local Zeepkist installation
- BepInEx 5 installed into Zeepkist

The project compiles against assemblies from your own installation. Those
assemblies are proprietary dependencies and must never be committed.

## Configure game paths

The default Windows Steam path is:

`C:\Program Files (x86)\Steam\steamapps\common\Zeepkist`

For another location, set the `ZEEPKIST_INSTALL_DIR` environment variable:

```powershell
$env:ZEEPKIST_INSTALL_DIR = 'D:\SteamLibrary\steamapps\common\Zeepkist'
dotnet build miniZeep.sln -c Release
```

Alternatively, create `Directory.Build.props.user` in the repository root. It
is ignored by Git:

```xml
<Project>
  <PropertyGroup>
    <ZEEPKIST_INSTALL_DIR>D:\SteamLibrary\steamapps\common\Zeepkist</ZEEPKIST_INSTALL_DIR>
  </PropertyGroup>
</Project>
```

Advanced setups can set `ZEEPKIST_MANAGED_DIR` and `BEPINEX_CORE_DIR`
individually.

## Build

```powershell
dotnet restore miniZeep.sln
dotnet build miniZeep.sln -c Release
```

Output:

`src/ZeepCast/bin/Release/netstandard2.0/ZeepCast.dll`

## Install a development build

Close Zeepkist before replacing a loaded DLL, then copy the build output to:

`Zeepkist/BepInEx/plugins/ZeepCast/ZeepCast.dll`

Keep local deployment scripts and installation paths out of the repository.

## Project structure

```text
src/ZeepCast/
  Plugin.cs
  Core/
    BroadcastDirector.cs
    RacerSnapshot.cs
  Rendering/
    NativeSpectatorGraphics.cs
    SelectedRacerVisibility.cs
    SolidRacerPresentation.cs
  UI/
    BroadcastHud.cs
    UiFactory.cs
```

See [Architecture](ARCHITECTURE.md) for responsibilities and lifecycle.

## Manual validation

At minimum, test:

1. Activation and exit in a multiplayer level
2. Overview, live-field, and follow views
3. Deep zoom, cursor focus, pan, orbit, reset, roster scrolling, and Follow
   side/lead/height modifiers
4. Racer selection by tile, marker, and keyboard
5. Multiple attempts after a racer finishes
6. Direct F6 exit, repeated entry, pause menu, return to visualization, and
   Back to Race
7. Clean feed, racer-label toggle, F9 automatic behavior, and manual override
8. Host and non-host observer sessions
9. Player join/leave while the director is open
10. Restoration of cursor, vanilla HUD, one playable local kart, camera
    controller, stock spectator graphics, materials, and settings on exit

## Working with game behavior

It is reasonable to inspect public API metadata or locally decompile a game
assembly for interoperability research where legally permitted. Do not copy
decompiled source into this repository. Document only the minimal behavioral
finding required to explain original miniZeep code.

## Release hygiene

Before committing, run the repository gate:

```powershell
.\tools\verify-release.ps1
```

Review every new file. Do not use broad staging from a mixed external
workspace.
