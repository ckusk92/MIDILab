using System.Collections.Generic;
using MIDILab.Models;

namespace MIDILab.Services;

public static class DrumOutputProfiles
{
    public static readonly DrumOutputProfile GeneralMidi = Create(
        DrumOutputProfileId.GeneralMidi,
        "General MIDI",
        "GM",
        new Dictionary<DrumInstrument, byte>
        {
            [DrumInstrument.Kick] = 36,
            [DrumInstrument.Rimshot] = 37,
            [DrumInstrument.Snare] = 38,
            [DrumInstrument.HandClap] = 39,
            [DrumInstrument.LowTom] = 41,
            [DrumInstrument.ClosedHiHat] = 42,
            [DrumInstrument.FloorTom] = 43,
            [DrumInstrument.PedalHiHat] = 44,
            [DrumInstrument.MidTom] = 45,
            [DrumInstrument.OpenHiHat] = 46,
            [DrumInstrument.HighTom] = 48,
            [DrumInstrument.Crash] = 49,
            [DrumInstrument.Ride] = 51,
            [DrumInstrument.China] = 52,
            [DrumInstrument.Tambourine] = 54,
            [DrumInstrument.Splash] = 55,
            [DrumInstrument.Cowbell] = 56,
            [DrumInstrument.Crash2] = 57
        });

    public static readonly DrumOutputProfile StevenSlateDrums55Factory = Create(
        DrumOutputProfileId.StevenSlateDrums55Factory,
        "Steven Slate Drums 5.5 - Factory Map",
        "SSD5.5",
        new Dictionary<DrumInstrument, byte>
        {
            // SSD5.5 factory articulation map. MIDILab chooses one practical
            // articulation when SSD exposes several variants of the same piece.
            [DrumInstrument.Kick] = 36,          // Kick Center
            [DrumInstrument.Snare] = 38,        // Snare Center
            [DrumInstrument.Rimshot] = 40,      // Snare Rimshot
            [DrumInstrument.LowTom] = 41,       // Floor Tom 2 Center
            [DrumInstrument.ClosedHiHat] = 42,  // Hi-Hat Tip Closed
            [DrumInstrument.FloorTom] = 43,     // Floor Tom 1 Center
            [DrumInstrument.PedalHiHat] = 44,   // Hi-Hat Pedal
            [DrumInstrument.MidTom] = 47,       // Rack Tom 2 Center
            [DrumInstrument.HighTom] = 48,      // Rack Tom 1 Center
            [DrumInstrument.Ride] = 51,         // Ride Bow Tip
            [DrumInstrument.Crash] = 55,        // Crash Left Edge
            [DrumInstrument.Crash2] = 57,       // Crash Right Edge
            [DrumInstrument.OpenHiHat] = 70,    // Hi-Hat Tip Open 2
            [DrumInstrument.Cowbell] = 75,      // Cowbell Tip
            [DrumInstrument.HandClap] = 76,     // Clap
            [DrumInstrument.Tambourine] = 79,   // Tambourine Hit
            [DrumInstrument.Splash] = 50,       // Splash Edge
            [DrumInstrument.China] = 31         // China Edge
        });

    public static IReadOnlyList<DrumOutputProfile> All { get; } =
        [GeneralMidi, StevenSlateDrums55Factory];

    public static DrumOutputProfile FromId(DrumOutputProfileId id) => id switch
    {
        DrumOutputProfileId.StevenSlateDrums55Factory => StevenSlateDrums55Factory,
        _ => GeneralMidi
    };

    private static DrumOutputProfile Create(
        DrumOutputProfileId id,
        string displayName,
        string shortName,
        IReadOnlyDictionary<DrumInstrument, byte> midiNotes)
    {
        return new DrumOutputProfile
        {
            Id = id,
            DisplayName = displayName,
            ShortName = shortName,
            MidiNotes = midiNotes,
            SupportedInstruments = new HashSet<DrumInstrument>(midiNotes.Keys)
        };
    }
}
