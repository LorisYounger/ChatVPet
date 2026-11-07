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
        public float VectorWeight { get; set; } = 0.8f;

        /// <summary>
        /// 综合分数中关键词评分的权重。属性沿用 BM25 命名，实际使用命中词评分。
        /// </summary>
        public float BM25Weight { get; set; } = 0.2f;

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
                .Where(x => x.EmbeddingVectors == null || x.EmbeddingVectors.Length == 0)
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
        /// <summary>
        /// 计算查询文本与来源的综合相似度，结合向量余弦相似度和关键词命中率。 越大越相似
        /// </summary>
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
            float keywordScore = ComputeKeywordSimilarity(message, source.KeyWords, CP?.Localization);

            // 无向量时退化为仅用关键词权重，避免向量为0时稀释总分
            float usedVectorWeight = cosineScores.Count > 0 ? VectorWeight : 0f;
            float totalWeight = Math.Max(MinTotalWeight, usedVectorWeight + BM25Weight);
            return (vectorScore * usedVectorWeight + keywordScore * BM25Weight) / totalWeight;
        }

        public static float AggregateSimilarity(List<float> scores, int dropLowCount)
        {
            if (scores.Count == 0)
                return 0;
            if (scores.Count == 1)
                return scores[0];

            var ordered = scores.OrderByDescending(x => x).ToList();
            // 只在向量数量 >= 3 时才丢弃低分，避免双向量时退化为 Max 操作
            int effectiveDrop = ordered.Count >= 3 ? Math.Max(0, dropLowCount) : 0;
            int keepCount = Math.Max(1, ordered.Count - effectiveDrop);
            return (float)Math.Clamp(ordered.Take(keepCount).Sum() / Math.Sqrt(keepCount), 0, 1);
        }

        /// <summary>
        /// 计算查询文本与关键词集合的关键词命中率。 数值越大越相似
        /// <para>
        /// KeyWords 是程序员预先编辑的可能会碰撞的词语（如 <c>"钱"</c>、<c>"体力"</c>），
        /// 当查询词命中关键词时提高匹配程度。<br/>
        /// KeyWords 在各来源格式不统一：<br/>
        /// - DynKDBStatic 等手动设置关键词的来源：<c>localization.WordSplit(...)</c>
        ///   每个分词词语一个元素（如 <c>"钱"</c>、<c>"体力"</c>）<br/>
        /// - 知识和工具包含原始描述；<see cref="Dialogue"/> 包含空格拼接的分词文本。
        /// </para>
        /// 当 <paramref name="localization"/> 可用时，查询词和关键词均使用词语级别分词（<c>localization.WordSplit</c>），
        /// 确保多字词（如 <c>"体力"</c>）能在双侧正确匹配，同时对查询词过滤停用词。<br/>
        /// 不可用时退回正则 Tokenize（CJK 单字拆分）。<br/>
        /// 返回命中词数除以关键词总数的平方根，并限制到 [0, 1]。
        /// </summary>
        /// <param name="query">用户输入的查询文本</param>
        /// <param name="keywords">来源的 KeyWords 集合（格式可能为单词元素或空格拼接整句）</param>
        /// <param name="localization">本地化接口，用于词语级别分词和停用词过滤；为 null 时退回正则 Tokenize</param>
        /// <returns>关键词命中率，范围 [0, 1]；关键词集合为空时返回 0</returns>
        public static float ComputeKeywordSimilarity(string query, IEnumerable<string> keywords, ILocalization? localization = null)
        {
            if (keywords == null || string.IsNullOrWhiteSpace(query))
                return 0;

            // ── 查询词处理：优先用 Localization 词级别分词 + 停用词过滤 ──
            List<string> queryTerms;
            if (localization != null)
            {
                var stopWords = localization.StopWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
                queryTerms = localization.WordSplit(query)
                    .Where(w => !string.IsNullOrWhiteSpace(w) && !stopWords.Contains(w))
                    .Select(w => w.ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }
            else
            {
                queryTerms = Tokenize(query).Distinct().ToList();
            }

            if (queryTerms.Count == 0)
                return 0;

            // ── 关键词处理：与查询词使用相同粒度的分词策略 ──
            // 当 localization 可用时，使用 WordSplit 进行词语级别分词，
            // 确保多字中文词（如"体力"）在两侧保持一致，避免粒度不匹配导致命中率为 0。
            // 无论元素是单个词语（"钱"）还是空格拼接整句（"金钱 数据 体力"），均可正确处理。
            HashSet<string> keywordSet;
            if (localization != null)
            {
                keywordSet = keywords
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .SelectMany(x => localization.WordSplit(x))
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => w.ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                keywordSet = keywords
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .SelectMany(x => Tokenize(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (keywordSet.Count == 0)
                return 0;

            // 用词表大小的平方根归一化命中词数
            int hitCount = queryTerms.Count(t => keywordSet.Contains(t));
            return (float)Math.Clamp(hitCount / Math.Sqrt(keywordSet.Count), 0, 1);
        }

        /// <summary>
        /// 已废弃：请使用 <see cref="ComputeKeywordSimilarity(string, IEnumerable{string}, ILocalization?)"/>。
        /// 保留此方法仅为兼容外部调用。
        /// </summary>
        [Obsolete("Use ComputeKeywordSimilarity(string, IEnumerable<string>, ILocalization?) instead.")]
        public static float ComputeBM25Similarity(string query, IEnumerable<string> documents)
            => ComputeKeywordSimilarity(query, documents);

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
        /// 计算余弦相似度，返回值已 clamp 到 [0, 1] 数值越大越相似。
        /// </summary>
        public static float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                Console.WriteLine($"[W2VEngine] Vector dimension mismatch: {vectorA.Length} vs {vectorB.Length}. Did you change the Embedding model? Clear vector cache via ClearVectors(IEnumerable<Iw2vSource> sources), for example by clearing vectors for KnowledgeDataBases/Tools/Dialogues/DiaryEntries.");
                return 0;
            }

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
            return Math.Clamp(dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)), 0f, 1f);
        }
    }
}
