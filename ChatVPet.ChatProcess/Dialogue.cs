using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 聊天历史记录
    /// </summary>
    public class Dialogue : IKeyWords, IInCheck
    {
        /// <summary>
        /// 问题
        /// </summary>
        public string Question { get; set; } = "";
        /// <summary>
        /// 回答
        /// </summary>
        public string Answer { get; set; } = "";

        /// <summary>
        /// 工具调用 (源文本)
        /// </summary>
        public string ToolCall { get; set; } = "";
        /// <summary>
        /// 关键字组
        /// </summary>
        public Dictionary<string, int> KeyWords { get; set; } = new Dictionary<string, int>();
        /// <summary>
        /// 重要性
        /// </summary>
        public double Importance { get; set; } = 0.5;
        /// <summary>
        /// 该知识库词语总数
        /// </summary>
        public int WordsCount { get; set; }
        public int InCheck(string message, string[] keywords_list, Dictionary<string, int> keywords_dict)
        {
            if (Importance == 1)
            {
                return 0;
            }
            return Math.Max(10, IInCheck.IgnoreValue - (int)(((IKeyWords)this).Score(keywords_dict, keywords_list.Length) * 2 * Importance));
        }
        /// <summary>
        /// 转换为消息
        /// </summary>
        public string[] ToMessages(ILocalization localization)
        {
            return [Question, localization.Response + "\n" + Answer + "\n" + localization.ToolCall + "\n" + ToolCall];
        }
        public Dialogue() { }
        public Dialogue(string question, string answer, string toolCall,double importance, ILocalization localization)
        {
            Question = question;
            Answer = answer;
            ToolCall = toolCall;
            var Words = localization.WordSplit(question + answer);
            KeyWords = IKeyWords.GetKeyWords(Words);
            Importance = importance;
            WordsCount = Words.Length;
        }

    }
}
