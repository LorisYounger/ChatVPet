using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ChatVPet.ChatProcess;

/// <summary>Per-process retrieval state. Only text-derived vectors and scores are cached.</summary>
public class ContextRetrieval(VPetChatProcess process)
{
    public string EmbeddingIdentity { get; set; } = "";
    public string RerankIdentity { get; set; } = "";
    public string QueryInstruction { get; set; } = "Given a conversation, retrieve relevant knowledge, memories and game tools.";
    public int CandidateCount { get; set; } = 24;
    public int VectorCapacity { get; set; } = 4096;
    public int QueryCapacity { get; set; } = 128;
    public int ScoreCapacity { get; set; } = 2048;
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public float MinimumRerankScore { get; set; } = 0.1f;
    public bool UseCache { get; set; } = true;
    public bool ServiceUnavailable { get; private set; }
    public long EmbeddingRequests { get; private set; }
    public long RerankRequests { get; private set; }
    public long RerankCandidates { get; private set; }
    public long CacheHits { get; private set; }
    public long CacheMisses { get; private set; }
    public double LastMilliseconds { get; private set; }
    private readonly Queue<double> timings = new();
    private readonly Queue<double> coldTimings = new();
    private readonly Queue<double> hotTimings = new();
    public double P95Milliseconds => timings.Count == 0 ? 0 : timings.Order().ElementAt((int)Math.Ceiling(timings.Count * 0.95) - 1);
    public double ColdP95Milliseconds => coldTimings.Count == 0 ? 0 : coldTimings.Order().ElementAt((int)Math.Ceiling(coldTimings.Count * 0.95) - 1);
    public double HotP95Milliseconds => hotTimings.Count == 0 ? 0 : hotTimings.Order().ElementAt((int)Math.Ceiling(hotTimings.Count * 0.95) - 1);
    private readonly Dictionary<string, float[]> vectors = [];
    private readonly Dictionary<string, (DateTime Time, float[] Vector)> queries = [];
    private readonly Dictionary<string, (DateTime Time, float Score)> scores = [];
    // Settings searches and conversation retrieval may overlap; serialize cache fills, including identical requests.
    private readonly object gate = new();
    private string scope = "";
    private int dimension;

    public void Clear()
    {
        lock (gate)
        {
            vectors.Clear(); queries.Clear(); scores.Clear(); dimension = 0;
        }
    }

