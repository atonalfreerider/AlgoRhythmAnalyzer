using NAudio.Midi;

namespace AlgoRhythmAnalyzer;

public class MidiReader(string midiFilePath)
{
    MidiFile midiFile = new(midiFilePath, false);

    public List<Tuple<long, long>> ExtractMeasures()
    {
        // Assume 4/4 time signature if not specified
        int numerator = 4;
        int denominator = 4;

        // Ticks per quarter note
        int ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;

        // List to hold start and end times of each measure in ticks
        List<Tuple<long, long>> measures = [];

        // Iterate through the MIDI events
        foreach (IList<MidiEvent>? track in midiFile.Events)
        {
            int currentMeasure = 0;
            int ticksPerMeasure = ticksPerQuarterNote * numerator * 4 / denominator;

            foreach (MidiEvent midiEvent in track)
            {
                if (midiEvent is TimeSignatureEvent ts)
                {
                    numerator = ts.Numerator;
                    denominator = (int)Math.Pow(2, ts.Denominator);
                    ticksPerMeasure = ticksPerQuarterNote * numerator * 4 / denominator;
                }

                // Check if we reached the end of the measure
                if (midiEvent.AbsoluteTime / ticksPerMeasure > currentMeasure)
                {
                    long measureStartTime = currentMeasure * ticksPerMeasure;
                    long measureEndTime = (currentMeasure + 1) * ticksPerMeasure;
                    measures.Add(new Tuple<long, long>(measureStartTime, measureEndTime));
                    currentMeasure++;
                }
            }
        }

        return measures;
    }

    public Tuple<List<NoteOnEvent>, Dictionary<string,List<NoteOnEvent>>> ReadDrumAndInst()
    {
        List<NoteOnEvent> drumEvents = [];
        Dictionary<string, List<NoteOnEvent>> otherEventsByInstrument = [];
        for (int trackIndex = 0; trackIndex < midiFile.Events.Count(); trackIndex++)
        {
            IList<MidiEvent> trackEvents = midiFile.Events[trackIndex];

            if (trackEvents.FirstOrDefault() is TextEvent textEvent)
            {
                if (textEvent.Text.Contains("drum", StringComparison.InvariantCultureIgnoreCase) ||
                    trackEvents.Any(x => x.Channel == 10))
                {
                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent { Velocity: > 0 } noteOnEvent)
                        {
                            drumEvents.Add(noteOnEvent);
                        }
                    }
                }
                else
                {
                    string instrument = textEvent.Text;
                    if (!otherEventsByInstrument.TryGetValue(instrument, out List<NoteOnEvent>? value))
                    {
                        value = [];
                        otherEventsByInstrument[instrument] = value;
                    }

                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent { Velocity: > 0 } noteOnEvent)
                        {
                            value.Add(noteOnEvent);
                        }
                    }
                }
            }
            else
            {
                if (trackEvents.Any(x => x.Channel == 10))
                {
                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent { Velocity: > 0 } noteOnEvent)

                        {
                            drumEvents.Add(noteOnEvent);
                        }
                    }
                }
                else
                {
                    string instrument = trackEvents.FirstOrDefault().Channel.ToString();
                    if (!otherEventsByInstrument.TryGetValue(instrument, out List<NoteOnEvent>? value))
                    {
                        value = [];
                        otherEventsByInstrument[instrument] = value;
                    }

                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent { Velocity: > 0 } noteOnEvent)
                        {
                            value.Add(noteOnEvent);
                        }
                    }
                }
            }
        }

        return new Tuple<List<NoteOnEvent>, Dictionary<string, List<NoteOnEvent>>>(drumEvents, otherEventsByInstrument);
    }
}