using System;
using System.Collections.Generic;
using System.Linq;
using MIDILab.Models;

namespace MIDILab.Services;

public sealed class GenreGrooveGenerator
{
    private readonly Random _random = new();

    public DrumPattern Generate(
        int bpm,
        int bars,
        double energy,
        double humanize,
        GrooveGenre genre,
        TimeSignature? timeSignature = null,
        GrooveSection section = GrooveSection.General,
        IReadOnlyCollection<DrumInstrument>? allowedInstruments = null)
    {
        var meter = timeSignature ?? TimeSignature.FourFour;
        var pattern = new DrumPattern { Bpm = bpm, Bars = bars, Meter = meter };
        var sectionEnergy = AdjustEnergyForSection(energy, section);

        for (var bar = 0; bar < bars; bar++)
        {
            var start = bar * pattern.StepsPerBar;

            if (meter != TimeSignature.FourFour)
            {
                AddMeterAwareBar(pattern, start, bar, bars, sectionEnergy, humanize, genre);
                continue;
            }

            switch (genre)
            {
                case GrooveGenre.Indie:
                    AddIndieBar(pattern, start, bar, bars, sectionEnergy, humanize);
                    break;
                case GrooveGenre.Folk:
                    AddFolkBar(pattern, start, bar, bars, sectionEnergy, humanize);
                    break;
                case GrooveGenre.Metal:
                    AddMetalBar(pattern, start, bar, bars, sectionEnergy, humanize);
                    break;
                default:
                    AddRockBar(pattern, start, bar, bars, sectionEnergy, humanize);
                    break;
            }
        }

        ApplySectionShape(pattern, section, energy, humanize, genre);

        var allowed = allowedInstruments is null
            ? null
            : new HashSet<DrumInstrument>(allowedInstruments);

        AddOptionalKitColors(pattern, energy, humanize, genre, section, allowed);

        if (allowed is not null)
            pattern.Hits.RemoveAll(h => !allowed.Contains(h.Instrument));

        return pattern;
    }

