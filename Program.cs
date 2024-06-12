using AlgoRhythmAnalyzer;
using NAudio.Midi;
using Newtonsoft.Json;

public class Program
{
    public static void Main(string[] args)
    {
        string midiPath = args[0];

        Console.WriteLine($"Reading {midiPath}");
        MidiReader midiReader = new MidiReader(midiPath);

        List<Tuple<long, long>> measures = midiReader.ExtractMeasures();

        (Dictionary<string, List<NoteOnEvent>> drumEvents,
                Dictionary<string, List<NoteOnEvent>> otherEventsByInstrument) =
            midiReader.ReadDrumAndInst();

        Dictionary<string, List<List<int>>> drumPatterns = [];
        foreach (KeyValuePair<string, List<NoteOnEvent>> entry in drumEvents)
        {
            List<List<int>> patterns = GeneratePatterns(entry.Value, measures);
            drumPatterns[entry.Key] = patterns;
        }
        
        Dictionary<string, List<List<int>>> otherPatternsByInstrument = [];
        foreach (KeyValuePair<string, List<NoteOnEvent>> entry in otherEventsByInstrument)
        {
            List<List<int>> patterns = GeneratePatterns(entry.Value, measures);
            otherPatternsByInstrument[entry.Key] = patterns;
        }

        string parentDirectory = Path.GetDirectoryName(args[0]);
        string midiFileWithoutExtension = Path.GetFileNameWithoutExtension(args[0]);

        // serialize the patterns to JSON with Newtonsoft
        string drumPatternsJson = JsonConvert.SerializeObject(drumPatterns, Formatting.Indented);
        File.WriteAllText(
            Path.Combine(parentDirectory, midiFileWithoutExtension + "-drum-patterns.json"),
            drumPatternsJson);

        string otherPatternsByInstrumentJson =
            JsonConvert.SerializeObject(otherPatternsByInstrument, Formatting.Indented);

        File.WriteAllText(
            Path.Combine(parentDirectory, midiFileWithoutExtension + "-inst-patterns.json"),
            otherPatternsByInstrumentJson);

        Console.WriteLine($"Wrote to {parentDirectory}");
    }

    static List<List<int>> GeneratePatterns(List<NoteOnEvent> events, List<Tuple<long, long>> measures)
    {
        // gather each note sequence by measure, by determining if the note start time is within the measure start and end times
        List<List<NoteOnEvent>> noteSequenceByMeasure = [];
        noteSequenceByMeasure.AddRange(measures.Select(measure =>
            Util.NotesInAMeasure(events, measure.Item1, measure.Item2)));

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
}