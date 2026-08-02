# Roadmap: To Be Built

This roadmap is a direction, not a promise or an ownership claim. Contributors
are encouraged to prototype, challenge priorities, and take responsibility for
features they care about.

## Stabilize the foundation

- Add repeatable tests around racer attempt/finish transitions
- Improve compatibility diagnostics for game updates
- Add configuration for zoom floor, zoom speed, pan speed, and orbit speed
- Add user-rebindable controls for map drag and framing reset
- Validate ultra-wide, 4K, low-resolution, and multi-monitor layouts
- Add localization-ready UI strings
- Add an optional performance/debug panel

## Broadcast essentials

- Localized transparency or dither cutaway when a followed racer is occluded
- Camera presets saved per track
- Sector cameras placed or inferred along a course
- Smooth manual and automatic transitions between cameras
- Battle detection for close racers and position changes
- Finish-line and final-sector camera logic
- Director hotkeys suitable for a Stream Deck

## Event and cup workflows

- Configurable racer cards and sponsor-safe overlays
- Cup standings separate from Zeepkist's current round display
- Heat, qualification, and elimination formats
- Race notes and commentator cues
- Manual penalties, flags, and incident markers with explicit host authority
- Import/export of event branding and layouts

Any server-visible scoring or lobby mutation must be optional, explicit, and
individually guarded for host authority. Non-host observers must not spam
rejected commands.

## Production output

- Second-window or second-display output
- OBS/browser-source-friendly telemetry bridge
- Safe overlay API for community themes
- Replay bookmarks and post-race highlight markers
- Camera motion suitable for clips and thumbnails

## Quality and release engineering

- Automated source-format and secret-scanning checks
- A public compatibility matrix
- Packaged GitHub releases without committing binaries to source control
- Issue templates for bugs, compatibility reports, and feature proposals
- Contributor-owned feature areas and maintainership documentation