    public List<T> Select<T>(string query, IEnumerable<T> source, int limit) where T : class, Iw2vSource, IInCheck
    {
        lock (gate)
        {
            var timer = Stopwatch.StartNew();
            long requestsBefore = EmbeddingRequests + RerankRequests;
            ServiceUnavailable = false;
            string currentScope = process.SaveId + "|" + EmbeddingIdentity + "|" + RerankIdentity + "|" + QueryInstruction;
            if (scope != currentScope) { Clear(); scope = currentScope; }
            var candidates = source.Select(x => (Item: x, Text: string.Join("\n", x.EmbeddingTexts))).ToList();
            if (limit <= 0 || candidates.Count == 0) return [];
            float[] queryVector = [];
            var currentVectors = new Dictionary<string, float[]>();
            try
            {
                if (process.AIAPIEmbeddingFunction != null)
                {
                    string queryText = QueryInstruction.Length == 0 ? query : $"Instruct: {QueryInstruction}\nQuery: {query}";
                    string queryKey = Digest(queryText);
                    if (UseCache && queries.TryGetValue(queryKey, out var cached) && DateTime.UtcNow - cached.Time < CacheLifetime)
                    {
                        queryVector = cached.Vector; CacheHits++;
                    }
                    else
                    {
                        CacheMisses++; EmbeddingRequests++;
                        var result = process.AIAPIEmbeddingFunction([queryText]);
                        ValidateVectors(result, 1);
                        queryVector = result[0];
                        if (UseCache)
                        {
                            if (queries.Count >= QueryCapacity) queries.Remove(queries.Keys.First());
                            queries[queryKey] = (DateTime.UtcNow, queryVector);
                        }
                    }
                    var missing = new Dictionary<string, string>();
                    foreach (var candidate in candidates)
                    {
                        string key = Digest(candidate.Text);
                        if (UseCache && vectors.TryGetValue(key, out var vector)) { currentVectors[key] = vector; CacheHits++; }
                        else { missing[key] = candidate.Text; CacheMisses++; }
                    }
                    foreach (var batch in missing.Chunk(64))
                    {
                        EmbeddingRequests++;
                        var result = process.AIAPIEmbeddingFunction(batch.Select(x => x.Value));
                        ValidateVectors(result, batch.Length);
                        for (int i = 0; i < batch.Length; i++)
                        {
                            currentVectors[batch[i].Key] = result[i];
                            if (UseCache)
                            {
                                if (vectors.Count >= VectorCapacity) vectors.Remove(vectors.Keys.First());
                                vectors[batch[i].Key] = result[i];
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ServiceUnavailable = true;
                queryVector = [];
                process.LastRetrievalError = "向量服务不可用，使用关键词检索。";
            }
            var shortlist = candidates.Select(x =>
            {
                float keyword = W2VEngine.ComputeKeywordSimilarity(query, x.Item.KeyWords.Concat([x.Text]), process.Localization);
                float similarity = keyword;
                if (queryVector.Length > 0 && currentVectors.TryGetValue(Digest(x.Text), out var vector))
                    similarity = 0.8f * W2VEngine.ComputeCosineSimilarity(queryVector, vector) + 0.2f * keyword;
                return (x.Item, x.Text, Score: x.Item.InCheck(query, similarity));
            }).Where(x => x.Score < IInCheck.IgnoreValue).OrderBy(x => x.Score).Take(CandidateCount).ToList();
            var selected = shortlist.Select(x => x.Item).Take(limit).ToList();
            if (shortlist.Count > 0 && process.AIAPIRerankFunction != null)
            {
                try
                {
                    var pairScores = new float[shortlist.Count];
                    var missing = new List<int>();
                    for (int i = 0; i < shortlist.Count; i++)
                    {
                        var key = Digest(query + "\0" + shortlist[i].Text);
                        if (UseCache && scores.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Time < CacheLifetime)
                        { pairScores[i] = cached.Score; CacheHits++; }
                        else { missing.Add(i); CacheMisses++; }
                    }
                    if (missing.Count > 0)
                    {
                        RerankRequests++; RerankCandidates += missing.Count;
                        var result = process.AIAPIRerankFunction(query, missing.Select(i => shortlist[i].Text).ToList());
                        if (result.Length != missing.Count || result.Any(x => !float.IsFinite(x) || x < 0 || x > 1))
                            throw new InvalidDataException("Rerank must return one finite score in [0,1] per document, in input order.");
                        for (int j = 0; j < missing.Count; j++)
                        {
                            int i = missing[j]; pairScores[i] = result[j];
                            if (UseCache)
                            {
                                if (scores.Count >= ScoreCapacity) scores.Remove(scores.Keys.First());
                                scores[Digest(query + "\0" + shortlist[i].Text)] = (DateTime.UtcNow, result[j]);
                            }
                        }
                    }
                    selected = shortlist.Select((x, i) => (x.Item, Score: pairScores[i])).Where(x => x.Score >= MinimumRerankScore)
                        .OrderByDescending(x => x.Score).Take(limit).Select(x => x.Item).ToList();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    ServiceUnavailable = true;
                    process.LastRetrievalError = "重排服务不可用，沿用混合召回顺序。";
                }
            }
            LastMilliseconds = timer.Elapsed.TotalMilliseconds;
            timings.Enqueue(LastMilliseconds);
            if (timings.Count > 200) timings.Dequeue();
            var temperatureTimings = EmbeddingRequests + RerankRequests == requestsBefore ? hotTimings : coldTimings;
            temperatureTimings.Enqueue(LastMilliseconds);
            if (temperatureTimings.Count > 200) temperatureTimings.Dequeue();
            return selected;
        }
    }

    private void ValidateVectors(float[][] result, int count)
    {
        if (result.Length != count || result.Any(v => v.Length == 0 || v.Any(x => !float.IsFinite(x))))
            throw new InvalidDataException("Embedding count or values are invalid.");
        int returnedDimension = result[0].Length;
        if (result.Any(v => v.Length != returnedDimension) || dimension != 0 && dimension != returnedDimension)
        {
            Clear();
            throw new InvalidDataException("Embedding dimensions changed; vectors cleared.");
        }
        dimension = returnedDimension;
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
