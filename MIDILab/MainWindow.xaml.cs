using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MIDILab.Models;
using MIDILab.Services;
using Microsoft.Win32;

namespace MIDILab;

public partial class MainWindow : Window
{
    private static readonly DrumInstrument[] DefaultInstruments =
    [
        DrumInstrument.Crash,
        DrumInstrument.Ride,
        DrumInstrument.OpenHiHat,
        DrumInstrument.ClosedHiHat,
        DrumInstrument.Snare,
        DrumInstrument.HighTom,
        DrumInstrument.MidTom,
        DrumInstrument.FloorTom,
        DrumInstrument.Kick
    ];

    private static readonly DrumInstrument[] AvailableInstruments =
    [
        DrumInstrument.Crash,
        DrumInstrument.Crash2,
        DrumInstrument.Splash,
        DrumInstrument.China,
        DrumInstrument.Ride,
        DrumInstrument.OpenHiHat,
        DrumInstrument.ClosedHiHat,
        DrumInstrument.PedalHiHat,
        DrumInstrument.Tambourine,
        DrumInstrument.Cowbell,
        DrumInstrument.HandClap,
        DrumInstrument.Rimshot,
        DrumInstrument.Snare,
        DrumInstrument.HighTom,
        DrumInstrument.MidTom,
        DrumInstrument.LowTom,
        DrumInstrument.FloorTom,
        DrumInstrument.Kick
    ];

    private readonly List<DrumInstrument> _activeInstruments = [.. DefaultInstruments];

    private readonly GenreGrooveGenerator _generator = new();
    private readonly MidiExporter _midiExporter = new();
    private readonly MidiPreviewPlayer _previewPlayer = new();
    private DrumPattern _pattern = new();
    private DrumPattern? _generatedBaseline;
    private readonly DispatcherTimer _playheadTimer;
    private readonly Stopwatch _playheadStopwatch = new();
    private Border? _playheadMarker;
    private int _currentPlayheadStep = -1;

    public MainWindow()
    {
        InitializeComponent();

        _playheadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _playheadTimer.Tick += PlayheadTimer_Tick;

        BuildKitOptions();
        DrawEditor();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
        GenerateVariation("Generated a new starting groove");
    }

