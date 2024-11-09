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
    public class KnowledgeDataBase : IKeyWords, IInCheck
    {
        public KnowledgeDataBase()
        {
            KeyWords = new Dictionary<string, int>();
        }
        public KnowledgeDataBase(string knowledgeData, ILocalization localization, bool isImportant = false)
        {
            KnowledgeData = knowledgeData;
            IsImportant = isImportant;
            var Words = localization.WordSplit(knowledgeData);
            KeyWords = IKeyWords.GetKeyWords(Words);
            WordsCount = Words.Length;
        }
        /// <summary>
        /// 关键字组
        /// </summary>
        public virtual Dictionary<string, int> KeyWords { get; set; }
        /// <summary>
        /// 知识库数据
        /// </summary>
        public virtual string KnowledgeData { get; set; } = "";
        /// <summary>
        /// 是否是重要的知识库,必须加入到知识库中
        /// </summary>
        public virtual bool IsImportant { get; set; } = false;
        /// <summary>
        /// 该知识库词语总数
        /// </summary>
        public int WordsCount { get; set; }

        public int InCheck(string message, string[] keywords_list, Dictionary<string, int> keywords_dict)
        {
            if (IsImportant)
            {
                return 0;
            }
            return Math.Max(10, IInCheck.IgnoreValue - ((IKeyWords)this).Score(keywords_dict, keywords_list.Length));
        }

    }
}
