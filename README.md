# MIDILab

MIDILab is an open-source Windows drum MIDI generator and editor built with C# and WPF. It is designed to create musically useful, humanized drum parts that can be edited visually and exported to a DAW such as REAPER.

MIDILab focuses on generating and editing the **performance data** in a drum part: note placement, velocity, timing, fills, meter, and kit mapping. The final drum tone comes from the instrument or sampler that plays the exported MIDI.

## Current release

**v1.2** adds Undo/Redo and a dedicated Fill Generator.

For downloadable builds, use the repository's **Releases** page rather than downloading build output from the source tree.

> MIDILab is currently a Windows application. Early community builds may be unsigned, so Windows can display a security warning. Only download builds from the official repository Releases page.

## Features

- Rule-based drum groove generation for **Rock, Indie, Folk, and Metal**
- Section-aware generation for **General, Intro, Verse, Pre-Chorus, Chorus, Bridge, Breakdown, and Outro**
- Time signatures: **4/4, 3/4, 6/8, 5/4, and 7/8**
- Meter groupings for 5/4 and 7/8
- Adjustable **BPM, Energy, and Humanize** controls
- Velocity and microtiming humanization
- Velocity-aware pad shading so ghost notes and accents are visible
- Right-click hit editor for exact **velocity and timing offset**
- Drag-and-drop hits while preserving velocity and timing data
- Undo/Redo with keyboard shortcuts
- Fill Generator with target bar, fill length, and intensity controls
- Customizable drum kit rows
- Six accent themes plus Light/Dark Mode
- Save/Open editable `.midilab` project files
- MIDI preview through the Windows MIDI output system
- MIDI export for **General MIDI** and **Steven Slate Drums 5.5 Factory Map**

## Typical workflow

1. Choose meter, genre, section, energy, humanization, and kit options.
2. Generate a groove or variation.
3. Edit hits directly in the grid.
4. Drag notes to new positions without losing velocity or timing.
5. Right-click a hit for exact performance editing.
6. Generate or regenerate fills where needed.
7. Save the editable work as a `.midilab` project.
8. Export a `.mid` file and load it into a DAW/drum instrument.

## Controls

| Action | Input |
| --- | --- |
| Add/remove a hit | Left-click a pad |
| Move a hit | Drag an active pad to an empty pad |
| Edit velocity/timing | Right-click an active pad |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` or `Ctrl+Shift+Z` |

A drag is a true move. The hit keeps its exact velocity and timing offset. Dropping onto an occupied pad is blocked so another note is not overwritten accidentally.

## Fill Generator

The Fill Generator replaces only the selected ending region of a target bar while leaving the rest of the groove untouched.

You can choose:

- **Target bar**
- **1 Beat, 2 Beats, or Full Bar**
- **Intensity from 1 to 10**

Fill generation is genre-aware and can create snare builds, mixed snare/tom movement, descending tom runs, kick accents, and occasional cymbal color at higher intensities. It also respects the currently enabled kit pieces and the existing Humanize setting.

## Projects vs. MIDI exports

`.midilab` and `.mid` serve different purposes.

- **`.midilab`** stores editable MIDILab state, including notes, velocity, timing, generator settings, active kit pieces, export target, theme, and reset baseline.
- **`.mid`** is the portable musical output intended for REAPER or another DAW.

## Drum sounds and export mappings

MIDI does not contain recorded drum sounds. MIDILab exports note and performance information, then your DAW or drum plugin supplies the sound.

Current export profiles:

- **General MIDI**
- **Steven Slate Drums 5.5 - Factory Map**

Selecting the SSD5.5 profile changes MIDI note mapping on export. It does not make the built-in Windows preview use SSD5.5 sounds.

## Requirements

- Windows
- .NET 10 SDK
- Visual Studio with the **.NET desktop development** workload, or the .NET CLI

## Build from source

Clone the repository, then from the repository root run:

```powershell
dotnet restore MIDILab.slnx
dotnet build MIDILab.slnx --configuration Release
```

To run from the project directory:

```powershell
cd MIDILab
dotnet run
```

You can also open `MIDILab.slnx` in Visual Studio and press **F5**.

## Repository documentation

- [Contributing](CONTRIBUTING.md)
- [Roadmap](ROADMAP.md)
- [Changelog](CHANGELOG.md)
- [Generation engine](docs/GENERATION.md)
- [Security policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Contributing

Contributions are welcome. Bug fixes, UI improvements, new generator behavior, MIDI mappings, documentation, and feature ideas are all useful.

For small changes, feel free to open a pull request. For larger behavior or architecture changes, opening a feature request first is encouraged so the direction can be discussed before significant work is done.

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup and pull-request guidance.

## Roadmap

MIDILab is still evolving. Possible future directions include better musical variation, song arrangement tools, MIDI import, expanded humanization, additional mapping profiles, improved preview audio, and potentially **keyboard/MIDI instrument support beyond drums**.

The roadmap is directional rather than a promise of specific release dates. See [ROADMAP.md](ROADMAP.md).

## License

MIDILab is available under the [MIT License](LICENSE).
