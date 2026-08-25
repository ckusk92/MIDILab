# Changelog

Notable user-facing changes to MIDILab are documented here.

## Unreleased

- Repository prepared for public open-source contributions.
- Added CI, contribution guidance, issue/PR templates, security policy, roadmap, and MIT license.

## v1.2

### Added

- Undo and Redo buttons.
- `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Shift+Z` shortcuts.
- Pattern edit history for generation, reset, add/remove, drag moves, hit edits, kit changes, and fill generation.
- Fill Generator with target bar, 1 Beat / 2 Beats / Full Bar length, and intensity 1-10.
- Genre-aware snare/tom fill families with humanized velocity and timing.

## v1.1.1

### Added

- Drag-and-drop hit movement while preserving velocity and timing offset.
- Horizontal and cross-instrument moves.
- Occupied destinations are protected from accidental overwrite.

## v1.1

### Added

- Six accent themes: Indigo, Ocean, Emerald, Amber, Rose, and Slate.
- Velocity-based pad shading.
- Advanced Hit Editor for velocity and timing offset.
- `.midilab` Save/Open project format.

### Changed

- Reduced horizontal page padding and expanded usable editor width.

## v1.0

### Added

- Export profiles separated from musical drum roles.
- General MIDI export profile.
- Steven Slate Drums 5.5 Factory Map export profile.

## v0.9

### Added

- Customize Kit workflow.
- Optional additional drum/percussion rows.
- Expanded General MIDI percussion support.

## v0.8.3

### Fixed

- Replaced native WPF ComboBox chrome with a MIDILab-themed template for readable dark-mode selected values.

## v0.8.2

### Fixed

- Improved selected ComboBox text/background behavior in Dark Mode.

## v0.8.1

### Fixed

- Improved ComboBox dropdown text and selection colors in Dark Mode.

## v0.8

### Added

- Time signatures: 4/4, 3/4, 6/8, 5/4, and 7/8.
- Odd-meter groupings for 5/4 and 7/8.
- Meter-aware groove generation and MIDI time-signature metadata.
- Section Type generation shaping.
- Dark Mode.

### Changed

- Renamed the application to MIDILab.
- Added the initial indigo visual theme and UI refresh.
