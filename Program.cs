using AlgoRhythmAnalyzer;
using NAudio.Midi;
using Newtonsoft.Json;

public class Program
{
    class Pattern
    {
        public string TrackName;
        public int TrackNumber;
        public List<Tuple<int, int>> BaseTimingPattern;
        public List<int> MasterPattern;
        public List<List<int>> NoteVariationPatterns;
    }
    
    public static void Main(string[] args)
    {
        string midiPath = args[0];

        Console.WriteLine($"Reading {midiPath}");
        MidiReader midiReader = new MidiReader(midiPath);

        List<Tuple<long, long>> measures = midiReader.ExtractMeasures();

        (List<NoteOnEvent> drumEvents, Dictionary<string, List<NoteOnEvent>> otherEventsByInstrument) =
            midiReader.ReadDrumAndInst();

        List<List<int>> drumPatterns = GeneratePatterns(drumEvents, measures);

        Dictionary<string, List<List<int>>> otherPatternsByInstrument = [];
        foreach (KeyValuePair<string, List<NoteOnEvent>> entry in otherEventsByInstrument)
        {
            if (entry.Value.Count == 0) continue;

            List<List<int>> patterns = GeneratePatterns(entry.Value, measures);
            otherPatternsByInstrument[entry.Key] = patterns;
        }
        
        List<Pattern> finalDrumPatterns = FinalPatterns(drumPatterns, measures, drumEvents, "Drums", 10);
        
        Dictionary<string, List<Pattern>> finalOtherPatternsByInstrument = [];
        foreach (KeyValuePair<string,List<List<int>>> keyValuePair in otherPatternsByInstrument)
        {
            List<Pattern> finalPatterns = FinalPatterns(keyValuePair.Value, measures, otherEventsByInstrument[keyValuePair.Key], keyValuePair.Key, 0);
            finalOtherPatternsByInstrument[keyValuePair.Key] = finalPatterns;
        }
       

        // TODO determine overall chord progressions
        
        string parentDirectory = Path.GetDirectoryName(args[0]);

        string patternJson = JsonConvert.SerializeObject(finalDrumPatterns, Formatting.Indented);
        File.WriteAllText($"{parentDirectory}/drum-patterns.json", patternJson);
        
        string otherPatternJson = JsonConvert.SerializeObject(finalOtherPatternsByInstrument, Formatting.Indented);
        File.WriteAllText($"{parentDirectory}/other-patterns.json", otherPatternJson);

        Console.WriteLine($"Wrote to {parentDirectory}");
    }

    static List<List<int>> GeneratePatterns(List<NoteOnEvent> events, List<Tuple<long, long>> measures)
    {
        // gather each note sequence by measure, by determining if the note start time is within the measure start and end times
        List<List<NoteOnEvent>> noteSequenceByMeasure = [];
        foreach (Tuple<long,long> measure in measures)
        {
            List<NoteOnEvent> notesInMeasure = Util.NotesInAMeasure(events, measure.Item1, measure.Item2);
            noteSequenceByMeasure.Add(notesInMeasure);
        }

        // for each note sequence, compare it to all other note sequences
        // rank the similarity of each sequence, based on note timing overlap
        // bucket the sequences by similarity
        List<List<int>> similarityBucketIndices = [];
        for (int i = 0; i < noteSequenceByMeasure.Count; i++)
        {
            // skip sequence that has already been bucketed
            if (similarityBucketIndices.Any(x => x.Contains(i))) continue;

            Dictionary<int, long> similarityRankings = [];
            for (int j = i + 1; j < noteSequenceByMeasure.Count; j++)
            {
                long similarityS1S2 = Util.CalculateSimilarity(
                    noteSequenceByMeasure[i],
                    noteSequenceByMeasure[j],
                    measures[i].Item1,
                    measures[j].Item1);

                long similarityS2S1 = Util.CalculateSimilarity(
                    noteSequenceByMeasure[j],
                    noteSequenceByMeasure[i],
                    measures[j].Item1,
                    measures[i].Item1);

                similarityRankings[j] = similarityS1S2 + similarityS2S1;
            }

            if (similarityRankings.Count == 0 || similarityRankings.Values.Max() <= 0)
            {
                continue;
            }

            // sort the similarity rankings by score and add all the indices with a score in the 90th percentile to a list
            List<int> bucketIndices = [];
            foreach (KeyValuePair<int, long> entry in similarityRankings.OrderByDescending(entry => entry.Value))
            {
                if (entry.Value >= similarityRankings.Values.Max() * 0.9)
                {
                    bucketIndices.Add(entry.Key);
                }
            }

            // if any one of the indices are already in a bucket, merge the buckets with only distinct indices
            bool foundBucket = false;
            for (int k = 0; k < similarityBucketIndices.Count; k++)
            {
                if (similarityBucketIndices[k].Intersect(bucketIndices).Any())
                {
                    similarityBucketIndices[k] = similarityBucketIndices[k].Union(bucketIndices).ToList();
                    foundBucket = true;
                    break;
                }
            }

            // if no bucket was found, create a new bucket
            if (!foundBucket)
            {
                similarityBucketIndices.Add(bucketIndices);
            }
        }

        return similarityBucketIndices;
    }

