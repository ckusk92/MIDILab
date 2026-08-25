using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MIDILab.Models;

namespace MIDILab.Services;

public sealed class MidiExporter
{
    private const int TicksPerQuarterNote = 480;
    private const int TicksPerSixteenth = TicksPerQuarterNote / 4;
    private const int DrumChannel = 9; // MIDI channel 10, zero-based.

    public void Export(DrumPattern pattern, string filePath, DrumOutputProfile outputProfile)
    {
        using var file = File.Create(filePath);
        using var writer = new BinaryWriter(file);

        WriteAscii(writer, "MThd");
        WriteBigEndian(writer, 6);
        WriteBigEndian(writer, (short)0); // MIDI format 0.
        WriteBigEndian(writer, (short)1); // One track.
        WriteBigEndian(writer, (short)TicksPerQuarterNote);

        using var trackStream = new MemoryStream();
        using (var trackWriter = new BinaryWriter(trackStream, Encoding.ASCII, leaveOpen: true))
        {
            WriteTempo(trackWriter, pattern.Bpm);
            WriteTimeSignature(trackWriter, pattern.Meter);
            WriteNotes(trackWriter, pattern, outputProfile);
            WriteVariableLength(trackWriter, 0);
            trackWriter.Write((byte)0xFF);
            trackWriter.Write((byte)0x2F);
            trackWriter.Write((byte)0x00);
        }

        WriteAscii(writer, "MTrk");
        WriteBigEndian(writer, checked((int)trackStream.Length));
        trackStream.Position = 0;
        trackStream.CopyTo(file);
    }

    private static void WriteNotes(BinaryWriter writer, DrumPattern pattern, DrumOutputProfile outputProfile)
    {
        var events = new List<MidiEvent>();

        foreach (var hit in pattern.Hits)
        {
            if (!outputProfile.MidiNotes.TryGetValue(hit.Instrument, out var note))
                continue;

            var baseTick = hit.Step * TicksPerSixteenth;
            var startTick = Math.Max(0, baseTick + hit.TimingOffsetTicks);
            var endTick = startTick + 60;

            events.Add(new MidiEvent(startTick, true, note, (byte)Math.Clamp(hit.Velocity, 1, 127)));
            events.Add(new MidiEvent(endTick, false, note, 0));
        }

        var ordered = events
            .OrderBy(e => e.Tick)
            .ThenBy(e => e.IsNoteOn ? 1 : 0) // Note-offs first if timestamps collide.
            .ToList();

        var previousTick = 0;
        foreach (var midiEvent in ordered)
        {
            var delta = midiEvent.Tick - previousTick;
            WriteVariableLength(writer, delta);

            writer.Write((byte)((midiEvent.IsNoteOn ? 0x90 : 0x80) | DrumChannel));
            writer.Write(midiEvent.Note);
            writer.Write(midiEvent.Velocity);

            previousTick = midiEvent.Tick;
        }
    }

    private static void WriteTempo(BinaryWriter writer, int bpm)
    {
        var microsecondsPerQuarter = 60_000_000 / Math.Clamp(bpm, 20, 400);

        WriteVariableLength(writer, 0);
        writer.Write((byte)0xFF);
        writer.Write((byte)0x51);
        writer.Write((byte)0x03);
        writer.Write((byte)((microsecondsPerQuarter >> 16) & 0xFF));
        writer.Write((byte)((microsecondsPerQuarter >> 8) & 0xFF));
        writer.Write((byte)(microsecondsPerQuarter & 0xFF));
    }

    private static void WriteTimeSignature(BinaryWriter writer, TimeSignature timeSignature)
    {
        WriteVariableLength(writer, 0);
        writer.Write((byte)0xFF);
        writer.Write((byte)0x58);
        writer.Write((byte)0x04);
        writer.Write((byte)timeSignature.Numerator);

        var denominatorPower = timeSignature.Denominator switch
        {
            1 => 0,
            2 => 1,
            4 => 2,
            8 => 3,
            16 => 4,
            _ => 2
        };
        writer.Write((byte)denominatorPower);

        // 24 MIDI clocks per quarter note. For compound /8 meters, a dotted-quarter
        // metronome pulse is musically useful, so advertise 36 clocks per click.
        writer.Write((byte)(timeSignature.Denominator == 8 && timeSignature.GroupSizes.All(g => g == 3) ? 36 : 24));
        writer.Write((byte)0x08);
    }

    private static void WriteVariableLength(BinaryWriter writer, int value)
    {
        uint buffer = (uint)(value & 0x7F);
        var remaining = value >> 7;

        while (remaining > 0)
        {
            buffer <<= 8;
            buffer |= (uint)((remaining & 0x7F) | 0x80);
            remaining >>= 7;
        }

        while (true)
        {
            writer.Write((byte)(buffer & 0xFF));
            if ((buffer & 0x80) != 0)
                buffer >>= 8;
            else
                break;
        }
    }

    private static void WriteAscii(BinaryWriter writer, string text) =>
        writer.Write(Encoding.ASCII.GetBytes(text));

    private static void WriteBigEndian(BinaryWriter writer, short value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndian(BinaryWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private sealed record MidiEvent(int Tick, bool IsNoteOn, byte Note, byte Velocity);
}
