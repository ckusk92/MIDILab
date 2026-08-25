# MIDILab Roadmap

This roadmap describes possible directions for MIDILab. It is intentionally flexible and does not promise release dates or guarantee that every idea will be implemented.

## Current focus

The current goal is to make MIDILab a fast, approachable drum-composition tool that can generate believable MIDI, expose performance details when needed, and hand clean MIDI off to a DAW or drum sampler.

Areas that are especially valuable now:

- Improve generated fills based on real-world use and feedback
- Continue refining groove realism and humanization
- Make editing faster without turning MIDILab into a full DAW
- Add automated tests as generator and project-file behavior grows
- Expand documentation for contributors

## Likely next-stage ideas

### Editing and workflow

- Copy/paste and rectangular grid selections
- Loop playback of selected bars or ranges
- Per-row Solo and Mute for preview
- Lock selected rows/bars and regenerate only unlocked material
- Additional batch editing for velocity and timing

### Generation

- Per-instrument complexity/activity controls
- More sophisticated fill styles and fill shaping
- Musical variation based on an existing groove rather than a completely new pattern
- Better phrase-level dynamics and groove consistency
- More specialized non-4/4 genre behavior

### MIDI interoperability

- Custom MIDI mapping profiles
- Additional built-in drum-plugin mappings
- MIDI import for editing, humanizing, and creating variations from existing drum parts

## Larger ideas

### Song Arrangement Mode

Build a full drum performance from connected sections such as:

`Intro -> Verse -> Chorus -> Verse -> Chorus -> Bridge -> Final Chorus -> Outro`

The goal would be musical continuity between sections, not merely concatenating unrelated generated patterns. Repeated verses and choruses could share recognizable ideas while becoming gradually more active or varied.

### Human Drummer Engine

Evolve Humanize from simple controlled randomness toward performance-aware behavior, including:

- Instrument-specific timing tendencies
- Phrase-level velocity shapes
- More intentional ghost-note behavior
- Natural fill crescendos
- Consistent groove feel across measures
- Physical playability and limb-awareness checks
- Genre-specific push/laid-back timing tendencies

### Higher-quality preview audio

Explore replacing the basic system MIDI preview with a sample-based preview engine that better demonstrates velocity layers, dynamics, and repeated-hit variation.

### Keyboard and broader MIDI instrument support

MIDILab may eventually expand beyond drum generation to support **keyboard, piano, synth, or other MIDI instrument parts**. This would be a larger architectural step because pitched instruments require concepts that drum grids do not, including note duration, pitch, chords, scales, voicing, sustain, and possibly a piano-roll style editor.

The intention would be to preserve MIDILab's current philosophy: generate a useful musical starting point, make it easy to edit, and export standard MIDI for use in a DAW.

## Community ideas

Ideas and contributions are welcome. If you want to propose a significant new direction, please open a Feature Request so the use case and scope can be discussed before implementation begins.
