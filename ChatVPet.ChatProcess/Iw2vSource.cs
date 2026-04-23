using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    public interface Iw2vSource
    {
        /// <summary>
        /// 关键字组，用于 BM25 关键词匹配。
        /// 通常为各字段文本经过分词后的词组（每个元素为一段分词结果），
        /// 与 <see cref="EmbeddingTexts"/> 分开，避免向量与关键词职责混淆。
        /// </summary>
        IEnumerable<string> KeyWords { get; }

        /// <summary>
        /// 用于嵌入向量生成的原始文本列表，每项将被独立生成一个向量，
        /// 与 <see cref="EmbeddingVectors"/> 一一对应。
        /// </summary>
        IEnumerable<string> EmbeddingTexts { get; }

        /// <summary>
        /// 聚合向量（<see cref="EmbeddingVectors"/> 各向量的均值），用于快速余弦相似度检索。
        /// 由 <see cref="W2VEngine"/> 自动填充，无需手动赋值。
        /// </summary>
        float[]? Vector { get; set; }

        /// <summary>
        /// 每个 <see cref="EmbeddingTexts"/> 对应的嵌入向量缓存，
        /// 由 <see cref="W2VEngine"/> 自动填充，无需手动赋值。
        /// </summary>
        float[][]? EmbeddingVectors { get; set; }
    }
}
