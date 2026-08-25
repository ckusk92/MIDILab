using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using MIDILab.Models;
using MIDILab.Services;
using Microsoft.Win32;

namespace MIDILab;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions ProjectJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly IReadOnlyDictionary<string, ThemePalette> ThemePalettes =
        new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["Indigo"] = new("#FF5865F2", "#FF4752D6", "#FFEEF0FF", "#FFF0F2FF", "#FF8992FF", "#FFA0A7FF", "#FF292E50", "#FF252A45"),
            ["Ocean"] = new("#FF1677E8", "#FF0F61C1", "#FFEAF4FF", "#FFEDF6FF", "#FF72B4FF", "#FF93C6FF", "#FF173653", "#FF18324A"),
            ["Emerald"] = new("#FF16865B", "#FF116B49", "#FFE9F7F0", "#FFECF8F2", "#FF59CC96", "#FF7ADBAF", "#FF173A2D", "#FF183329"),
            ["Amber"] = new("#FFC87500", "#FFA45F00", "#FFFFF4DF", "#FFFFF7E8", "#FFF2AA45", "#FFFFC36F", "#FF49331A", "#FF3C2D1B"),
            ["Rose"] = new("#FFD94C76", "#FFB93A62", "#FFFDEDF2", "#FFFFF0F4", "#FFF07B9D", "#FFF49AB3", "#FF4B2634", "#FF40242F"),
            ["Slate"] = new("#FF596579", "#FF465163", "#FFEEF1F5", "#FFF1F3F6", "#FFA7B0C0", "#FFC0C7D2", "#FF303641", "#FF292F38")
        };
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
    private CellTag? _editingHit;
    private CellTag? _dragSourceHit;
    private Point _dragStartPoint;
    private bool _isDraggingHit;
    private bool _suppressClickAfterDrag;
    private bool _isLoadingProject;

    private const string DrumHitDragFormat = "MIDILab.DrumHit";

    public MainWindow()
    {
        InitializeComponent();

        _playheadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _playheadTimer.Tick += PlayheadTimer_Tick;
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;

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
        var accent = ((SolidColorBrush)FindResource("AccentBrush")).Color;
        _playheadMarker = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, accent.R, accent.G, accent.B)),
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
        if (_isLoadingProject || !IsLoaded)
            return;

        ApplyTheme();
        StatusTextBlock.Text = $"Appearance changed to {(DarkModeCheckBox.IsChecked == true ? "dark" : "light")} {GetSelectedThemeName()} theme.";
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingProject || !IsLoaded)
            return;

        ApplyTheme();
        StatusTextBlock.Text = $"Theme changed to {GetSelectedThemeName()}.";
    }

    private void ApplyTheme()
    {
        var dark = DarkModeCheckBox.IsChecked == true;
        var themeName = GetSelectedThemeName();
        if (!ThemePalettes.TryGetValue(themeName, out var palette))
            palette = ThemePalettes["Indigo"];

        static SolidColorBrush Brush(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        Resources["WindowBackgroundBrush"] = Brush(dark ? "#FF11151C" : "#FFF2F5FA");
        Resources["WindowForegroundBrush"] = Brush(dark ? "#FFF1F4F8" : "#FF1D2433");
        Resources["PanelBackgroundBrush"] = Brush(dark ? "#FF1A2029" : "#FFFFFFFF");
        Resources["ControlBackgroundBrush"] = Brush(dark ? "#FF242C37" : "#FFFBFCFE");
        Resources["ControlForegroundBrush"] = Brush(dark ? "#FFF1F4F8" : "#FF1D2433");
        Resources["BorderBrushTheme"] = Brush(dark ? "#FF364152" : "#FFD8DEE9");
        Resources["SubtleBackgroundBrush"] = Brush(dark ? "#FF151B23" : "#FFF7F9FC");
        Resources["StatusForegroundBrush"] = Brush(dark ? "#FFAAB4C2" : "#FF667085");
        Resources["AccentBrush"] = Brush(dark ? palette.DarkAccent : palette.LightAccent);
        Resources["AccentHoverBrush"] = Brush(dark ? palette.DarkHover : palette.LightHover);
        Resources["AccentForegroundBrush"] = Brush(dark ? "#FF10131A" : "#FFFFFFFF");
        Resources["AccentSoftBrush"] = Brush(dark ? palette.DarkSoft : palette.LightSoft);
        Resources["ButtonHoverBrush"] = Brush(dark ? "#FF303A47" : "#FFF0F3F8");
        Resources["EditorHeaderBrush"] = Brush(dark ? palette.DarkHeader : palette.LightHeader);
        Resources["EditorLabelBrush"] = Brush(dark ? "#FF202733" : "#FFF7F8FC");
        Resources["EditorGridLineBrush"] = Brush(dark ? "#FF364152" : "#FFDDE2EA");
        Resources["EditorStrongLineBrush"] = Brush(dark ? "#FF667085" : "#FFA7B0C0");

        DrawEditor();
    }

    private string GetSelectedThemeName() =>
        ThemeComboBox.SelectedItem is ComboBoxItem { Content: string theme } && ThemePalettes.ContainsKey(theme)
            ? theme
            : "Indigo";

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
        if (_isLoadingProject || !IsLoaded || OutputProfileComboBox is null)
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

    private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(BpmTextBox.Text, out var bpm) || bpm is < 20 or > 400)
        {
            MessageBox.Show("Enter a BPM between 20 and 400 before saving the project.", "Invalid BPM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "MIDILab project (*.midilab)|*.midilab",
            DefaultExt = ".midilab",
            AddExtension = true,
            FileName = $"MIDILab_{bpm}bpm.midilab"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var project = CaptureProject(bpm);
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(project, ProjectJsonOptions));
            StatusTextBlock.Text = $"Saved project {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MIDILab could not save this project.\n\n{ex.Message}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIDILab project (*.midilab)|*.midilab|All files (*.*)|*.*",
            DefaultExt = ".midilab",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var project = JsonSerializer.Deserialize<MIDILabProjectFile>(json, ProjectJsonOptions)
                ?? throw new InvalidDataException("The project file is empty or invalid.");

            if (project.Version != 1)
                throw new InvalidDataException($"This MIDILab version cannot open project format version {project.Version}.");

            ApplyProject(project);
            StatusTextBlock.Text = $"Opened project {Path.GetFileName(dialog.FileName)} with {project.Pattern.Hits.Count} hits.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MIDILab could not open this project.\n\n{ex.Message}", "Open Project", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private MIDILabProjectFile CaptureProject(int bpm)
    {
        var selectedMeter = GetSelectedTimeSignature();
        return new MIDILabProjectFile
        {
            Settings = new MIDILabProjectSettings
            {
                Bpm = bpm,
                Bars = GetSelectedBars(),
                TimeSignatureNumerator = selectedMeter.Numerator,
                TimeSignatureDenominator = selectedMeter.Denominator,
                TimeSignatureGrouping = selectedMeter.Grouping,
                Genre = GetSelectedGenre(),
                Section = GetSelectedSection(),
                Energy = EnergySlider.Value,
                Humanize = HumanizeSlider.Value,
                OutputProfile = GetSelectedOutputProfile().Id,
                ActiveInstruments = [.. _activeInstruments],
                Theme = GetSelectedThemeName(),
                DarkMode = DarkModeCheckBox.IsChecked == true
            },
            Pattern = ToProjectPattern(_pattern),
            GeneratedBaseline = _generatedBaseline is null ? null : ToProjectPattern(_generatedBaseline)
        };
    }

    private void ApplyProject(MIDILabProjectFile project)
    {
        StopPreview();
        HideHitEditor();
        _isLoadingProject = true;

        try
        {
            var settings = project.Settings ?? throw new InvalidDataException("The project is missing settings.");
            BpmTextBox.Text = Math.Clamp(settings.Bpm, 20, 400).ToString();
            SetComboBoxByContent(BarsComboBox, settings.Bars.ToString(), "4");
            SetComboBoxByContent(GenreComboBox, settings.Genre.ToString(), "Rock");
            SetComboBoxByContent(SectionComboBox, FormatSection(settings.Section), "General");
            EnergySlider.Value = Math.Clamp(settings.Energy, 0, 100);
            HumanizeSlider.Value = Math.Clamp(settings.Humanize, 0, 100);
            OutputProfileComboBox.SelectedIndex = settings.OutputProfile == DrumOutputProfileId.StevenSlateDrums55Factory ? 1 : 0;

            var signature = $"{settings.TimeSignatureNumerator}/{settings.TimeSignatureDenominator}";
            SetComboBoxByContent(TimeSignatureComboBox, signature, "4/4");
            ConfigureGroupingOptions(signature);
            if (GroupingPanel.Visibility == Visibility.Visible)
                SetComboBoxByContent(GroupingComboBox, settings.TimeSignatureGrouping, GroupingComboBox.Items.Count > 0 ? ((ComboBoxItem)GroupingComboBox.Items[0]).Content?.ToString() ?? string.Empty : string.Empty);

            var themeName = !string.IsNullOrWhiteSpace(settings.Theme) && ThemePalettes.ContainsKey(settings.Theme)
                ? settings.Theme
                : "Indigo";
            SetComboBoxByContent(ThemeComboBox, themeName, "Indigo");
            DarkModeCheckBox.IsChecked = settings.DarkMode;

            var profile = DrumOutputProfiles.FromId(settings.OutputProfile);
            _activeInstruments.Clear();
            foreach (var instrument in (settings.ActiveInstruments ?? []).Where(i => AvailableInstruments.Contains(i) && profile.Supports(i)).Distinct())
                _activeInstruments.Add(instrument);
            if (_activeInstruments.Count == 0)
                _activeInstruments.AddRange(DefaultInstruments.Where(profile.Supports));
            SortActiveInstruments();

            _pattern = FromProjectPattern(project.Pattern ?? throw new InvalidDataException("The project is missing a drum pattern."));
            _generatedBaseline = project.GeneratedBaseline is null ? null : FromProjectPattern(project.GeneratedBaseline);
            _pattern.Hits.RemoveAll(h => !_activeInstruments.Contains(h.Instrument) || !profile.Supports(h.Instrument));
            _generatedBaseline?.Hits.RemoveAll(h => !_activeInstruments.Contains(h.Instrument) || !profile.Supports(h.Instrument));
        }
        finally
        {
            _isLoadingProject = false;
        }

        ApplyTheme();
        BuildKitOptions();
        DrawEditor();
        VariationButton.IsEnabled = _pattern.Hits.Count > 0;
        ResetButton.IsEnabled = _generatedBaseline is not null && !PatternsEquivalent(_pattern, _generatedBaseline);
    }

    private static MIDILabProjectPattern ToProjectPattern(DrumPattern pattern) => new()
    {
        Bars = pattern.Bars,
        Bpm = pattern.Bpm,
        TimeSignatureNumerator = pattern.Meter.Numerator,
        TimeSignatureDenominator = pattern.Meter.Denominator,
        TimeSignatureGrouping = pattern.Meter.Grouping,
        Hits = pattern.Hits.Select(hit => new MIDILabProjectHit
        {
            Instrument = hit.Instrument,
            Step = hit.Step,
            Velocity = hit.Velocity,
            TimingOffsetTicks = hit.TimingOffsetTicks
        }).ToList()
    };

    private static DrumPattern FromProjectPattern(MIDILabProjectPattern source)
    {
        var signature = TimeSignature.FromSelection($"{source.TimeSignatureNumerator}/{source.TimeSignatureDenominator}", source.TimeSignatureGrouping);
        var pattern = new DrumPattern
        {
            Bars = Math.Clamp(source.Bars, 1, 64),
            Bpm = Math.Clamp(source.Bpm, 20, 400),
            Meter = signature
        };

        foreach (var hit in source.Hits ?? [])
        {
            if (hit.Step < 0 || hit.Step >= pattern.TotalSteps)
                continue;

            pattern.Hits.Add(new DrumHit
            {
                Instrument = hit.Instrument,
                Step = hit.Step,
                Velocity = Math.Clamp(hit.Velocity, 1, 127),
                TimingOffsetTicks = Math.Clamp(hit.TimingOffsetTicks, -120, 120)
            });
        }

        return pattern;
    }

    private static bool PatternsEquivalent(DrumPattern left, DrumPattern right)
    {
        if (left.Bars != right.Bars || left.Bpm != right.Bpm || left.Meter != right.Meter || left.Hits.Count != right.Hits.Count)
            return false;

        var leftHits = left.Hits.OrderBy(h => h.Instrument).ThenBy(h => h.Step).ToList();
        var rightHits = right.Hits.OrderBy(h => h.Instrument).ThenBy(h => h.Step).ToList();

        for (var index = 0; index < leftHits.Count; index++)
        {
            var leftHit = leftHits[index];
            var rightHit = rightHits[index];
            if (leftHit.Instrument != rightHit.Instrument ||
                leftHit.Step != rightHit.Step ||
                leftHit.Velocity != rightHit.Velocity ||
                leftHit.TimingOffsetTicks != rightHit.TimingOffsetTicks)
            {
                return false;
            }
        }

        return true;
    }

    private static void SetComboBoxByContent(ComboBox comboBox, string desired, string fallback)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), fallback, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
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
        HideHitEditor();
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
        var isKitHeader = column == 0;
        var border = new Border
        {
            Margin = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Background = isKitHeader
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("EditorHeaderBrush"),
            BorderThickness = new Thickness(startsBar ? 2 : startsGroup ? 1.25 : 1),
            BorderBrush = startsBar
                ? (Brush)FindResource("AccentBrush")
                : startsGroup
                    ? (Brush)FindResource("EditorStrongLineBrush")
                    : (Brush)FindResource("EditorGridLineBrush"),
            Child = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isKitHeader
                    ? (Brush)FindResource("AccentForegroundBrush")
                    : (Brush)FindResource("WindowForegroundBrush"),
                FontWeight = isKitHeader || startsBar ? FontWeights.SemiBold : FontWeights.Normal
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
            Margin = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)FindResource("EditorLabelBrush"),
            BorderBrush = (Brush)FindResource("EditorGridLineBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 0, 7, 0),
            Child = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Medium
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
            AllowDrop = true,
            ToolTip = hit is null
                ? $"{instrument}, step {step + 1}: empty"
                : $"{instrument}, step {step + 1}: velocity {hit.Velocity}, timing {hit.TimingOffsetTicks:+#;-#;0} ticks • drag to move • right-click to edit"
        };

        if (hit is not null)
        {
            button.FontWeight = FontWeights.Bold;
            button.Background = GetVelocityPadBrush(hit.Velocity);
            button.Foreground = GetVelocityPadForeground(hit.Velocity);
            button.BorderBrush = (Brush)FindResource("AccentBrush");
        }
        else
        {
            button.BorderBrush = (Brush)FindResource("EditorGridLineBrush");
        }

        button.Click += StepButton_Click;
        button.PreviewMouseLeftButtonDown += StepButton_PreviewMouseLeftButtonDown;
        button.PreviewMouseMove += StepButton_PreviewMouseMove;
        button.PreviewMouseRightButtonUp += StepButton_RightClick;
        button.DragOver += StepButton_DragOver;
        button.Drop += StepButton_Drop;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, step + 1);
        EditorGrid.Children.Add(button);
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressClickAfterDrag)
            return;

        if (sender is not Button { Tag: CellTag tag })
            return;

        StopPreview();
        _pattern.ToggleHit(tag.Instrument, tag.Step, DefaultVelocity(tag.Instrument));
        DrawEditor();
        ResetButton.IsEnabled = _generatedBaseline is not null;
        StatusTextBlock.Text = "Pattern edited. Reset Edits restores the generated version; New Variation starts another idea.";
    }

    private void StepButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Tag: CellTag tag })
            return;

        _dragStartPoint = e.GetPosition(this);
        _dragSourceHit = _pattern.FindHit(tag.Instrument, tag.Step) is null ? null : tag;
    }

    private void StepButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHit || e.LeftButton != MouseButtonState.Pressed || _dragSourceHit is not CellTag source)
            return;

        var currentPoint = e.GetPosition(this);
        var movedFarEnough =
            Math.Abs(currentPoint.X - _dragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPoint.Y - _dragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough || _pattern.FindHit(source.Instrument, source.Step) is null)
            return;

        StopPreview();
        HideHitEditor();
        _isDraggingHit = true;

        try
        {
            var data = new DataObject(DrumHitDragFormat, source);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            _isDraggingHit = false;
            _dragSourceHit = null;
            _suppressClickAfterDrag = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _suppressClickAfterDrag = false));
        }
    }

    private void StepButton_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = CanDropHit(sender, e.Data) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private bool CanDropHit(object sender, IDataObject data)
    {
        if (sender is not Button { Tag: CellTag target } || !data.GetDataPresent(DrumHitDragFormat))
            return false;

        if (data.GetData(DrumHitDragFormat) is not CellTag source)
            return false;

        if (source == target)
            return true;

        return _pattern.FindHit(source.Instrument, source.Step) is not null &&
               _pattern.FindHit(target.Instrument, target.Step) is null;
    }

    private void StepButton_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button { Tag: CellTag target } ||
            !e.Data.GetDataPresent(DrumHitDragFormat) ||
            e.Data.GetData(DrumHitDragFormat) is not CellTag source)
            return;

        if (source == target)
            return;

        var sourceHit = _pattern.FindHit(source.Instrument, source.Step);
        if (sourceHit is null)
            return;

        if (_pattern.FindHit(target.Instrument, target.Step) is not null)
        {
            StatusTextBlock.Text = "That pad already contains a hit. Drag to an empty pad so no note is overwritten.";
            return;
        }

        var velocity = sourceHit.Velocity;
        var timingOffset = sourceHit.TimingOffsetTicks;
        _pattern.Hits.Remove(sourceHit);
        _pattern.Hits.Add(new DrumHit
        {
            Instrument = target.Instrument,
            Step = target.Step,
            Velocity = velocity,
            TimingOffsetTicks = timingOffset
        });

        DrawEditor();
        ResetButton.IsEnabled = _generatedBaseline is not null;

        var sourcePosition = FormatCellPosition(source);
        var targetPosition = FormatCellPosition(target);
        StatusTextBlock.Text =
            $"Moved {InstrumentDisplayName(source.Instrument)} {sourcePosition} to {InstrumentDisplayName(target.Instrument)} {targetPosition}. Velocity {velocity}, timing {timingOffset:+#;-#;0} ticks preserved.";
    }

    private string FormatCellPosition(CellTag tag)
    {
        var bar = tag.Step / _pattern.StepsPerBar + 1;
        var localStep = tag.Step % _pattern.StepsPerBar + 1;
        return $"(bar {bar}, step {localStep})";
    }

    private void StepButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Tag: CellTag tag })
            return;

        var hit = _pattern.FindHit(tag.Instrument, tag.Step);
        if (hit is null)
            return;

        e.Handled = true;
        StopPreview();
        _editingHit = tag;
        HitEditorTitleTextBlock.Text = InstrumentDisplayName(tag.Instrument);
        var bar = tag.Step / _pattern.StepsPerBar + 1;
        var localStep = tag.Step % _pattern.StepsPerBar;
        var millisecondsPerTick = 60_000.0 / _pattern.Bpm / 480.0;
        HitEditorPositionTextBlock.Text = $"Bar {bar}, step {localStep + 1} • 1 tick ≈ {millisecondsPerTick:0.0} ms";
        HitVelocitySlider.Value = hit.Velocity;
        VelocityValueTextBlock.Text = hit.Velocity.ToString();
        HitTimingTextBox.Text = hit.TimingOffsetTicks.ToString();
        HitEditorBorder.Visibility = Visibility.Visible;
    }

    private void HitVelocitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VelocityValueTextBlock is not null)
            VelocityValueTextBlock.Text = Math.Round(e.NewValue).ToString("0");
    }

    private void ApplyHitEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingHit is not CellTag tag)
            return;

        if (!int.TryParse(HitTimingTextBox.Text, out var timing) || timing is < -60 or > 60)
        {
            MessageBox.Show("Enter a timing offset between -60 and +60 MIDI ticks.", "Invalid timing offset", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var hit = _pattern.FindHit(tag.Instrument, tag.Step);
        if (hit is null)
        {
            HideHitEditor();
            return;
        }

        hit.Velocity = Math.Clamp((int)Math.Round(HitVelocitySlider.Value), 1, 127);
        hit.TimingOffsetTicks = timing;
        HideHitEditor();
        DrawEditor();
        ResetButton.IsEnabled = _generatedBaseline is not null;
        StatusTextBlock.Text = $"Updated {InstrumentDisplayName(tag.Instrument)}: velocity {hit.Velocity}, timing {hit.TimingOffsetTicks:+#;-#;0} ticks.";
    }

    private void RemoveHitEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingHit is not CellTag tag)
            return;

        var hit = _pattern.FindHit(tag.Instrument, tag.Step);
        if (hit is not null)
            _pattern.Hits.Remove(hit);

        HideHitEditor();
        DrawEditor();
        ResetButton.IsEnabled = _generatedBaseline is not null;
        StatusTextBlock.Text = $"Removed {InstrumentDisplayName(tag.Instrument)} hit.";
    }

    private void CancelHitEditButton_Click(object sender, RoutedEventArgs e) => HideHitEditor();

    private void HideHitEditor()
    {
        _editingHit = null;
        if (HitEditorBorder is not null)
            HitEditorBorder.Visibility = Visibility.Collapsed;
    }

    private Brush GetVelocityPadBrush(int velocity)
    {
        var soft = ((SolidColorBrush)FindResource("AccentSoftBrush")).Color;
        var accent = ((SolidColorBrush)FindResource("AccentBrush")).Color;
        var normalized = Math.Clamp((velocity - 1) / 126.0, 0.0, 1.0);
        var blendAmount = 0.12 + (normalized * 0.88);
        return new SolidColorBrush(BlendColors(soft, accent, blendAmount));
    }

    private Brush GetVelocityPadForeground(int velocity) =>
        velocity >= 78
            ? (Brush)FindResource("AccentForegroundBrush")
            : (Brush)FindResource("AccentBrush");

    private static Color BlendColors(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        byte Blend(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
        return Color.FromArgb(255, Blend(from.R, to.R), Blend(from.G, to.G), Blend(from.B, to.B));
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
        if (_isLoadingProject || !IsLoaded || GroupingComboBox is null)
            return;

        var signature = TimeSignatureComboBox.SelectedItem is ComboBoxItem { Content: string value } ? value : "4/4";
        ConfigureGroupingOptions(signature);
    }

    private void ConfigureGroupingOptions(string signature)
    {
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

    private sealed record ThemePalette(
        string LightAccent,
        string LightHover,
        string LightSoft,
        string LightHeader,
        string DarkAccent,
        string DarkHover,
        string DarkSoft,
        string DarkHeader);

    private sealed record CellTag(DrumInstrument Instrument, int Step);
}