    private void AddOptionalKitColors(
        DrumPattern pattern,
        double energy,
        double humanize,
        GrooveGenre genre,
        GrooveSection section,
        HashSet<DrumInstrument>? allowed)
    {
        if (allowed is null || pattern.TotalSteps == 0)
            return;

        bool Has(DrumInstrument instrument) => allowed.Contains(instrument);
        var steps = pattern.StepsPerBar;

        for (var bar = 0; bar < pattern.Bars; bar++)
        {
            var start = bar * steps;
            var isOpeningBar = bar == 0;
            var isEndingBar = bar == pattern.Bars - 1;

            if (Has(DrumInstrument.Crash2) &&
                (section == GrooveSection.Chorus || genre == GrooveGenre.Metal) &&
                _random.NextDouble() < 0.22 + energy * 0.30)
            {
                AddHit(pattern, DrumInstrument.Crash2, start, 106 + (int)(energy * 12), humanize * 0.75);
            }

            if (Has(DrumInstrument.China) &&
                genre == GrooveGenre.Metal &&
                energy > 0.55 &&
                _random.NextDouble() < 0.16 + energy * 0.22)
            {
                AddHit(pattern, DrumInstrument.China, start, 110 + (int)(energy * 10), humanize * 0.7);
            }

            if (Has(DrumInstrument.Splash) &&
                !isOpeningBar &&
                _random.NextDouble() < 0.08 + energy * 0.12)
            {
                var splashStep = start + Math.Min(steps - 1, Math.Max(0, steps / 2));
                AddHit(pattern, DrumInstrument.Splash, splashStep, 84 + (int)(energy * 12), humanize);
            }

            if (Has(DrumInstrument.PedalHiHat) &&
                genre is GrooveGenre.Folk or GrooveGenre.Indie &&
                _random.NextDouble() < 0.35)
            {
                for (var step = 0; step < steps; step += Math.Max(2, pattern.Meter.StepsPerDenominatorBeat))
                    TryHit(pattern, DrumInstrument.PedalHiHat, start + step, 0.30 + energy * 0.20, 66, humanize * 0.8);
            }

            if (Has(DrumInstrument.Tambourine) &&
                genre is GrooveGenre.Folk or GrooveGenre.Indie &&
                energy > 0.35)
            {
                var spacing = Math.Max(2, pattern.Meter.StepsPerDenominatorBeat);
                for (var step = 0; step < steps; step += spacing)
                    TryHit(pattern, DrumInstrument.Tambourine, start + step, 0.25 + energy * 0.28, 72, humanize);
            }

            if (Has(DrumInstrument.Cowbell) &&
                section == GrooveSection.Bridge &&
                _random.NextDouble() < 0.40)
            {
                var spacing = Math.Max(2, pattern.Meter.StepsPerDenominatorBeat);
                for (var step = 0; step < steps; step += spacing)
                    TryHit(pattern, DrumInstrument.Cowbell, start + step, 0.42, 78, humanize);
            }

            if (Has(DrumInstrument.HandClap) &&
                (section == GrooveSection.Chorus || genre == GrooveGenre.Indie))
            {
                foreach (var snare in pattern.Hits
                    .Where(h => h.Instrument == DrumInstrument.Snare && h.Step >= start && h.Step < start + steps && h.Velocity >= 88)
                    .ToList())
                {
                    if (_random.NextDouble() < 0.28 + energy * 0.30)
                        AddHit(pattern, DrumInstrument.HandClap, snare.Step, Math.Max(68, snare.Velocity - 18), humanize * 0.7);
                }
            }

            if (Has(DrumInstrument.Rimshot) &&
                section is GrooveSection.Verse or GrooveSection.Intro &&
                energy < 0.65)
            {
                foreach (var snare in pattern.Hits
                    .Where(h => h.Instrument == DrumInstrument.Snare && h.Step >= start && h.Step < start + steps && h.Velocity < 80)
                    .ToList())
                {
                    if (_random.NextDouble() < 0.35)
                        AddHit(pattern, DrumInstrument.Rimshot, snare.Step, 58, humanize);
                }
            }

            if (Has(DrumInstrument.LowTom) && isEndingBar && energy > 0.55 && steps >= 4)
            {
                TryHit(pattern, DrumInstrument.LowTom, start + steps - 2, 0.32 + energy * 0.25, 94, humanize);
            }
        }
    }

    private static double AdjustEnergyForSection(double energy, GrooveSection section) => section switch
    {
        GrooveSection.Intro => Math.Clamp(energy - 0.18, 0, 1),
        GrooveSection.Verse => Math.Clamp(energy - 0.08, 0, 1),
        GrooveSection.PreChorus => Math.Clamp(energy + 0.05, 0, 1),
        GrooveSection.Chorus => Math.Clamp(energy + 0.16, 0, 1),
        GrooveSection.Bridge => Math.Clamp(energy - 0.02, 0, 1),
        GrooveSection.Breakdown => Math.Clamp(energy - 0.24, 0, 1),
        GrooveSection.Outro => Math.Clamp(energy + 0.02, 0, 1),
        _ => energy
    };

