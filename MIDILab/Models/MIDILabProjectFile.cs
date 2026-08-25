namespace MIDILab.Models;

public sealed class MIDILabProjectFile
{
    public int Version { get; set; } = 1;
    public MIDILabProjectSettings Settings { get; set; } = new();
    public MIDILabProjectPattern Pattern { get; set; } = new();
    public MIDILabProjectPattern? GeneratedBaseline { get; set; }
}

public sealed class MIDILabProjectSettings
{
    public int Bpm { get; set; } = 120;
    public int Bars { get; set; } = 4;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public string TimeSignatureGrouping { get; set; } = "4";
    public GrooveGenre Genre { get; set; } = GrooveGenre.Rock;
    public GrooveSection Section { get; set; } = GrooveSection.General;
    public double Energy { get; set; } = 55;
    public double Humanize { get; set; } = 45;
    public DrumOutputProfileId OutputProfile { get; set; } = DrumOutputProfileId.GeneralMidi;
    public List<DrumInstrument> ActiveInstruments { get; set; } = [];
    public string Theme { get; set; } = "Indigo";
    public bool DarkMode { get; set; }
}

public sealed class MIDILabProjectPattern
{
    public int Bars { get; set; } = 4;
    public int Bpm { get; set; } = 120;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public string TimeSignatureGrouping { get; set; } = "4";
    public List<MIDILabProjectHit> Hits { get; set; } = [];
}

public sealed class MIDILabProjectHit
{
    public DrumInstrument Instrument { get; set; }
    public int Step { get; set; }
    public int Velocity { get; set; } = 100;
    public int TimingOffsetTicks { get; set; }
}
