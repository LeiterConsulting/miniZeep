# miniZeep

miniZeep is a community-first broadcast and spectator toolkit for
[Zeepkist](https://store.steampowered.com/app/1440670/Zeepkist/). The current
plugin is named **ZeepCast**: an observer-side isometric race director for cups,
contests, community events, and streams.

The 0.3 release line is a bounded, spectator-first broadcast tool rather than an
open-ended prototype. The code remains intentionally extensible: use the
camera and telemetry core, change the interface, or build another production
workflow on top of it.

> Have fun. If you build on the core mechanics, please credit miniZeep and
> Chris Leiter or 'L3it3R' somewhere visible. The legal requirements are those in the
> [MIT License](LICENSE).

## What works today

- Overview and live-field isometric shots plus isometric, chase, lead, and
  damped-dolly trackside racer-follow shots
- Shared follow framing: zoom, orbit/pitch, lateral offset, lead/lag, height,
  precision/fast adjustment, and one-key reset
- Deep cursor-focused zoom, orbit, keyboard pan, and mouse-drag pan
- Scrollable classification, clickable racer tiles, and optional world labels
- Live speed, attempt/result time, finish state, race status, field totals, and
  championship points
- Viewer-facing program graphics plus an independently hideable Race Control
  console for a clean feed
- A local occluded-racer highlight for the selected competitor
- Solid live racer cosmetics instead of the translucent remote ghost tint
- Exact spectator-camera handoff and automatic graphics behavior across pause,
  photo mode, normal racing, and scene changes
- Host and non-host observer support
- Local-only camera, interface, telemetry, and presentation behavior

ZeepCast reads Zeepkist's existing multiplayer state. The current code does not
send gameplay packets, custom chat commands, or lobby mutations.

## Quick start

### Players

1. Install BepInEx 5 for Zeepkist.
2. Download or build `ZeepCast.dll`.
3. Put the DLL in `Zeepkist/BepInEx/plugins/ZeepCast/`.
4. Start a level and press `F6`.

See the complete [User Guide](docs/USER_GUIDE.md) for controls, configuration,
and troubleshooting.

### Developers

1. Clone this repository.
2. Install Zeepkist and BepInEx 5 locally.
3. Set `ZEEPKIST_INSTALL_DIR` if the game is not in the default Windows Steam
   location.
4. Run:

   ```powershell
   dotnet build miniZeep.sln -c Release
   ```

The output is
`src/ZeepCast/bin/Release/netstandard2.0/ZeepCast.dll`.

The repository never includes Zeepkist, Unity, or BepInEx binaries. Builds
reference the copies from your own local installation. Read
[Development](docs/DEVELOPMENT.md) before making changes.

## Default controls

| Input | Action |
| --- | --- |
| `F6` | Enter or leave ZeepCast |
| `F9` | Hide or show the broadcast interface |
| Backquote (`` ` ``) | Hide or show Race Control and help for a clean feed |
| `V` | Cycle Overview, Field, and Follow shots |
| `1` / `2` / `3` / `4` | Isometric, Chase, Lead, or Trackside follow |
| `H` | Show or hide the complete operator control reference |
| `M` | Hide or show on-track racer labels |
| `[` / `]` | Previous or next racer |
| `W A S D` | Pan Overview/Field; lead/lag and lateral offset in Follow |
| `Q E` | Lower or raise the Follow camera |
| Middle mouse drag | Pan the map directly |
| Right mouse drag | Orbit and change pitch |
| Mouse wheel over map | Cursor-focused zoom |
| Mouse wheel over roster | Scroll the racer list |
| `Shift` + camera input | Fine adjustment |
| `Ctrl` + camera input | Fast adjustment |
| `R` | Reset framing, orbit, zoom, and Follow modifiers |

## Project guides

- [User Guide](docs/USER_GUIDE.md)
- [Development Setup](docs/DEVELOPMENT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Broadcast Release Contract](docs/BROADCAST_RELEASE.md)
- [Roadmap / To Be Built](docs/ROADMAP.md)
- [Future Ideas](docs/IDEAS.md)
- [Contributing](CONTRIBUTING.md)
- [Agent Environment](AGENTS.md)

## Contributing

Bug reports, camera experiments, UI improvements, accessibility work, and
entirely new uses of the core are welcome. Please read
[CONTRIBUTING.md](CONTRIBUTING.md).

## License and attribution

miniZeep is licensed under the [MIT License](LICENSE).

Original core mechanics and project direction: **Leiter Consulting**.

Zeepkist is a game by Steelpan Interactive. This community project is
unofficial and is not affiliated with or endorsed by Steelpan Interactive.
