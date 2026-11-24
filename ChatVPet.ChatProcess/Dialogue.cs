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
        public string DialogueId { get; set; } = Guid.NewGuid().ToString("N");
        public string SaveId { get; set; } = "default";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public ChatTurn? Turn { get; set; }
        /// <summary>
        /// 问题
        /// </summary>
        public string Question { get; set; } = "";
        /// <summary>
        /// 回答
        /// </summary>
        public string Answer { get; set; } = "";

        public float ImportanceWeight_Muti { get; set; } = 2;
        public float ImportanceWeight_Plus { get; set; } = 0;


        /// <summary>
        /// 转换为消息
        /// </summary>
        public string[] ToMessages()
        {
            // Reconstruct only conversation facts. Old references, state snapshots and summaries
            // must not be recursively carried into each later request.
            if (Turn == null) return [Question, Answer];
            string answer = Answer;
            if (Turn.ResultInput.Length > 0 && Turn.Initial.Raw != Answer)
                answer = Turn.Initial.Raw + "\n" + Answer;
            string receipts = string.Join("; ", Turn.Receipts
                .Where(x => x.Tool is not ("历史补查" or "操作判断"))
                .Select(x => x.ToContext()));
            if (receipts.Length > 0) answer += "\n[历史回执:" + ChatProtocol.Field(receipts) + "]";
            return [ChatProtocol.UserMessage(Question, observation: Turn.Observation), answer];
        }

        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

        public Dialogue() { KeyWords = []; }
        public Dialogue(string question, string answer, ILocalization localization, double important_muit = 2, double important_plus = 0)
        {
            Question = question;
            Answer = answer;
            KeyWords =
            [
                 string.Join(" ", localization.WordSplit(question)),
                    string.Join(" ", localization.WordSplit(answer))
            ];


            ImportanceWeight_Muti = (float)important_muit;
            ImportanceWeight_Plus = (float)important_plus;
        }
        /// <summary>
        /// 关键字组（经分词处理，用于 BM25 关键词匹配）
        /// </summary>
        public IEnumerable<string> KeyWords { get; set; }
        /// <summary>
        /// 嵌入向量的原始文本列表（问题与回答），每项独立生成一个向量
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<string> EmbeddingTexts => [Question, Answer];

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
    }
}
