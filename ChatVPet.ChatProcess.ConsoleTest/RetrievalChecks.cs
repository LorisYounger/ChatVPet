using Newtonsoft.Json;

namespace ChatVPet.ChatProcess.ConsoleTest;

internal static class RetrievalChecks
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS: {name}");
        }

        var process = new VPetChatProcess();
        var knowledge = new KnowledgeDataBase("balance", ["balance"], ["money left"], process.Localization);
        var tool = new ToolUse("balance", "read balance", ["money"], ["money left"], [], process.Localization);
        Check(knowledge.EmbeddingTexts.SequenceEqual(["money left", "balance"]), "Knowledge retrieval examples");
        Check(tool.EmbeddingTexts.Contains("money left"), "Tool retrieval examples");
        var plainKnowledge = new KnowledgeDataBase("balance", [], [], process.Localization);
        Check(W2VEngine.ComputeKeywordSimilarity("balance", plainKnowledge.KeyWords, process.Localization) > 0.99f,
            "Knowledge text remains searchable without explicit keywords or vectors");
        var restored = JsonConvert.DeserializeObject<KnowledgeDataBase>(JsonConvert.SerializeObject(knowledge))!;
        Check(restored.EmbeddingTexts.Contains("balance"), "Deserialized knowledge keeps its embedding text");
        var defaultTool = new ToolUse { Code = "old", Descriptive = "old description" };
        defaultTool.Code = "updated";
        Check(defaultTool.EmbeddingTexts.Contains("updated"), "Parameterless tools use current fields");

        int embeddingCalls = 0;
        var embeddedTexts = new List<string>();
        process.AIAPIEmbeddingFunction = texts =>
        {
            embeddingCalls++;
            var batch = texts.ToList();
            embeddedTexts.AddRange(batch);
            return batch.Select(_ => new float[] { 1, 0 }).ToArray();
        };
        process.Retrieval.Score("balance", [knowledge]);
        Check(embeddedTexts.Any(x => x.Contains("money left")), "Examples reach the embedding delegate");
        int cachedCalls = embeddingCalls;
        process.Retrieval.Score("balance", [knowledge]);
        Check(embeddingCalls == cachedCalls, "Cached vectors are reused");
        process.ClearRetrievalCache();
        process.Retrieval.Score("balance", [knowledge]);
        Check(embeddingCalls > cachedCalls, "Cleared vectors are rebuilt");

        Check(W2VEngine.ComputeKeywordSimilarity("体力", ["体力"], process.Localization) > 0.99f,
            "Chinese keyword segmentation matches");
        Check(W2VEngine.ComputeKeywordSimilarity("rain", ["balance"], process.Localization) == 0,
            "Unrelated keywords score zero");
        Check(W2VEngine.ComputeCosineSimilarity([1, 0], [-1, 0]) == 0,
            "Opposite vectors stay within the score range");

        process.KnowledgeDataBases.Add(new KnowledgeDataBase("unrelated", ["rain"], [], process.Localization));
        process.KnowledgeDataBases.Add(knowledge);
        process.KnowledgeDataBases.Add(new KnowledgeDataBase("partial", ["balance", "other"], [], process.Localization, 0.5));
        process.Tools.Add(tool);
        var dialogue = new Dialogue("balance", "balance", process.Localization);
        process.Dialogues.Add(dialogue);
        // Exercise keyword-only scoring without a network service.
        process.AIAPIEmbeddingFunction = null;
        process.ClearRetrievalCache();
        var result = process.QuerySimilar("balance");
        Check(result.Knowledges.Count == 2 && ReferenceEquals(result.Knowledges[0].Item, knowledge)
            && result.Knowledges[0].Score < result.Knowledges[1].Score,
            "Query filters unrelated knowledge and sorts ascending");
        Check(result.Tools.Count == 1 && ReferenceEquals(result.Tools[0].Item, tool),
            "Query returns the original tool");
        Check(result.Dialogues.Count == 1 && ReferenceEquals(result.Dialogues[0].Item, dialogue),
            "Query preserves history identity for the settings list");
        Console.WriteLine($"{checks} offline retrieval checks passed.");
    }
}
