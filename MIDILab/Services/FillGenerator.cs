using System;
using System.Collections.Generic;
using System.Linq;
using MIDILab.Models;

namespace MIDILab.Services;

public sealed class FillGenerator
{
    private readonly Random _random = new();

    public int Generate(
        DrumPattern pattern,
        int barIndex,
        int fillSteps,
        double intensity,
        double humanize,
        GrooveGenre genre,
        IReadOnlyCollection<DrumInstrument> allowedInstruments)
    {
        if (barIndex < 0 || barIndex >= pattern.Bars)
            throw new ArgumentOutOfRangeException(nameof(barIndex));

        intensity = Math.Clamp(intensity, 0.0, 1.0);
        humanize = Math.Clamp(humanize, 0.0, 1.0);
        fillSteps = Math.Clamp(fillSteps, 1, pattern.StepsPerBar);

        var barStart = barIndex * pattern.StepsPerBar;
        var barEnd = barStart + pattern.StepsPerBar;
        var fillStart = Math.Max(barStart, barEnd - fillSteps);
        var allowed = new HashSet<DrumInstrument>(allowedInstruments);

        var handDrums = new[]
        {
            DrumInstrument.Snare,
            DrumInstrument.HighTom,
            DrumInstrument.MidTom,
            DrumInstrument.LowTom,
            DrumInstrument.FloorTom
        }.Where(allowed.Contains).ToArray();

        var kickAvailable = allowed.Contains(DrumInstrument.Kick);
        var crashAvailable = allowed.Contains(DrumInstrument.Crash);
        var chinaAvailable = allowed.Contains(DrumInstrument.China);

        // If the custom kit has no normal fill drums, fall back to any active
        // non-cymbal instrument so the command can still produce something useful.
        if (handDrums.Length == 0)
        {
            handDrums = allowed
                .Where(i => i is not DrumInstrument.ClosedHiHat and not DrumInstrument.OpenHiHat and
                            not DrumInstrument.PedalHiHat and not DrumInstrument.Ride and
                            not DrumInstrument.Crash and not DrumInstrument.Crash2 and
                            not DrumInstrument.Splash and not DrumInstrument.China)
                .ToArray();
        }

        if (handDrums.Length == 0 && kickAvailable)
            handDrums = [DrumInstrument.Kick];

        if (handDrums.Length == 0)
            return 0;

        // A fill replaces the groove only inside its selected ending region.
        pattern.Hits.RemoveAll(h => h.Step >= fillStart && h.Step < barEnd);
        var style = ChooseStyle(genre, intensity);

        switch (style)
        {
            case FillStyle.SnareBuild:
                AddSnareBuild(pattern, fillStart, barEnd, intensity, humanize, handDrums, genre, kickAvailable);
                break;
            case FillStyle.Mixed:
                AddMixedFill(pattern, fillStart, barEnd, intensity, humanize, handDrums, genre, kickAvailable);
                break;
            default:
                AddTomRun(pattern, fillStart, barEnd, intensity, humanize, handDrums, genre, kickAvailable);
                break;
        }

        // Longer/high-energy fills occasionally announce their beginning with a
        // cymbal if that articulation is active. We intentionally do not add a
        // crash to the next bar, so the selected region is the only region changed.
        if (fillSteps >= Math.Max(4, pattern.StepsPerBar / 2) && intensity > 0.72 && _random.NextDouble() < 0.34)
        {
            var accent = genre == GrooveGenre.Metal && chinaAvailable ? DrumInstrument.China : DrumInstrument.Crash;
            if (allowed.Contains(accent))
                AddHit(pattern, accent, fillStart, 104 + (int)(intensity * 18), humanize * 0.65);
            else if (crashAvailable)
                AddHit(pattern, DrumInstrument.Crash, fillStart, 102 + (int)(intensity * 16), humanize * 0.65);
        }

        return pattern.Hits.Count(h => h.Step >= fillStart && h.Step < barEnd);
    }

    private FillStyle ChooseStyle(GrooveGenre genre, double intensity)
    {
        var roll = _random.NextDouble();
        return genre switch
        {
            GrooveGenre.Folk => roll < 0.58 ? FillStyle.SnareBuild : FillStyle.Mixed,
            GrooveGenre.Indie => roll < 0.38 ? FillStyle.SnareBuild : roll < 0.72 ? FillStyle.Mixed : FillStyle.TomRun,
            GrooveGenre.Metal => roll < 0.18 ? FillStyle.SnareBuild : roll < 0.48 ? FillStyle.Mixed : FillStyle.TomRun,
            _ => intensity < 0.35 && roll < 0.55 ? FillStyle.SnareBuild : roll < 0.48 ? FillStyle.Mixed : FillStyle.TomRun
        };
    }

    private void AddTomRun(
        DrumPattern pattern,
        int start,
        int end,
        double intensity,
        double humanize,
        DrumInstrument[] drums,
        GrooveGenre genre,
        bool kickAvailable)
    {
        var positions = BuildPositions(start, end, intensity, genre);
        var ordered = OrderDrumsForRun(drums);

        for (var i = 0; i < positions.Count; i++)
        {
            var progress = positions.Count <= 1 ? 1.0 : i / (double)(positions.Count - 1);
            var drumIndex = Math.Min(ordered.Length - 1, (int)Math.Floor(progress * ordered.Length));
            var instrument = ordered[drumIndex];
            var velocity = BaseVelocity(genre, intensity) - 9 + (int)(progress * 19);
            AddHit(pattern, instrument, positions[i], velocity, humanize);

            if (kickAvailable && intensity > 0.74 && i > 0 && i < positions.Count - 1 && i % 3 == 0 && _random.NextDouble() < 0.45)
                AddHit(pattern, DrumInstrument.Kick, positions[i], velocity - 7, humanize * 0.8);
        }
    }

