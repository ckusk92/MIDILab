namespace MIDILab.Models;

public sealed class DrumPattern
{
    public int Bars { get; set; } = 4;
    public int Bpm { get; set; } = 120;
    public TimeSignature Meter { get; set; } = TimeSignature.FourFour;
    public List<DrumHit> Hits { get; } = [];

    public int StepsPerBar => Meter.StepsPerBar;
    public int TotalSteps => Bars * StepsPerBar;

    public DrumHit? FindHit(DrumInstrument instrument, int step) =>
        Hits.FirstOrDefault(h => h.Instrument == instrument && h.Step == step);

    public void ToggleHit(DrumInstrument instrument, int step, int defaultVelocity = 100)
    {
        var existing = FindHit(instrument, step);
        if (existing is not null)
        {
            Hits.Remove(existing);
            return;
        }

        Hits.Add(new DrumHit
        {
            Instrument = instrument,
            Step = step,
            Velocity = defaultVelocity
        });
    }

    public DrumPattern Clone()
    {
        var clone = new DrumPattern
        {
            Bars = Bars,
            Bpm = Bpm,
            Meter = Meter
        };

        foreach (var hit in Hits)
        {
            clone.Hits.Add(new DrumHit
            {
                Instrument = hit.Instrument,
                Step = hit.Step,
                Velocity = hit.Velocity,
                TimingOffsetTicks = hit.TimingOffsetTicks
            });
        }

        return clone;
    }
}
