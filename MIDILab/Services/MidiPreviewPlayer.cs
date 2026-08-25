using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MIDILab.Models;

namespace MIDILab.Services;

public sealed class MidiPreviewPlayer : IDisposable
{
    private const int TicksPerQuarterNote = 480;
    private const int TicksPerSixteenth = TicksPerQuarterNote / 4;
    private const int DrumChannel = 9; // MIDI channel 10, zero-based.
    private const uint MidiMapper = 0xFFFFFFFF;

    private static readonly IReadOnlyDictionary<DrumInstrument, byte> MidiNotes =
        new Dictionary<DrumInstrument, byte>
        {
            [DrumInstrument.Kick] = 36,
            [DrumInstrument.Rimshot] = 37,
            [DrumInstrument.Snare] = 38,
            [DrumInstrument.HandClap] = 39,
            [DrumInstrument.FloorTom] = 43,
            [DrumInstrument.LowTom] = 41,
            [DrumInstrument.MidTom] = 45,
            [DrumInstrument.HighTom] = 48,
            [DrumInstrument.ClosedHiHat] = 42,
            [DrumInstrument.PedalHiHat] = 44,
            [DrumInstrument.OpenHiHat] = 46,
            [DrumInstrument.Crash] = 49,
            [DrumInstrument.Crash2] = 57,
            [DrumInstrument.Ride] = 51,
            [DrumInstrument.Tambourine] = 54,
            [DrumInstrument.Splash] = 55,
            [DrumInstrument.Cowbell] = 56,
            [DrumInstrument.China] = 52
        };

    private CancellationTokenSource? _playbackCts;
    private IntPtr _midiHandle;

    public bool IsPlaying => _playbackCts is not null;

    public async Task PlayAsync(DrumPattern sourcePattern)
    {
        Stop();

        if (sourcePattern.Hits.Count == 0)
            return;

        var pattern = sourcePattern.Clone();
        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;

        try
        {
            OpenMidiOutput();

            var millisecondsPerTick = 60_000.0 / pattern.Bpm / TicksPerQuarterNote;
            var events = pattern.Hits
                .Where(h => MidiNotes.ContainsKey(h.Instrument))
                .Select(h => new PreviewEvent(
                    Math.Max(0, h.Step * TicksPerSixteenth + h.TimingOffsetTicks) * millisecondsPerTick,
                    MidiNotes[h.Instrument],
                    (byte)Math.Clamp(h.Velocity, 1, 127)))
                .OrderBy(e => e.TimeMilliseconds)
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            foreach (var midiEvent in events)
            {
                token.ThrowIfCancellationRequested();

                var remaining = midiEvent.TimeMilliseconds - stopwatch.Elapsed.TotalMilliseconds;
                if (remaining > 1)
                    await Task.Delay(TimeSpan.FromMilliseconds(remaining), token);

                SendNoteOn(midiEvent.Note, midiEvent.Velocity);
            }

            var patternLengthMs = pattern.TotalSteps * TicksPerSixteenth * millisecondsPerTick;
            var tail = patternLengthMs - stopwatch.Elapsed.TotalMilliseconds + 120;
            if (tail > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(tail), token);
        }
        catch (OperationCanceledException)
        {
            // Stop() intentionally cancels playback.
        }
        finally
        {
            CloseMidiOutput();
            _playbackCts?.Dispose();
            _playbackCts = null;
        }
    }

    public void Stop()
    {
        _playbackCts?.Cancel();
        if (_midiHandle != IntPtr.Zero)
            midiOutReset(_midiHandle);
    }

    public void Dispose()
    {
        Stop();
        CloseMidiOutput();
        _playbackCts?.Dispose();
        _playbackCts = null;
    }

    private void OpenMidiOutput()
    {
        CloseMidiOutput();

        var result = midiOutOpen(out _midiHandle, MidiMapper, IntPtr.Zero, IntPtr.Zero, 0);
        if (result != 0)
        {
            _midiHandle = IntPtr.Zero;
            throw new InvalidOperationException(
                "Windows could not open a MIDI output device. Make sure a MIDI synth/output device is available.");
        }
    }

    private void CloseMidiOutput()
    {
        if (_midiHandle == IntPtr.Zero)
            return;

        midiOutReset(_midiHandle);
        midiOutClose(_midiHandle);
        _midiHandle = IntPtr.Zero;
    }

    private void SendNoteOn(byte note, byte velocity)
    {
        if (_midiHandle == IntPtr.Zero)
            return;

        var status = (byte)(0x90 | DrumChannel);
        var message = (uint)(status | (note << 8) | (velocity << 16));
        midiOutShortMsg(_midiHandle, message);
    }

    [DllImport("winmm.dll")]
    private static extern uint midiOutOpen(
        out IntPtr lphmo,
        uint uDeviceID,
        IntPtr dwCallback,
        IntPtr dwInstance,
        uint dwFlags);

    [DllImport("winmm.dll")]
    private static extern uint midiOutShortMsg(IntPtr hmo, uint dwMsg);

    [DllImport("winmm.dll")]
    private static extern uint midiOutReset(IntPtr hmo);

    [DllImport("winmm.dll")]
    private static extern uint midiOutClose(IntPtr hmo);

    private sealed record PreviewEvent(double TimeMilliseconds, byte Note, byte Velocity);
}