    private void VariationButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
        GenerateVariation("Generated a new variation");
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_generatedBaseline is null)
            return;

        StopPreview();
        _pattern = _generatedBaseline.Clone();
        DrawEditor();
        ResetButton.IsEnabled = false;
        StatusTextBlock.Text = "Reset all manual edits to the most recently generated variation.";
    }

    private void GenerateVariation(string statusPrefix)
    {
        if (!int.TryParse(BpmTextBox.Text, out var bpm) || bpm is < 20 or > 400)
        {
            MessageBox.Show("Enter a BPM between 20 and 400.", "Invalid BPM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bars = GetSelectedBars();
        var genre = GetSelectedGenre();
        var section = GetSelectedSection();
        var energy = EnergySlider.Value / 100.0;
        var humanize = HumanizeSlider.Value / 100.0;
        var timeSignature = GetSelectedTimeSignature();

        _pattern = _generator.Generate(bpm, bars, energy, humanize, genre, timeSignature, section, _activeInstruments);
        _generatedBaseline = _pattern.Clone();

        DrawEditor();
        VariationButton.IsEnabled = true;
        ResetButton.IsEnabled = false;
        StatusTextBlock.Text = $"{statusPrefix}: {genre}, {FormatSection(section)}, {timeSignature} {FormatGrouping(timeSignature)}, {bars} bars at {bpm} BPM. Click cells to customize it.";
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pattern.Hits.Count == 0)
        {
            MessageBox.Show("Generate or add some drum hits first.", "Nothing to play", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PlayButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusTextBlock.Text = $"Previewing {_pattern.Bars} bars at {_pattern.Bpm} BPM...";
        StartPlayhead();

        try
        {
            await _previewPlayer.PlayAsync(_pattern);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MIDI Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusTextBlock.Text = "Preview could not start. MIDI export still works normally.";
        }
        finally
        {
            StopPlayhead();
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;

            if (!_previewPlayer.IsPlaying && StatusTextBlock.Text.StartsWith("Previewing", StringComparison.Ordinal))
                StatusTextBlock.Text = "Preview finished.";
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
        StatusTextBlock.Text = "Preview stopped.";
    }

    private void StopPreview()
    {
        _previewPlayer.Stop();
        StopPlayhead();
        PlayButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void StartPlayhead()
    {
        StopPlayhead();
        DrumScrollViewer.ScrollToHorizontalOffset(0);
        _playheadStopwatch.Restart();
        ShowPlayheadStep(0);
        _playheadTimer.Start();
    }

    private void PlayheadTimer_Tick(object? sender, EventArgs e)
    {
        if (_pattern.TotalSteps <= 0 || _pattern.Bpm <= 0)
            return;

        var millisecondsPerStep = 60_000.0 / _pattern.Bpm / 4.0;
        var step = (int)(_playheadStopwatch.Elapsed.TotalMilliseconds / millisecondsPerStep);

        if (step >= _pattern.TotalSteps)
        {
            StopPlayhead();
            return;
        }

        if (step != _currentPlayheadStep)
            ShowPlayheadStep(step);
    }

    private void ShowPlayheadStep(int step)
    {
        if (_playheadMarker is not null)
            EditorGrid.Children.Remove(_playheadMarker);

        _currentPlayheadStep = step;
        _playheadMarker = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, 30, 144, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 30, 144, 255)),
            BorderThickness = new Thickness(2, 0, 2, 0),
            IsHitTestVisible = false
        };

        Grid.SetRow(_playheadMarker, 0);
        Grid.SetRowSpan(_playheadMarker, _activeInstruments.Count + 1);
        Grid.SetColumn(_playheadMarker, step + 1);
        Panel.SetZIndex(_playheadMarker, 100);
        EditorGrid.Children.Add(_playheadMarker);
        AutoScrollToPlayhead(step);
    }

    private void AutoScrollToPlayhead(int step)
    {
        // The first editor column is 105 px wide and each 16th-note step is 34 px.
        // Follow playback in chunks: once the playhead reaches about 80% of the
        // visible area, advance the viewport so it returns to roughly 25%.
        const double instrumentColumnWidth = 105.0;
        const double stepColumnWidth = 34.0;

        var viewportWidth = DrumScrollViewer.ViewportWidth;
        if (viewportWidth <= 0)
            return;

        var playheadCenter = instrumentColumnWidth + (step * stepColumnWidth) + (stepColumnWidth / 2.0);
        var positionInViewport = playheadCenter - DrumScrollViewer.HorizontalOffset;
        var followThreshold = viewportWidth * 0.80;

        if (positionInViewport < followThreshold)
            return;

        var targetOffset = playheadCenter - (viewportWidth * 0.25);
        targetOffset = Math.Clamp(targetOffset, 0, DrumScrollViewer.ScrollableWidth);
        DrumScrollViewer.ScrollToHorizontalOffset(targetOffset);
    }

    private void StopPlayhead()
    {
        _playheadTimer.Stop();
        _playheadStopwatch.Reset();
        _currentPlayheadStep = -1;

        if (_playheadMarker is not null)
        {
            EditorGrid.Children.Remove(_playheadMarker);
            _playheadMarker = null;
        }
    }

    private void DarkModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var dark = DarkModeCheckBox.IsChecked == true;
        static SolidColorBrush Brush(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        Resources["WindowBackgroundBrush"] = Brush(dark ? "#FF11151C" : "#FFF4F6FA");
        Resources["WindowForegroundBrush"] = Brush(dark ? "#FFF1F4F8" : "#FF18202A");
        Resources["PanelBackgroundBrush"] = Brush(dark ? "#FF1A2029" : "#FFFFFFFF");
        Resources["ControlBackgroundBrush"] = Brush(dark ? "#FF242C37" : "#FFF9FAFC");
        Resources["ControlForegroundBrush"] = Brush(dark ? "#FFF1F4F8" : "#FF18202A");
        Resources["BorderBrushTheme"] = Brush(dark ? "#FF364152" : "#FFD6DCE5");
        Resources["SubtleBackgroundBrush"] = Brush(dark ? "#FF151B23" : "#FFF0F3F8");
        Resources["StatusForegroundBrush"] = Brush(dark ? "#FFAAB4C2" : "#FF667085");
        Resources["AccentBrush"] = Brush(dark ? "#FF7C86FF" : "#FF5865F2");
        Resources["AccentHoverBrush"] = Brush(dark ? "#FF929AFF" : "#FF4752D6");
        Resources["AccentForegroundBrush"] = Brush(dark ? "#FF10131A" : "#FFFFFFFF");
        Resources["ButtonHoverBrush"] = Brush(dark ? "#FF303A47" : "#FFEEF1F6");

        DrawEditor();
    }

    private void BuildKitOptions()
    {
        KitOptionsPanel.Children.Clear();
        var profile = GetSelectedOutputProfile();

        if (KitProfileHintTextBlock is not null)
        {
            KitProfileHintTextBlock.Text =
                $"The original nine pieces are selected by default. Available pieces and export notes currently follow: {profile.DisplayName}.";
        }

        foreach (var instrument in AvailableInstruments.Where(profile.Supports))
        {
            var midiNote = profile.MidiNotes[instrument];
            var checkBox = new CheckBox
            {
                Content = InstrumentDisplayName(instrument),
                Tag = instrument,
                IsChecked = _activeInstruments.Contains(instrument),
                Margin = new Thickness(0, 0, 18, 8),
                MinWidth = 105,
                ToolTip = $"{InstrumentDisplayName(instrument)} -> MIDI note {midiNote} when exporting to {profile.DisplayName}."
            };
            checkBox.Checked += KitInstrument_Changed;
            checkBox.Unchecked += KitInstrument_Changed;
            KitOptionsPanel.Children.Add(checkBox);
        }
    }

    private void KitInstrument_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: DrumInstrument instrument } checkBox)
            return;

        if (checkBox.IsChecked == true)
        {
            if (!_activeInstruments.Contains(instrument))
                _activeInstruments.Add(instrument);
        }
        else
        {
            _activeInstruments.Remove(instrument);
            _pattern.Hits.RemoveAll(h => h.Instrument == instrument);
            _generatedBaseline?.Hits.RemoveAll(h => h.Instrument == instrument);
        }

        SortActiveInstruments();

        if (IsLoaded)
        {
            StopPreview();
            DrawEditor();
            StatusTextBlock.Text = $"{_activeInstruments.Count} kit pieces active. New generations will use this kit.";
        }
    }

    private void OutputProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || OutputProfileComboBox is null)
            return;

        StopPreview();

        var profile = GetSelectedOutputProfile();
        var unsupported = _activeInstruments.Where(i => !profile.Supports(i)).ToList();

        foreach (var instrument in unsupported)
        {
            _activeInstruments.Remove(instrument);
            _pattern.Hits.RemoveAll(h => h.Instrument == instrument);
            _generatedBaseline?.Hits.RemoveAll(h => h.Instrument == instrument);
        }

        SortActiveInstruments();
        BuildKitOptions();
        DrawEditor();

        StatusTextBlock.Text =
            $"Export target set to {profile.DisplayName}. Preview remains General MIDI; exported notes use the selected target map.";
    }

    private void CustomizeKitButton_Click(object sender, RoutedEventArgs e)
    {
        var opening = KitSettingsBorder.Visibility != Visibility.Visible;
        KitSettingsBorder.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        CustomizeKitButton.Content = opening ? "Hide Kit" : "Customize Kit";
    }

    private void ResetKitButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
        var profile = GetSelectedOutputProfile();
        _activeInstruments.Clear();
        _activeInstruments.AddRange(DefaultInstruments.Where(profile.Supports));

        _pattern.Hits.RemoveAll(h => !_activeInstruments.Contains(h.Instrument));
        _generatedBaseline?.Hits.RemoveAll(h => !_activeInstruments.Contains(h.Instrument));

        BuildKitOptions();
        DrawEditor();
        StatusTextBlock.Text = "Restored the default MIDILab drum kit.";
    }

    private void SortActiveInstruments()
    {
        _activeInstruments.Sort((left, right) =>
            Array.IndexOf(AvailableInstruments, left).CompareTo(Array.IndexOf(AvailableInstruments, right)));
    }

    private static string InstrumentDisplayName(DrumInstrument instrument) => instrument switch
    {
        DrumInstrument.Crash2 => "Crash 2",
        DrumInstrument.OpenHiHat => "Open Hi-Hat",
        DrumInstrument.ClosedHiHat => "Closed Hi-Hat",
        DrumInstrument.PedalHiHat => "Pedal Hi-Hat",
        DrumInstrument.HandClap => "Hand Clap",
        DrumInstrument.HighTom => "High Tom",
        DrumInstrument.MidTom => "Mid Tom",
        DrumInstrument.LowTom => "Low Tom",
        DrumInstrument.FloorTom => "Floor Tom",
        _ => instrument.ToString()
    };

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var opening = SettingsGuideBorder.Visibility != Visibility.Visible;
        SettingsGuideBorder.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        HelpButton.Content = opening ? "Hide Settings Guide" : "? Settings Guide";
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pattern.Hits.Count == 0)
        {
            MessageBox.Show("Generate or add some drum hits first.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var outputProfile = GetSelectedOutputProfile();
        var safeProfileName = outputProfile.ShortName.Replace(".", string.Empty);

        var dialog = new SaveFileDialog
        {
            Filter = "MIDI file (*.mid)|*.mid",
            DefaultExt = ".mid",
            AddExtension = true,
            FileName = $"MIDILab_{_pattern.Bpm}bpm_{safeProfileName}.mid"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _midiExporter.Export(_pattern, dialog.FileName, outputProfile);
        StatusTextBlock.Text =
            $"Exported {System.IO.Path.GetFileName(dialog.FileName)} for {outputProfile.DisplayName}. Drag it into REAPER.";
    }

    private void DrawEditor()
    {
        _playheadMarker = null;
        _currentPlayheadStep = -1;
        EditorGrid.Children.Clear();
        EditorGrid.RowDefinitions.Clear();
        EditorGrid.ColumnDefinitions.Clear();

        EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        for (var step = 0; step < _pattern.TotalSteps; step++)
            EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        EditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        foreach (var _ in _activeInstruments)
            EditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });

        AddHeaderCell("Kit", 0);
        for (var step = 0; step < _pattern.TotalSteps; step++)
        {
            var local = step % _pattern.StepsPerBar;
            var subdivision = GetStepLabel(local, _pattern.Meter);
            AddHeaderCell(subdivision, step + 1, local == 0, _pattern.Meter.GroupStartSteps.Contains(local));
        }

        for (var row = 0; row < _activeInstruments.Count; row++)
        {
            var instrument = _activeInstruments[row];
            AddInstrumentLabel(instrument, row + 1);

            for (var step = 0; step < _pattern.TotalSteps; step++)
                AddStepButton(instrument, step, row + 1);
        }
    }

    private void AddHeaderCell(string text, int column, bool startsBar = false, bool startsGroup = false)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(startsBar ? 2.5 : startsGroup ? 1.5 : 0.5, 0.5, 0.5, 0.5),
            BorderBrush = Brushes.DimGray,
            Child = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = startsBar ? FontWeights.Bold : FontWeights.Normal
            }
        };

        Grid.SetRow(border, 0);
        Grid.SetColumn(border, column);
        EditorGrid.Children.Add(border);
    }

    private void AddInstrumentLabel(DrumInstrument instrument, int row)
    {
        var text = instrument switch
        {
            DrumInstrument.OpenHiHat => "Open HH",
            DrumInstrument.ClosedHiHat => "Closed HH",
            DrumInstrument.PedalHiHat => "Pedal HH",
            DrumInstrument.Crash2 => "Crash 2",
            DrumInstrument.HandClap => "Hand Clap",
            DrumInstrument.HighTom => "High Tom",
            DrumInstrument.MidTom => "Mid Tom",
            DrumInstrument.LowTom => "Low Tom",
            DrumInstrument.FloorTom => "Floor Tom",
            _ => instrument.ToString()
        };

        var border = new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(8, 0, 6, 0),
            Child = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);
        EditorGrid.Children.Add(border);
    }

    private void AddStepButton(DrumInstrument instrument, int step, int row)
    {
        var hit = _pattern.FindHit(instrument, step);
        var localStep = step % _pattern.StepsPerBar;
        var startsBeat = localStep % _pattern.Meter.StepsPerDenominatorBeat == 0;
        var startsGroup = _pattern.Meter.GroupStartSteps.Contains(localStep);
        var startsBar = localStep == 0;

        var button = new Button
        {
            Tag = new CellTag(instrument, step),
            Margin = new Thickness(startsBar ? 2 : startsGroup ? 1.5 : startsBeat ? 1 : 0.5),
            Padding = new Thickness(0),
            Content = hit is null ? string.Empty : "●",
            FontSize = hit is null ? 12 : 18,
            ToolTip = hit is null
                ? $"{instrument}, step {step + 1}: empty"
                : $"{instrument}, step {step + 1}: velocity {hit.Velocity}, timing {hit.TimingOffsetTicks:+#;-#;0} ticks"
        };

        if (hit is not null)
            button.FontWeight = FontWeights.Bold;

        button.Click += StepButton_Click;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, step + 1);
        EditorGrid.Children.Add(button);
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CellTag tag })
            return;

        StopPreview();
        _pattern.ToggleHit(tag.Instrument, tag.Step, DefaultVelocity(tag.Instrument));
        DrawEditor();
        ResetButton.IsEnabled = _generatedBaseline is not null;
        StatusTextBlock.Text = "Pattern edited. Reset Edits restores the generated version; New Variation starts another idea.";
    }

    private DrumOutputProfile GetSelectedOutputProfile() =>
        OutputProfileComboBox.SelectedIndex == 1
            ? DrumOutputProfiles.StevenSlateDrums55Factory
            : DrumOutputProfiles.GeneralMidi;

    private TimeSignature GetSelectedTimeSignature()
    {
        var signature = TimeSignatureComboBox.SelectedItem is ComboBoxItem { Content: string value }
            ? value
            : "4/4";
        var grouping = GroupingComboBox.SelectedItem is ComboBoxItem { Content: string groupValue }
            ? groupValue
            : null;
        return TimeSignature.FromSelection(signature, grouping);
    }

    private void TimeSignatureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || GroupingComboBox is null)
            return;

        var signature = TimeSignatureComboBox.SelectedItem is ComboBoxItem { Content: string value } ? value : "4/4";
        GroupingComboBox.Items.Clear();

        if (signature == "5/4")
        {
            GroupingComboBox.Items.Add(new ComboBoxItem { Content = "3+2" });
            GroupingComboBox.Items.Add(new ComboBoxItem { Content = "2+3" });
            GroupingComboBox.SelectedIndex = 0;
            GroupingPanel.Visibility = Visibility.Visible;
        }
        else if (signature == "7/8")
        {
            GroupingComboBox.Items.Add(new ComboBoxItem { Content = "2+2+3" });
            GroupingComboBox.Items.Add(new ComboBoxItem { Content = "2+3+2" });
            GroupingComboBox.Items.Add(new ComboBoxItem { Content = "3+2+2" });
            GroupingComboBox.SelectedIndex = 0;
            GroupingPanel.Visibility = Visibility.Visible;
        }
        else
        {
            GroupingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private static string GetStepLabel(int localStep, TimeSignature meter)
    {
        var stepsPerBeat = meter.StepsPerDenominatorBeat;
        var beat = localStep / stepsPerBeat + 1;
        var sub = localStep % stepsPerBeat;

        if (stepsPerBeat == 4)
        {
            return sub switch
            {
                0 => beat.ToString(),
                1 => "e",
                2 => "&",
                _ => "a"
            };
        }

        if (stepsPerBeat == 2)
            return sub == 0 ? beat.ToString() : "&";

        return sub == 0 ? beat.ToString() : "·";
    }

    private static string FormatGrouping(TimeSignature meter) =>
        meter.Grouping == meter.Numerator.ToString() ? string.Empty : $"({meter.Grouping})";

    private GrooveSection GetSelectedSection() => SectionComboBox.SelectedIndex switch
    {
        1 => GrooveSection.Intro,
        2 => GrooveSection.Verse,
        3 => GrooveSection.PreChorus,
        4 => GrooveSection.Chorus,
        5 => GrooveSection.Bridge,
        6 => GrooveSection.Breakdown,
        7 => GrooveSection.Outro,
        _ => GrooveSection.General
    };

    private static string FormatSection(GrooveSection section) => section == GrooveSection.PreChorus
        ? "Pre-Chorus"
        : section.ToString();

    private int GetSelectedBars()
    {
        if (BarsComboBox.SelectedItem is ComboBoxItem { Content: string value } && int.TryParse(value, out var bars))
            return bars;

        return 4;
    }

    private GrooveGenre GetSelectedGenre()
    {
        if (GenreComboBox.SelectedItem is ComboBoxItem { Content: string value } &&
            Enum.TryParse<GrooveGenre>(value, ignoreCase: true, out var genre))
        {
            return genre;
        }

        return GrooveGenre.Rock;
    }

    private static int DefaultVelocity(DrumInstrument instrument) => instrument switch
    {
        DrumInstrument.Snare => 108,
        DrumInstrument.Kick => 105,
        DrumInstrument.Crash => 112,
        DrumInstrument.Crash2 => 110,
        DrumInstrument.China => 112,
        DrumInstrument.Splash => 92,
        DrumInstrument.OpenHiHat => 90,
        DrumInstrument.ClosedHiHat => 78,
        DrumInstrument.PedalHiHat => 72,
        DrumInstrument.Tambourine => 78,
        DrumInstrument.Cowbell => 84,
        DrumInstrument.HandClap => 92,
        DrumInstrument.Rimshot => 72,
        _ => 95
    };

    protected override void OnClosed(EventArgs e)
    {
        _previewPlayer.Dispose();
        base.OnClosed(e);
    }

    private sealed record CellTag(DrumInstrument Instrument, int Step);
}