    private void ApplySectionShape(DrumPattern pattern, GrooveSection section, double requestedEnergy, double humanize, GrooveGenre genre)
    {
        if (section == GrooveSection.General || pattern.TotalSteps == 0)
            return;

        var steps = pattern.StepsPerBar;

        if (section == GrooveSection.Intro)
        {
            // Leave more space at the beginning and avoid an automatic opening crash.
            pattern.Hits.RemoveAll(h => h.Step == 0 && h.Instrument == DrumInstrument.Crash);
            if (pattern.Bars > 1 && _random.NextDouble() < 0.55)
                pattern.Hits.RemoveAll(h => h.Step < steps && h.Instrument == DrumInstrument.Snare && h.Velocity < 75);
        }
        else if (section == GrooveSection.Verse)
        {
            // Keep verses controlled by reducing decorative crashes and some ghost notes.
            pattern.Hits.RemoveAll(h => h.Instrument == DrumInstrument.Crash && h.Step >= steps);
            pattern.Hits.RemoveAll(h => h.Instrument == DrumInstrument.Snare && h.Velocity < 55 && _random.NextDouble() < 0.45);
        }
        else if (section == GrooveSection.PreChorus)
        {
            // Add a simple lift toward the end without turning the whole section into a fill.
            var lastBar = (pattern.Bars - 1) * steps;
            var liftStart = lastBar + Math.Max(0, steps - Math.Min(4, steps));
            for (var step = liftStart; step < pattern.TotalSteps; step++)
            {
                if ((step - liftStart) % 2 == 0)
                    AddHit(pattern, DrumInstrument.Snare, step, 82 + (step - liftStart) * 3, humanize);
            }
        }
        else if (section == GrooveSection.Chorus)
        {
            // Choruses announce themselves with crashes and slightly more open cymbal color.
            for (var bar = 0; bar < pattern.Bars; bar += 2)
                AddHit(pattern, DrumInstrument.Crash, bar * steps, genre == GrooveGenre.Metal ? 121 : 114, humanize * 0.8);

            if (requestedEnergy > 0.45)
            {
                for (var bar = 0; bar < pattern.Bars; bar++)
                {
                    var step = bar * steps + Math.Max(0, steps - 2);
                    if (pattern.FindHit(DrumInstrument.ClosedHiHat, step) is not null)
                        ReplaceHat(pattern, step, DrumInstrument.OpenHiHat, 88 + (int)(requestedEnergy * 12), humanize);
                }
            }
        }
        else if (section == GrooveSection.Bridge)
        {
            // Encourage a noticeably different orchestration from a standard verse/chorus.
            for (var bar = 0; bar < pattern.Bars; bar++)
            {
                var start = bar * steps;
                if (_random.NextDouble() < 0.65)
                    AddHit(pattern, DrumInstrument.Ride, start, 88 + (int)(requestedEnergy * 12), humanize);
                if (steps >= 8 && _random.NextDouble() < 0.5)
                    AddHit(pattern, DrumInstrument.FloorTom, start + steps / 2, 84 + (int)(requestedEnergy * 10), humanize);
            }
        }
        else if (section == GrooveSection.Breakdown)
        {
            // Make the part intentionally sparse while retaining the primary pulse.
            pattern.Hits.RemoveAll(h =>
                (h.Instrument == DrumInstrument.ClosedHiHat || h.Instrument == DrumInstrument.Ride) &&
                (h.Step % Math.Max(1, pattern.Meter.StepsPerDenominatorBeat * 2)) != 0);
            pattern.Hits.RemoveAll(h => h.Instrument == DrumInstrument.Crash && h.Step != 0);
        }
        else if (section == GrooveSection.Outro)
        {
            // Give the ending a more deliberate final punctuation.
            var finalStep = pattern.TotalSteps - 1;
            AddHit(pattern, DrumInstrument.FloorTom, finalStep, 104 + (int)(requestedEnergy * 10), humanize);
            if (pattern.TotalSteps > 1)
                AddHit(pattern, DrumInstrument.Snare, finalStep - 1, 98 + (int)(requestedEnergy * 8), humanize);
        }
    }

