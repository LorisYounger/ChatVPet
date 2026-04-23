using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 知识数据库 
    /// </summary>
    /// virtual 意味着你可以继承重写个动态知识库
    public class KnowledgeDataBase : Iw2vSource, IInCheck
    {
        public KnowledgeDataBase()
        {
            KeyWords = [];
        }
        /// <summary>
        /// 知识数据库构造函数
        /// </summary>
        /// <param name="knowledgeData">知识库数据</param>
        /// <param name="keyword">关键字数组</param>
        /// <param name="localization">本地化接口</param>
        /// <param name="important_muit">重要性乘法权重</param>
        /// <param name="important_plus">重要性加法权重</param>
        public KnowledgeDataBase(string knowledgeData, string[] keyword, ILocalization localization, double important_muit = 2, double important_plus = 0)
        {
            KnowledgeData = knowledgeData;
            ImportanceWeight_Muti = (float)important_muit;
            ImportanceWeight_Plus = (float)important_plus;
            KeyWords = [.. keyword.Select(x => string.Join(" ", localization.WordSplit(x))), string.Join(" ", localization.WordSplit(knowledgeData))];
        }

        /// <summary>
        /// 知识库数据
        /// </summary>
        public virtual string KnowledgeData { get; set; } = "";
        /// <summary>
        /// 重要性
        /// </summary>
        public float ImportanceWeight_Muti { get; set; } = 2.2f;//知识库包含很多字,所以为了加大权重,默认多一点
        public float ImportanceWeight_Plus { get; set; } = 0.01f;
        /// <summary>
        /// 关键字组（经分词处理，用于 BM25 关键词匹配）
        /// </summary>
        public IEnumerable<string> KeyWords { get; set; }
        /// <summary>
        /// 嵌入向量的原始文本列表，每项独立生成一个向量
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<string> EmbeddingTexts => [KnowledgeData];
        /// <summary>
        /// 向量
        /// </summary>
        [JsonIgnore]
        public float[]? Vector { get; set; }
        /// <summary>
        /// 每个 EmbeddingTexts 对应的嵌入向量缓存
        /// </summary>
        [JsonIgnore]
        public float[][]? EmbeddingVectors { get; set; }

        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

    }
}
