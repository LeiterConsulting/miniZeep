# Contributing to miniZeep

Thank you for helping make Zeepkist events more fun to watch.

## Good contributions

- Reproducible bug fixes
- Camera and broadcast-control improvements
- Racer telemetry and run-state accuracy
- UI, accessibility, localization, and resolution support
- Performance improvements for large or complex tracks
- Documentation and newcomer setup improvements
- Experimental features that remain safe in multiplayer

Large ideas are welcome. Open an issue or discussion before a large rewrite so
people can coordinate rather than build competing foundations by accident.

## Development workflow

1. Fork and clone the repository.
2. Read [Development Setup](docs/DEVELOPMENT.md) and
   [Architecture](docs/ARCHITECTURE.md).
3. Create a focused branch.
4. Build and test in both overview and follow modes.
5. Test as a host and as a non-host observer when multiplayer behavior changes.
6. Open a pull request explaining the user impact and how you validated it.

## Pull request checklist

- The solution builds without warnings or errors.
- No Zeepkist, Unity, BepInEx, or third-party binaries are committed.
- No logs, local configuration, Steam IDs, credentials, or private screenshots
  are committed.
- Normal gameplay state is restored when ZeepCast closes.
- Non-host clients do not invoke host-only mutations or chat commands.
- New controls and settings are documented.
- The change is focused enough to review.

## Code style

- Keep nullable reference types enabled.
- Prefer local, reversible presentation changes over permanent game-state
  mutation.
- Capture any Unity state before changing it and restore it on exit.
- Avoid per-frame allocations and repeated scene-wide searches.
- Guard every future lobby-mutating operation with an explicit authority check.
- Log useful lifecycle events, not per-frame noise.

## Credit and licensing

By contributing, you agree that your contribution is licensed under the
project's [MIT License](LICENSE). Preserve the copyright and license notice in
copies or substantial portions.

Visible credit to miniZeep and Leiter Consulting is appreciated when publishing
a fork or a mod built on the core mechanics.

## Community conduct

Be constructive, assume good intent, and keep criticism about the work rather
than the person. Harassment, discrimination, or deliberate disruption is not
welcome.