    private void AddRockBar(DrumPattern pattern, int start, int bar, int bars, double energy, double humanize)
    {
        AddStraightHats(pattern, start, energy, humanize, sixteenthThreshold: 0.48, baseVelocity: 72);
        AddBackbeat(pattern, start, humanize, 110, ghostChance: 0.30);

        AddHit(pattern, DrumInstrument.Kick, start, 108, humanize);
        AddHit(pattern, DrumInstrument.Kick, start + 8, 104, humanize);

        TryHit(pattern, DrumInstrument.Kick, start + 6, 0.62, 96, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 14, 0.30 + energy * 0.55, 99, humanize);
        if (energy > 0.72)
            TryHit(pattern, DrumInstrument.Kick, start + 10, energy * 0.75, 95, humanize);

        if (bar == 0)
            AddHit(pattern, DrumInstrument.Crash, start, 112, humanize);

        if (bar == bars - 1 && energy >= 0.55)
            AddTomFill(pattern, start, energy, humanize);
    }

    private void AddIndieBar(DrumPattern pattern, int start, int bar, int bars, double energy, double humanize)
    {
        // Indie keeps a recognizable backbeat but favors syncopated kicks and hat variation.
        AddStraightHats(pattern, start, energy, humanize, sixteenthThreshold: 0.62, baseVelocity: 68);
        AddBackbeat(pattern, start, humanize, 106, ghostChance: 0.48);

        AddHit(pattern, DrumInstrument.Kick, start, 104, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 3, 0.22 + energy * 0.35, 89, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 6, 0.55, 95, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 8, 0.72, 101, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 11, 0.22 + energy * 0.35, 91, humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 14, 0.50 + energy * 0.30, 96, humanize);

        if (energy > 0.52 && _random.NextDouble() < 0.55)
            ReplaceHat(pattern, start + 14, DrumInstrument.OpenHiHat, 88, humanize);

        if (bar == 0 && _random.NextDouble() < 0.75)
            AddHit(pattern, DrumInstrument.Crash, start, 106, humanize);

        if (bar == bars - 1 && energy > 0.45)
            AddIndieFill(pattern, start, energy, humanize);
    }

    private void AddFolkBar(DrumPattern pattern, int start, int bar, int bars, double energy, double humanize)
    {
        // Restrained groove: mostly eighth-note hats, lighter dynamics and simpler kick movement.
        for (var step = 0; step < pattern.StepsPerBar; step += 2)
        {
            var accent = step % 4 == 0;
            var velocity = accent ? 70 : 58;
            AddHit(pattern, DrumInstrument.ClosedHiHat, start + step, velocity + (int)(energy * 8), humanize * 0.8);
        }

        AddHit(pattern, DrumInstrument.Snare, start + 4, 90 + (int)(energy * 8), humanize);
        AddHit(pattern, DrumInstrument.Snare, start + 12, 92 + (int)(energy * 8), humanize);

        AddHit(pattern, DrumInstrument.Kick, start, 94 + (int)(energy * 7), humanize);
        if (_random.NextDouble() < 0.72)
            AddHit(pattern, DrumInstrument.Kick, start + 8, 90 + (int)(energy * 7), humanize);
        TryHit(pattern, DrumInstrument.Kick, start + 10, Math.Max(0, energy - 0.48) * 0.70, 84, humanize);

        if (bar == 0 && energy > 0.72 && _random.NextDouble() < 0.35)
            AddHit(pattern, DrumInstrument.Crash, start, 94, humanize);

        if (bar == bars - 1 && energy > 0.70)
        {
            AddHit(pattern, DrumInstrument.Snare, start + 14, 78, humanize);
            AddHit(pattern, DrumInstrument.Snare, start + 15, 86, humanize);
        }
    }

