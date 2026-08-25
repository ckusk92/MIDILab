namespace MIDILab.Models;

public sealed class DrumHit
{
    public required DrumInstrument Instrument { get; init; }
    public required int Step { get; init; }
    public int Velocity { get; set; } = 100;
    public int TimingOffsetTicks { get; set; }
}
