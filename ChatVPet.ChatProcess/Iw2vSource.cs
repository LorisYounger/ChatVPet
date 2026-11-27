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
        /// 用于嵌入向量生成的原始文本列表。
        /// </summary>
        IEnumerable<string> EmbeddingTexts { get; }
    }
}
