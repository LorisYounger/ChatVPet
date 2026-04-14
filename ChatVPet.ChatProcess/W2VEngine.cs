
namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 词向量引擎（使用 AIAPI 兼容格式的远程 Embedding API）
    /// </summary>
    public class W2VEngine
    {
        /// <summary>
        /// 引索来源
        /// </summary>
        public VPetChatProcess CP { get; set; }

        public W2VEngine(VPetChatProcess vPetChatProcess)
        {
            CP = vPetChatProcess;
        }

        /// <summary>
        /// 获得指定文本的向量
        /// </summary>
        public float[] GetQueryVector(string query)
        {
            return CP?.AIAPIEmbeddingFunction?.Invoke(query) ?? [];
        }

        /// <summary>
        /// 获得/更新 指定原列的向量（仅更新 Vector 为 null or [] 的项）
        /// </summary>
        public void GetQueryVector(IEnumerable<Iw2vSource> iw2vSource)
        {
            var needUpdate = iw2vSource.Where(x => x.Vector == null || x.Vector.Length == 0).ToList();
            if (needUpdate.Count == 0)
                return;
            FillVectors(needUpdate);
        }

        private void FillVectors(List<Iw2vSource> sources)
        {
            const int batchSize = 64;
            var texts = sources.Select(x => string.Join(" ", x.KeyWords)).ToList();

            for (int i = 0; i < sources.Count; i += batchSize)
            {
                int count = Math.Min(batchSize, sources.Count - i);
                List<string> batch = texts.GetRange(i, count);

                List<float[]> result = new List<float[]>();
                for (int j = 0; j < count; j++)
                {
                    result.Add(GetQueryVector(batch[j]));
                }
                for (int j = 0; j < count; j++)
                {
                    sources[i + j].Vector = result[j];
                }
            }
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
