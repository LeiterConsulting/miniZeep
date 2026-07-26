# miniZeep

miniZeep is a community-first broadcast and spectator toolkit for
[Zeepkist](https://store.steampowered.com/app/1440670/Zeepkist/). The current
plugin is named **ZeepCast**: an observer-side isometric race director for cups,
contests, community events, and streams.

The project is intentionally open-ended. Use the camera and telemetry core,
change the interface, build a different broadcast workflow, or take the ideas
in an entirely new direction.

> Have fun. If you build on the core mechanics, please credit miniZeep and
> Leiter Consulting somewhere visible. The legal requirements are those in the
> [MIT License](LICENSE).

## What works today

- Isometric whole-level overview and followed-racer views
- Deep cursor-focused zoom, orbit, keyboard pan, and mouse-drag pan
- Clickable racer roster and on-track racer markers
- Live speed, attempt time, finish state, status, and championship points
- Solid live racer cosmetics instead of the translucent remote ghost tint
- Automatic HUD behavior when entering menus or returning to racing
- Host and non-host observer support
- Local-only camera, interface, telemetry, and presentation behavior

ZeepCast reads Zeepkist's existing multiplayer state. The current code does not
send gameplay packets, custom chat commands, or lobby mutations.

## Quick start

### Players

1. Install BepInEx 5 for Zeepkist.
2. Download or build `ZeepCast.dll`.
3. Put the DLL in `Zeepkist/BepInEx/plugins/ZeepCast/`.
4. Start a level and press `F8`.

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
| `F8` | Enter or leave ZeepCast |
| `F9` | Hide or show the broadcast interface |
| `Tab` | Toggle overview and followed-racer views |
| `[` / `]` | Previous or next racer |
| `W A S D` | Pan relative to the camera |
| Middle mouse drag | Pan the map directly |
| Right mouse drag | Orbit and change pitch |
| Mouse wheel over map | Cursor-focused zoom |
| Mouse wheel over roster | Scroll the racer list |
| `Shift` + camera input | Fine adjustment |
| `Ctrl` + camera input | Fast adjustment |
| `R` | Reset framing, orbit, and zoom |

## Project guides

- [User Guide](docs/USER_GUIDE.md)
- [Development Setup](docs/DEVELOPMENT.md)
- [Architecture](docs/ARCHITECTURE.md)
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