    private void AddMetalBar(DrumPattern pattern, int start, int bar, int bars, double energy, double humanize)
    {
        // Metal gets denser cymbal work and substantially more kick activity as energy rises.
        var useRide = energy > 0.60 && _random.NextDouble() < 0.45;
        var cymbal = useRide ? DrumInstrument.Ride : DrumInstrument.ClosedHiHat;

        for (var step = 0; step < pattern.StepsPerBar; step++)
        {
            var isEighth = step % 2 == 0;
            if (!isEighth && _random.NextDouble() > Math.Clamp((energy - 0.40) * 1.7, 0, 0.95))
                continue;

            var accent = step % 4 == 0;
            AddHit(pattern, cymbal, start + step, (accent ? 99 : 84) + (int)(energy * 8), humanize * 0.75);
        }

        AddBackbeat(pattern, start, humanize * 0.8, 118, ghostChance: 0.08);

        AddHit(pattern, DrumInstrument.Kick, start, 116, humanize * 0.75);
        AddHit(pattern, DrumInstrument.Kick, start + 8, 114, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 2, 0.22 + energy * 0.50, 105, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 3, Math.Max(0, energy - 0.58) * 1.7, 102, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 6, 0.45 + energy * 0.40, 108, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 10, 0.30 + energy * 0.55, 106, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 14, 0.55 + energy * 0.35, 109, humanize * 0.75);
        TryHit(pattern, DrumInstrument.Kick, start + 15, Math.Max(0, energy - 0.62) * 1.9, 104, humanize * 0.75);

        if (bar == 0 || (energy > 0.78 && bar % 4 == 0))
            AddHit(pattern, DrumInstrument.Crash, start, 120, humanize * 0.7);

        if (bar == bars - 1 && energy > 0.42)
            AddMetalFill(pattern, start, energy, humanize);
    }

    private void AddStraightHats(
        DrumPattern pattern,
        int start,
        double energy,
        double humanize,
        double sixteenthThreshold,
        int baseVelocity)
    {
        for (var step = 0; step < pattern.StepsPerBar; step++)
        {
            var isEighth = step % 2 == 0;
            var sixteenthChance = Math.Clamp((energy - sixteenthThreshold) * 1.6, 0, 0.85);
            if (!isEighth && _random.NextDouble() > sixteenthChance)
                continue;

            var accent = step % 4 == 0;
            var velocity = baseVelocity + (accent ? 16 : 0) + (int)(energy * 10);
            AddHit(pattern, DrumInstrument.ClosedHiHat, start + step, velocity, humanize);
        }
    }

    private void AddBackbeat(DrumPattern pattern, int start, double humanize, int velocity, double ghostChance)
    {
        AddHit(pattern, DrumInstrument.Snare, start + 4, velocity, humanize);
        AddHit(pattern, DrumInstrument.Snare, start + 12, velocity + 2, humanize);

        if (_random.NextDouble() < ghostChance)
        {
            var ghostStep = _random.NextDouble() < 0.5 ? 10 : 15;
            AddHit(pattern, DrumInstrument.Snare, start + ghostStep, 44, humanize);
        }
    }

    private void AddTomFill(DrumPattern pattern, int start, double energy, double humanize)
    {
        RemoveCymbalsFrom(pattern, start + 12);
        AddHit(pattern, DrumInstrument.Snare, start + 12, 96, humanize);
        AddHit(pattern, DrumInstrument.HighTom, start + 13, 91, humanize);
        AddHit(pattern, DrumInstrument.MidTom, start + 14, 96, humanize);
        AddHit(pattern, DrumInstrument.FloorTom, start + 15, 102 + (int)(energy * 8), humanize);
    }

    private void AddIndieFill(DrumPattern pattern, int start, double energy, double humanize)
    {
        RemoveCymbalsFrom(pattern, start + 14);
        if (_random.NextDouble() < 0.5)
        {
            AddHit(pattern, DrumInstrument.Snare, start + 14, 82, humanize);
            AddHit(pattern, DrumInstrument.Snare, start + 15, 96 + (int)(energy * 5), humanize);
        }
        else
        {
            AddHit(pattern, DrumInstrument.HighTom, start + 14, 86, humanize);
            AddHit(pattern, DrumInstrument.FloorTom, start + 15, 98, humanize);
        }
    }

