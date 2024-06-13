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

    public static int CalculateSimilarity(
        List<NoteOnEvent> set1, 
        List<NoteOnEvent> set2, 
        long measure1Start,
        long measure2Start)
    {
        int score = 0;
        HashSet<int> matchedIndices = [];

        foreach (NoteOnEvent event1 in set1)
        {
            if (event1.OffEvent == null) continue;

            int event1Start = (int)(event1.AbsoluteTime - measure1Start);
            int event1End = (int)(event1.OffEvent.AbsoluteTime - measure1Start);
            int event1Duration = event1End - event1Start;
            int margin = (int)Math.Round(event1Duration * 0.1f); // 10% margin of error for start and end times

            bool matchFound = false;
            for (int i = 0; i < set2.Count; i++)
            {
                if (matchedIndices.Contains(i)) continue;

                NoteOnEvent event2 = set2[i];

                if (event2.OffEvent == null) continue;

                long event2Start = (int)(event2.AbsoluteTime - measure2Start);
                long event2End = (int)(event2.OffEvent.AbsoluteTime - measure2Start);
                if (Math.Abs(event1Start - event2Start) < margin && Math.Abs(event1End - event2End) < margin)
                {
                    int eventLength = event1End - event1Start;
                    score += eventLength;
                    matchedIndices.Add(i);
                    matchFound = true;
                    break;
                }
            }

            if (!matchFound)
            {
                int eventLength = event1End - event1Start;
                score -= eventLength;
            }
        }

        return score;
    }

    public static List<NoteOnEvent> NotesInAMeasure(List<NoteOnEvent> allNotes, long startTick, long endTick)
    {
        return allNotes.Where(n => n.AbsoluteTime >= startTick && n.AbsoluteTime < endTick).ToList();
    }
}