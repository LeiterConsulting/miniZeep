# Future Ideas

These are intentionally broader than the roadmap. Some may become miniZeep
features; others may be better as separate mods built on the same core.

## The tiny roller-coaster broadcast

Lean into the original inspiration: a whole track presented like a miniature
theme-park ride, with racers moving through it as readable characters rather
than anonymous dots.

- Track-aware camera compositions
- Tilt-shift, depth, and color treatments
- Crowd or checkpoint activity indicators
- A "tabletop" presentation preset
- Gentle automatic orbit for pre-race introductions

## An actual race director

Build a system that recommends shots without taking control away from the
operator.

- Detect overtakes, crashes, recoveries, and close battles
- Rank moments by broadcast interest
- Preview the next suggested camera
- Let the operator accept, reject, or lock a racer
- Learn per-track camera preferences locally

## Commentator tools

- Racer biographies and pronunciation notes
- Previous results and personal-best context
- Split comparison and projected finish
- Talking-point queue
- One-click lower thirds
- Color-safe and dyslexia-friendly layouts

Personal information should be opt-in and stored locally or in clearly
consented event data.

## Open extension surface

- Read-only telemetry events for companion mods
- Theme packs without code forks
- Custom racer-card widgets
- Camera-provider plugins
- Track metadata contributed by the community
- Versioned schemas for layouts and event configuration

The safest first extension API is local and read-only.

## Accessibility

- Scalable type and high-contrast themes
- Color-blind-safe racer identification
- Reduced-motion camera transitions
- Keyboard-only operation
- Screen-reader-friendly companion output
- Simplified "producer mode" with a small control set

## Post-race storytelling

- Bookmark incidents during a live race
- Export a timeline for editors
- Reconstruct a lightweight track map from recorded positions
- Generate race summaries from structured events
- Compare lines through a sector without distributing ghost files

## Community experiments

The core camera, racer discovery, and UI lifecycle could support:

- Marshal or steward views
- Track testing dashboards
- Coaching overlays
- Photo-mode composition tools
- Spectator mini-games
- Alternate non-racing event formats

If an experiment becomes specialized, keep the core reusable and let the
specialized workflow live in its own project.