    private void AddMetalFill(DrumPattern pattern, int start, double energy, double humanize)
    {
        RemoveCymbalsFrom(pattern, start + 12);
        AddHit(pattern, DrumInstrument.Snare, start + 12, 108, humanize * 0.8);
        AddHit(pattern, DrumInstrument.HighTom, start + 13, 106, humanize * 0.8);
        AddHit(pattern, DrumInstrument.MidTom, start + 14, 111, humanize * 0.8);
        AddHit(pattern, DrumInstrument.FloorTom, start + 15, 116 + (int)(energy * 5), humanize * 0.8);
    }

    private void RemoveCymbalsFrom(DrumPattern pattern, int step)
    {
        pattern.Hits.RemoveAll(h =>
            h.Step >= step &&
            (h.Instrument == DrumInstrument.ClosedHiHat ||
             h.Instrument == DrumInstrument.OpenHiHat ||
             h.Instrument == DrumInstrument.Ride));
    }

    private void ReplaceHat(DrumPattern pattern, int step, DrumInstrument replacement, int velocity, double humanize)
    {
        pattern.Hits.RemoveAll(h =>
            h.Step == step &&
            (h.Instrument == DrumInstrument.ClosedHiHat || h.Instrument == DrumInstrument.OpenHiHat));
        AddHit(pattern, replacement, step, velocity, humanize);
    }

    private void TryHit(
        DrumPattern pattern,
        DrumInstrument instrument,
        int step,
        double probability,
        int velocity,
        double humanize)
    {
        if (_random.NextDouble() < Math.Clamp(probability, 0, 1))
            AddHit(pattern, instrument, step, velocity, humanize);
    }

    private void AddMeterAwareBar(
        DrumPattern pattern, int start, int bar, int bars, double energy, double humanize, GrooveGenre genre)
    {
        var meter = pattern.Meter;
        var stepsPerBeat = meter.StepsPerDenominatorBeat;
        var groupStarts = meter.GroupStartSteps.OrderBy(x => x).ToArray();

        // Cymbal pulse follows the denominator beat. Higher energy may fill the
        // intervening 16ths, while group starts receive a stronger accent.
        for (var local = 0; local < pattern.StepsPerBar; local += stepsPerBeat)
        {
            var groupAccent = meter.GroupStartSteps.Contains(local);
            var instrument = genre == GrooveGenre.Metal && energy > 0.62
                ? DrumInstrument.Ride
                : DrumInstrument.ClosedHiHat;
            var baseVelocity = genre switch
            {
                GrooveGenre.Folk => groupAccent ? 70 : 58,
                GrooveGenre.Metal => groupAccent ? 103 : 88,
                GrooveGenre.Indie => groupAccent ? 86 : 68,
                _ => groupAccent ? 90 : 72
            };
            AddHit(pattern, instrument, start + local, baseVelocity + (int)(energy * 7), humanize);

            if (stepsPerBeat > 1 && energy > 0.68 && _random.NextDouble() < (energy - 0.55))
                AddHit(pattern, instrument, start + local + 1, Math.Max(45, baseVelocity - 18), humanize);
        }

        // Kick reinforces the opening and selected grouping boundaries.
        AddHit(pattern, DrumInstrument.Kick, start, GenreKickVelocity(genre, energy), humanize);
        for (var i = 1; i < groupStarts.Length; i++)
        {
            var boundary = groupStarts[i];
            if (i % 2 == 0 || _random.NextDouble() < 0.58 + energy * 0.25)
                AddHit(pattern, DrumInstrument.Kick, start + boundary, GenreKickVelocity(genre, energy) - 7, humanize);
            else
                AddHit(pattern, DrumInstrument.Snare, start + boundary, GenreSnareVelocity(genre, energy), humanize);
        }

        // Meter-specific anchors make common signatures feel intentional rather
        // than like truncated 4/4 patterns.
        if (meter.Numerator == 3 && meter.Denominator == 4)
        {
            AddHit(pattern, DrumInstrument.Snare, start + 4, GenreSnareVelocity(genre, energy), humanize);
            TryHit(pattern, DrumInstrument.Kick, start + 8, 0.55 + energy * 0.25, GenreKickVelocity(genre, energy) - 5, humanize);
        }
        else if (meter.Numerator == 6 && meter.Denominator == 8)
        {
            AddHit(pattern, DrumInstrument.Snare, start + 6, GenreSnareVelocity(genre, energy), humanize);
            TryHit(pattern, DrumInstrument.Kick, start + 4, 0.35 + energy * 0.35, GenreKickVelocity(genre, energy) - 8, humanize);
            TryHit(pattern, DrumInstrument.Kick, start + 10, 0.25 + energy * 0.35, GenreKickVelocity(genre, energy) - 8, humanize);
        }
        else if (groupStarts.Length > 1)
        {
            // In odd meters, alternate snare/kick emphasis across the chosen groups.
            for (var i = 1; i < groupStarts.Length; i++)
            {
                var boundary = start + groupStarts[i];
                if (i % 2 == 1)
                    AddHit(pattern, DrumInstrument.Snare, boundary, GenreSnareVelocity(genre, energy), humanize);
            }
        }

        if (bar == 0 && genre != GrooveGenre.Folk)
            AddHit(pattern, DrumInstrument.Crash, start, genre == GrooveGenre.Metal ? 120 : 108, humanize);

        if (bar == bars - 1 && energy > 0.58)
            AddMeterFill(pattern, start, energy, humanize, genre);
    }

