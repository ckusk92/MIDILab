namespace MIDILab.Models;

public sealed record TimeSignature(int Numerator, int Denominator, string Grouping)
{
    public static readonly TimeSignature FourFour = new(4, 4, "4");

    public int StepsPerDenominatorBeat => 16 / Denominator;
    public int StepsPerBar => Numerator * StepsPerDenominatorBeat;

    public int[] GroupSizes => Grouping
        .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(int.Parse)
        .ToArray();

    public IReadOnlySet<int> GroupStartSteps
    {
        get
        {
            var starts = new HashSet<int> { 0 };
            var accumulatedBeats = 0;
            foreach (var group in GroupSizes.Take(Math.Max(0, GroupSizes.Length - 1)))
            {
                accumulatedBeats += group;
                starts.Add(accumulatedBeats * StepsPerDenominatorBeat);
            }
            return starts;
        }
    }

    public override string ToString() => $"{Numerator}/{Denominator}";

    public static TimeSignature FromSelection(string signature, string? grouping = null) => signature switch
    {
        "3/4" => new TimeSignature(3, 4, "3"),
        "6/8" => new TimeSignature(6, 8, "3+3"),
        "5/4" => new TimeSignature(5, 4, NormalizeGrouping(grouping, "3+2", ["3+2", "2+3"])),
        "7/8" => new TimeSignature(7, 8, NormalizeGrouping(grouping, "2+2+3", ["2+2+3", "2+3+2", "3+2+2"])),
        _ => FourFour
    };

    private static string NormalizeGrouping(string? grouping, string fallback, string[] allowed) =>
        grouping is not null && allowed.Contains(grouping) ? grouping : fallback;
}
