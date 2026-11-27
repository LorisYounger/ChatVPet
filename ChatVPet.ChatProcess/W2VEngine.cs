using System.Text.RegularExpressions;
namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 向量余弦与关键词相似度计算
    /// </summary>
    public static class W2VEngine
    {
        private static readonly Regex TokenRegex = new(@"[\u4e00-\u9fff]|[\p{L}\p{N}]+", RegexOptions.Compiled);

        /// <summary>
        /// 计算查询文本与关键词集合的关键词命中率。 数值越大越相似
        /// </summary>
        public static float ComputeKeywordSimilarity(string query, IEnumerable<string> keywords, ILocalization? localization = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 0;

            Func<string, IEnumerable<string>> split = localization != null
                ? localization.WordSplit
                : text => TokenRegex.Matches(text.ToLowerInvariant()).Select(x => x.Value);

            var stopWords = localization?.StopWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var queryTerms = split(query)
                .Where(w => !string.IsNullOrWhiteSpace(w) && (stopWords == null || !stopWords.Contains(w)))
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (queryTerms.Count == 0)
                return 0;

            var keywordSet = keywords
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(split)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (keywordSet.Count == 0)
                return 0;

            int hitCount = queryTerms.Count(keywordSet.Contains);
            return (float)Math.Clamp(hitCount / Math.Sqrt(keywordSet.Count), 0, 1);
        }

        /// <summary>
        /// 计算余弦相似度，返回值已 clamp 到 [0, 1] 数值越大越相似。
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
            return Math.Clamp(dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)), 0f, 1f);
        }
    }
}