    private void AddMeterFill(DrumPattern pattern, int start, double energy, double humanize, GrooveGenre genre)
    {
        var fillSteps = Math.Min(4, pattern.StepsPerBar);
        var fillStart = start + pattern.StepsPerBar - fillSteps;
        RemoveCymbalsFrom(pattern, fillStart);
        var drums = new[] { DrumInstrument.Snare, DrumInstrument.HighTom, DrumInstrument.MidTom, DrumInstrument.FloorTom };
        for (var i = 0; i < fillSteps; i++)
        {
            var velocity = (genre == GrooveGenre.Metal ? 104 : 88) + i * 4 + (int)(energy * 5);
            AddHit(pattern, drums[i], fillStart + i, velocity, humanize);
        }
    }

    private static int GenreKickVelocity(GrooveGenre genre, double energy) => genre switch
    {
        GrooveGenre.Folk => 92 + (int)(energy * 7),
        GrooveGenre.Metal => 114 + (int)(energy * 6),
        GrooveGenre.Indie => 101 + (int)(energy * 7),
        _ => 106 + (int)(energy * 7)
    };

    private static int GenreSnareVelocity(GrooveGenre genre, double energy) => genre switch
    {
        GrooveGenre.Folk => 90 + (int)(energy * 7),
        GrooveGenre.Metal => 117 + (int)(energy * 5),
        GrooveGenre.Indie => 104 + (int)(energy * 7),
        _ => 109 + (int)(energy * 7)
    };

    private void AddHit(
        DrumPattern pattern,
        DrumInstrument instrument,
        int step,
        int baseVelocity,
        double humanize)
    {
        // Avoid duplicate notes of the same drum at the same grid position.
        if (pattern.FindHit(instrument, step) is not null)
            return;

        var velocitySpread = (int)Math.Round(8 * humanize);
        var timingSpread = (int)Math.Round(7 * humanize);

        var velocity = Math.Clamp(baseVelocity + RandomSigned(velocitySpread), 1, 127);
        pattern.Hits.Add(new DrumHit
        {
            Instrument = instrument,
            Step = step,
            Velocity = velocity,
            TimingOffsetTicks = RandomSigned(timingSpread)
        });
    }

    private int RandomSigned(int amount) =>
        amount <= 0 ? 0 : _random.Next(-amount, amount + 1);
}