    private void AddSnareBuild(
        DrumPattern pattern,
        int start,
        int end,
        double intensity,
        double humanize,
        DrumInstrument[] drums,
        GrooveGenre genre,
        bool kickAvailable)
    {
        var snare = drums.Contains(DrumInstrument.Snare) ? DrumInstrument.Snare : drums[0];
        var positions = BuildPositions(start, end, Math.Max(0.18, intensity - 0.08), genre);

        for (var i = 0; i < positions.Count; i++)
        {
            var progress = positions.Count <= 1 ? 1.0 : i / (double)(positions.Count - 1);
            var velocity = BaseVelocity(genre, intensity) - 20 + (int)(progress * 30);
            var instrument = snare;

            // Let the final quarter of a longer build spill naturally onto a tom.
            if (drums.Length > 1 && progress > 0.72 && _random.NextDouble() < 0.58)
                instrument = drums[Math.Min(drums.Length - 1, 1 + (i % (drums.Length - 1)))];

            AddHit(pattern, instrument, positions[i], velocity, humanize);
        }

        if (kickAvailable && intensity > 0.55)
            AddHit(pattern, DrumInstrument.Kick, start, BaseVelocity(genre, intensity) - 5, humanize * 0.8);
    }

    private void AddMixedFill(
        DrumPattern pattern,
        int start,
        int end,
        double intensity,
        double humanize,
        DrumInstrument[] drums,
        GrooveGenre genre,
        bool kickAvailable)
    {
        var positions = BuildPositions(start, end, intensity, genre);
        var ordered = OrderDrumsForRun(drums);
        var snare = drums.Contains(DrumInstrument.Snare) ? DrumInstrument.Snare : ordered[0];

        for (var i = 0; i < positions.Count; i++)
        {
            var progress = positions.Count <= 1 ? 1.0 : i / (double)(positions.Count - 1);
            DrumInstrument instrument;

            if (i == 0 || (i < positions.Count / 2 && _random.NextDouble() < 0.56))
                instrument = snare;
            else
                instrument = ordered[Math.Min(ordered.Length - 1, (int)(progress * ordered.Length))];

            var velocity = BaseVelocity(genre, intensity) - 13 + (int)(progress * 23);
            AddHit(pattern, instrument, positions[i], velocity, humanize);

            if (kickAvailable && intensity > 0.60 && (i == 0 || (i % 4 == 2 && _random.NextDouble() < intensity * 0.65)))
                AddHit(pattern, DrumInstrument.Kick, positions[i], velocity - 6, humanize * 0.75);
        }
    }

    private List<int> BuildPositions(int start, int end, double intensity, GrooveGenre genre)
    {
        var positions = new List<int>();
        var dense = intensity >= 0.58 || genre == GrooveGenre.Metal && intensity >= 0.42;
        var spacing = dense ? 1 : 2;

        for (var step = start; step < end; step += spacing)
        {
            var isFirst = step == start;
            var isLastAvailable = step + spacing >= end;
            var chance = isFirst || isLastAvailable
                ? 1.0
                : dense
                    ? 0.66 + intensity * 0.30
                    : 0.48 + intensity * 0.34;

            if (_random.NextDouble() <= Math.Clamp(chance, 0, 1))
                positions.Add(step);
        }

        if (positions.Count == 0)
            positions.Add(start);

        var lastStep = end - 1;
        if (intensity > 0.48 && !positions.Contains(lastStep))
            positions.Add(lastStep);

        positions.Sort();
        return positions;
    }

    private static DrumInstrument[] OrderDrumsForRun(DrumInstrument[] drums)
    {
        var order = new[]
        {
            DrumInstrument.Snare,
            DrumInstrument.HighTom,
            DrumInstrument.MidTom,
            DrumInstrument.LowTom,
            DrumInstrument.FloorTom,
            DrumInstrument.Kick,
            DrumInstrument.Rimshot,
            DrumInstrument.HandClap,
            DrumInstrument.Tambourine,
            DrumInstrument.Cowbell
        };

        return drums.OrderBy(drum =>
        {
            var index = Array.IndexOf(order, drum);
            return index >= 0 ? index : int.MaxValue;
        }).ToArray();
    }

    private static int BaseVelocity(GrooveGenre genre, double intensity) => genre switch
    {
        GrooveGenre.Folk => 78 + (int)(intensity * 19),
        GrooveGenre.Indie => 86 + (int)(intensity * 22),
        GrooveGenre.Metal => 101 + (int)(intensity * 22),
        _ => 91 + (int)(intensity * 22)
    };

    private void AddHit(DrumPattern pattern, DrumInstrument instrument, int step, int baseVelocity, double humanize)
    {
        if (pattern.FindHit(instrument, step) is not null)
            return;

        var velocitySpread = (int)Math.Round(8 * humanize);
        var timingSpread = (int)Math.Round(7 * humanize);
        pattern.Hits.Add(new DrumHit
        {
            Instrument = instrument,
            Step = step,
            Velocity = Math.Clamp(baseVelocity + RandomSigned(velocitySpread), 1, 127),
            TimingOffsetTicks = RandomSigned(timingSpread)
        });
    }

    private int RandomSigned(int amount) =>
        amount <= 0 ? 0 : _random.Next(-amount, amount + 1);

    private enum FillStyle
    {
        TomRun,
        SnareBuild,
        Mixed
    }
}
