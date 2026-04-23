using System.Text.RegularExpressions;
namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 词向量引擎（使用 AIAPI 兼容格式的远程 Embedding API）
    /// </summary>
    public class W2VEngine
    {
        private static readonly Regex TokenRegex = new(@"[\u4e00-\u9fff]|[\p{L}\p{N}]+", RegexOptions.Compiled);
        public const int DefaultDropLowCount = 1;
        public const float MinTotalWeight = 0.0001f;

        /// <summary>
        /// 引索来源
        /// </summary>
        public VPetChatProcess CP { get; set; }

        /// <summary>
        /// 综合分数中向量权重
        /// </summary>
        public float VectorWeight { get; set; } = 0.75f;

        /// <summary>
        /// 综合分数中关键词 BM25 权重
        /// </summary>
        public float BM25Weight { get; set; } = 0.25f;

        /// <summary>
        /// 多向量聚合时去掉最低分数量
        /// </summary>
        public int DropLowScoreCount { get; set; } = DefaultDropLowCount;

        public W2VEngine(VPetChatProcess vPetChatProcess)
        {
            CP = vPetChatProcess;
        }

        /// <summary>
        /// 获得指定文本的向量
        /// </summary>
        public float[] GetQueryVector(string query)
        {
            return CP?.AIAPIEmbeddingFunction?.Invoke([query])?.FirstOrDefault() ?? [];
        }
        /// <summary>
        /// 获得指定文本的向量
        /// </summary>
        public float[][] GetQueryVector(IEnumerable<string> query)
        {
            return CP?.AIAPIEmbeddingFunction?.Invoke(query) ?? [];
        }

        /// <summary>
        /// 清除指定集合中所有条目的向量缓存，下次使用时将重新计算。
        /// 更换 Embedding API 后调用此方法以避免使用旧 API 产生的缓存向量。
        /// </summary>
        public static void ClearVectors(IEnumerable<Iw2vSource> sources)
        {
            foreach (var source in sources)
            {
                source.EmbeddingVectors = null;
                source.Vector = null;
            }
        }

        /// <summary>
        /// 获得/更新 指定原列的向量（仅更新 EmbeddingVectors 为 null or [] 的项）
        /// </summary>
        public void GetQueryVector(IEnumerable<Iw2vSource> iw2vSource)
        {
            var needUpdate = iw2vSource
                .Where(x => x.EmbeddingVectors == null || x.EmbeddingVectors.Length == 0 || x.Vector == null || x.Vector.Length == 0)
                .ToList();
            if (needUpdate.Count == 0)
                return;
            FillVectors(needUpdate);
        }

        private void FillVectors(List<Iw2vSource> sources)
        {
            const int batchSize = 64;
            var tasks = sources
                .Select((source, index) => (source, index, texts: NormalizeInputs(source.EmbeddingTexts)))
                .ToList();
            var allTexts = tasks.SelectMany(x => x.texts).ToList();

            if (allTexts.Count == 0)
            {
                foreach (var task in tasks)
                {
                    task.source.EmbeddingVectors = [];
                    task.source.Vector = [];
                }
                return;
            }

            var allVectors = new List<float[]>(allTexts.Count);
            for (int i = 0; i < allTexts.Count; i += batchSize)
            {
                int count = Math.Min(batchSize, allTexts.Count - i);
                var batch = allTexts.GetRange(i, count);
                var result = GetQueryVector(batch);
                allVectors.AddRange(result);
            }

            int cursor = 0;
            foreach (var task in tasks)
            {
                int count = task.texts.Count;
                var vectors = count == 0 ? [] : allVectors.GetRange(cursor, count).ToArray();
                cursor += count;

                task.source.EmbeddingVectors = vectors;
                task.source.Vector = AggregateVectors(vectors);
            }
        }

        private static List<string> NormalizeInputs(IEnumerable<string> inputs)
        {
            return inputs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static float[]? AggregateVectors(float[][] vectors)
        {
            if (vectors.Length == 0)
                return [];
            if (vectors.Length == 1)
                return vectors[0];

            int dim = vectors[0].Length;
            if (dim == 0 || vectors.Any(x => x.Length != dim))
                return vectors[0];

            var merged = new float[dim];
            foreach (var vector in vectors)
            {
                for (int i = 0; i < dim; i++)
                    merged[i] += vector[i];
            }

            for (int i = 0; i < dim; i++)
                merged[i] /= vectors.Length;
            return merged;
        }

        public float ComputeSimilarity(string message, Iw2vSource source, float[] queryVector)
        {
            var vectors = source.EmbeddingVectors?.Where(x => x != null && x.Length > 0).ToList() ?? [];
            if (vectors.Count == 0 && source.Vector is { Length: > 0 })
                vectors.Add(source.Vector);

            var cosineScores = vectors
                .Select(x => ComputeCosineSimilarity(x!, queryVector))
                .Where(x => !float.IsNaN(x))
                .ToList();

            float vectorScore = AggregateSimilarity(cosineScores, DropLowScoreCount);
            float bm25Score = ComputeBM25Similarity(message, source.KeyWords);
            float totalWeight = Math.Max(MinTotalWeight, VectorWeight + BM25Weight);
            return (vectorScore * VectorWeight + bm25Score * BM25Weight) / totalWeight;
        }

        public static float AggregateSimilarity(List<float> scores, int dropLowCount)
        {
            if (scores.Count == 0)
                return 0;
            if (scores.Count == 1)
                return scores[0];

            var ordered = scores.OrderByDescending(x => x).ToList();
            int keepCount = Math.Max(1, ordered.Count - Math.Max(0, dropLowCount));
            return ordered.Take(keepCount).Average();
        }

        public static float ComputeBM25Similarity(string query, IEnumerable<string> documents)
        {
            var docList = documents
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            if (docList.Count == 0 || string.IsNullOrWhiteSpace(query))
                return 0;

            var queryTerms = Tokenize(query).Distinct().ToList();
            if (queryTerms.Count == 0)
                return 0;

            var tokenizedDocs = docList.Select(Tokenize).ToList();
            double avgDocLen = Math.Max(1, tokenizedDocs.Average(x => x.Count));
            const double k1 = 1.5; // BM25 参数：词频饱和控制
            const double b = 0.75; // BM25 参数：文档长度归一化强度

            // 预计算每个文档的词频表，避免内层循环重复遍历
            var tfTables = tokenizedDocs
                .Select(tokens => tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count()))
                .ToList();

            // 预计算每个查询词在多少文档中出现（文档频率），避免在评分循环中重复扫描
            var dfMap = queryTerms.ToDictionary(
                term => term,
                term => tfTables.Count(tf => tf.ContainsKey(term)));

            var scores = new List<double>(tokenizedDocs.Count);
            for (int di = 0; di < tokenizedDocs.Count; di++)
            {
                var docTokens = tokenizedDocs[di];
                if (docTokens.Count == 0)
                {
                    scores.Add(0);
                    continue;
                }

                var tfTable = tfTables[di];
                double score = 0;
                foreach (var term in queryTerms)
                {
                    if (!tfTable.TryGetValue(term, out int tf))
                        continue;

                    int df = dfMap[term];
                    double idf = Math.Log(1 + (tokenizedDocs.Count - df + 0.5) / (df + 0.5));
                    double denom = tf + k1 * (1 - b + b * docTokens.Count / avgDocLen);
                    score += idf * (tf * (k1 + 1)) / denom;
                }
                scores.Add(score);
            }

            double sum = 0;
            double max = 0;
            foreach (var score in scores)
            {
                sum += score;
                if (score > max)
                    max = score;
            }
            if (max <= 0)
                return 0;
            return (float)((sum / scores.Count) / max);
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];
            return TokenRegex.Matches(text.ToLowerInvariant())
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        /// <summary>
        /// 计算余弦相似度
        /// </summary>
        public static float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                return 0;

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0) return 0;
            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}