    static List<Pattern> FinalPatterns(
        List<List<int>> instPatterns, 
        List<Tuple<long, long>> measures, 
        List<NoteOnEvent> events, 
        string trackName, 
        int trackNumber)
    {
         // categorize versions within the patterns, group identical versions, create a master pattern for variation changes (ie chord progression)
        List<Pattern> finalPatterns = [];
        foreach (List<int> instPattern in instPatterns)
        {
            // take the fist version 
            Tuple<long, long> firstMeasure = measures[instPattern.First()];
            List<NoteOnEvent> firstMeasureNotes = Util.NotesInAMeasure(events, firstMeasure.Item1, firstMeasure.Item2);

            // capture the base timing for all versions
            List<Tuple<int, int>> baseTimingPattern = [];
            foreach (NoteOnEvent note in firstMeasureNotes)
            {
                baseTimingPattern.Add(new Tuple<int, int>(
                    (int)(note.AbsoluteTime - firstMeasure.Item1),
                    (int)(note.OffEvent.AbsoluteTime - firstMeasure.Item1)));
            }

            // capture the variation pitches for this pattern
            List<List<int>> noteVariationPatterns = [];
            List<int> masterPattern = [];
            int lastMeasureIndex = 0;
            foreach (int measureIndex in instPattern)
            {
                while (lastMeasureIndex < measureIndex - 1)
                {
                    lastMeasureIndex++;
                    masterPattern.Add(-1);
                }
                
                Tuple<long, long> measure = measures[measureIndex];
                List<NoteOnEvent> notes = Util.NotesInAMeasure(events, measure.Item1, measure.Item2);
                List<int> notePattern = notes.Select(x => x.NoteNumber).ToList();
                bool matched = false;
                foreach (List<int> prevPattern in noteVariationPatterns.ToList())
                {
                    if(notePattern.SequenceEqual(prevPattern))
                    {
                        masterPattern.Add(noteVariationPatterns.IndexOf(prevPattern));
                        matched = true;
                        break;
                    }
                }
                
                if (!matched)
                {
                    masterPattern.Add(noteVariationPatterns.Count);
                    noteVariationPatterns.Add(notePattern);
                }

                lastMeasureIndex = measureIndex;
            }
            
            Pattern pattern = new Pattern
            {
                TrackName = trackName,
                TrackNumber = trackNumber,
                BaseTimingPattern = baseTimingPattern,
                MasterPattern = masterPattern,
                NoteVariationPatterns = noteVariationPatterns
            };
            
            finalPatterns.Add(pattern);
        }

        return finalPatterns;
    }
}