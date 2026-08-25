using System.Collections.Generic;

namespace MIDILab.Models;

public enum DrumOutputProfileId
{
    GeneralMidi,
    StevenSlateDrums55Factory
}

public sealed class DrumOutputProfile
{
    public required DrumOutputProfileId Id { get; init; }
    public required string DisplayName { get; init; }
    public required string ShortName { get; init; }
    public required IReadOnlyDictionary<DrumInstrument, byte> MidiNotes { get; init; }
    public required IReadOnlySet<DrumInstrument> SupportedInstruments { get; init; }

    public bool Supports(DrumInstrument instrument) => SupportedInstruments.Contains(instrument);
}
