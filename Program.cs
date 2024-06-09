using NAudio.Midi;

public class Program
{
    public static void Main(string[] args)
    {
        string midiPath = args[0];
        
        MidiFile midi = new(midiPath, false);

        List<NoteOnEvent> drumEvents = [];
        List<NoteOnEvent> bassEvents = [];
        Dictionary<string, List<NoteOnEvent>> otherEventsByInstrument = [];
        for (int trackIndex = 0; trackIndex < midi.Events.Count(); trackIndex++)
        {
            IList<MidiEvent> trackEvents = midi.Events[trackIndex];

            if (trackEvents.FirstOrDefault() is TextEvent textEvent)
            {
                if (textEvent.Text.Contains("drum", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent noteOnEvent)
                        {
                            drumEvents.Add(noteOnEvent);
                        }
                    }        
                }
                else if (textEvent.Text.Contains("bass", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (MidiEvent trackEvent in trackEvents)
                    {
                        if (trackEvent is NoteOnEvent noteOnEvent)
                        {
                            bassEvents.Add(noteOnEvent);
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
                        if (trackEvent is NoteOnEvent noteOnEvent)
                        {
                            value.Add(noteOnEvent);
                        }
                    }        
                }
            }
        }

        var x = 1;
    }
}