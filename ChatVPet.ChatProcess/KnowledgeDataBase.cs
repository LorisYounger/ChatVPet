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
            KeyWords = "";
        }
        public KnowledgeDataBase(string knowledgeData, ILocalization localization, double important_muit = 2, double important_plus = 0)
        {
            KnowledgeData = knowledgeData;
            ImportanceWeight_Muti = (float)important_muit;
            ImportanceWeight_Plus = (float)important_plus;
            KeyWords = string.Join(" ", localization.WordSplit(knowledgeData));
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
        /// 关键字组
        /// </summary>
        public string KeyWords { get; set; }
        /// <summary>
        /// 向量
        /// </summary>
        [JsonIgnore]
        public float[]? Vector { get; set; }

        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

    }
}
