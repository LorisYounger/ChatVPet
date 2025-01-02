using Newtonsoft.Json;
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
    public class Dialogue : Iw2vSource, IInCheck
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
        /// 重要性
        /// </summary>
        public float Importance { get; set; } = 2;

        /// <summary>
        /// 转换为消息
        /// </summary>
        public string[] ToMessages(ILocalization localization)
        {
            return [Question, localization.Response + "\n" + Answer + "\n" + localization.ToolCall + "\n" + ToolCall];
        }

        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

        public Dialogue() { KeyWords = ""; }
        public Dialogue(string question, string answer, string toolCall, double importance, ILocalization localization)
        {
            Question = question;
            Answer = answer;
            ToolCall = toolCall;
            KeyWords = string.Join(" ", localization.WordSplit(question + answer));

        }
        /// <summary>
        /// 关键字组
        /// </summary>
        public string KeyWords { get; set; }

        /// <summary>
        /// 向量
        /// </summary>
        [JsonIgnore]
        public float[]? Vector { get; set; }
    }
}
