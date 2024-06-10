using NAudio.Midi;

namespace AlgoRhythmAnalyzer;

public static class Util
{
    public static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static void AnalyzeChord(List<int> midiNotes, out Interval chordType, out string baseKey)
    {
        chordType = Interval.Unknown;
        baseKey = "Unknown";

        if (midiNotes.Count < 3)
        {
            return;
        }

        // Normalize notes to a single octave
        List<int> normalizedNotes = midiNotes.Select(n => n % 12).Distinct().OrderBy(n => n).ToList();

        if (normalizedNotes.Count < 3)
        {
            return;
        }

        Dictionary<Interval, List<int>> chordPatterns = new Dictionary<Interval, List<int>>
        {
            { Interval.Major, [0, 4, 7] },
            { Interval.Minor, [0, 3, 7] },
            { Interval.Augmented, [0, 4, 8] },
            { Interval.Diminished, [0, 3, 6] }
        };

        Interval bestMatch = Interval.Unknown;
        foreach (int rootNote in normalizedNotes)
        {
            foreach (KeyValuePair<Interval, List<int>> pattern in chordPatterns)
            {
                List<int> patternNotes = pattern.Value.Select(n => (n + rootNote) % 12).OrderBy(n => n).ToList();

                // find the pattern for the sequence of notes that is most similar
                int matchCount = 0;
                for (int i = 0; i < normalizedNotes.Count; i++)
                {
                    if (patternNotes.Contains(normalizedNotes[i]))
                    {
                        matchCount++;
                    }
                }

                if (matchCount == normalizedNotes.Count)
                {
                    bestMatch = pattern.Key;
                    break;
                }
            }
        }

        if (bestMatch != Interval.Unknown)
        {
            chordType = bestMatch;
            baseKey = NoteNames[normalizedNotes[0]];
        }
    }

    public static int CalculateSimilarity(List<NoteOnEvent> set1, List<NoteOnEvent> set2, long measure1Start, long measure2Start)
    {
        int score = 0;
        HashSet<int> matchedIndices = [];

        foreach (NoteOnEvent event1 in set1)
        {
            if(event1.OffEvent == null) continue;
            
            long event1Start = event1.AbsoluteTime - measure1Start;
            long event1End = event1.OffEvent.AbsoluteTime - measure1Start;
            bool matchFound = false;
            for (int i = 0; i < set2.Count; i++)
            {
                if (matchedIndices.Contains(i)) continue;
                
                NoteOnEvent event2 = set2[i];
                
                if(event2.OffEvent == null) continue;
                
                long event2Start = event2.AbsoluteTime - measure2Start;
                long event2End = event2.OffEvent.AbsoluteTime - measure2Start;
                if (event1Start == event2Start && event1End == event2End)
                {
                    int eventLength = (int) (event1End - event1Start);
                    score += eventLength;
                    matchedIndices.Add(i);
                    matchFound = true;
                    break;
                }
            }

            if (!matchFound)
            {
                int eventLength = (int)(event1End - event1Start);
                score -= eventLength;
            }
        }

        return score;
    }

    public static List<Tuple<long, long>> ExtractMeasures(MidiFile midiFile)
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

    public static List<NoteOnEvent> NotesInAMeasure(List<NoteOnEvent> allNotes, long startTick, long endTick)
    {
        return allNotes.Where(n => n.AbsoluteTime >= startTick && n.AbsoluteTime < endTick).ToList();
    }
}