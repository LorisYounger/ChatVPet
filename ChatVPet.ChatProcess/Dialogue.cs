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

        // A failed lookup is not a correction to the player's facts or completed host actions.
        // Keep the original reply for display/history, but do not recycle it as memory evidence.
        [JsonIgnore]
        public string MemoryAnswer => Turn?.Receipts.Any(x => x.Tool == "历史补查" && x.Status != "observed") == true ? "" : Answer;

        internal IEnumerable<string> HostEvidence
        {
            get
            {
                if (Turn == null) yield break;
                if (Turn.Observation.Length > 0 && !Turn.Receipts.Any(x => x.Tool == "已有图片观察" && x.Status == "observed" && x.Facts == Turn.Observation))
                    yield return "图片观察: " + Turn.Observation;
                foreach (var receipt in Turn.Receipts.Where(x => x.Tool is not ("历史补查" or "操作判断")))
                    yield return receipt.ToContext();
            }
        }
        public float ImportanceWeight_Muti { get; set; } = 2;
        public float ImportanceWeight_Plus { get; set; } = 0;


        /// <summary>
        /// 转换为消息
        /// </summary>
        private IEnumerable<(long Order, ChatMessage Message)> OrderedMessages()
        {
            if (Turn != null)
            {
                foreach (var message in Turn.OrderedMessages()) yield return message;
                yield break;
            }
            if (Question.Length > 0) yield return (0, new("user", Question));
            if (Answer.Length > 0) yield return (0, new("assistant", Answer));
        }

        public IEnumerable<ChatMessage> ToMessages() => OrderedMessages().Select(m => m.Message);

        // A receipt can arrive after another player turn. Preserve that order in future requests and exports.
        public static IEnumerable<(string TurnId, ChatMessage Message)> Timeline(IEnumerable<Dialogue> entries) =>
            entries.SelectMany(d => d.OrderedMessages().Select(m => (d.DialogueId, m.Order, m.Message)))
                .OrderBy(m => m.Order).Select(m => (m.DialogueId, m.Message));

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
        // Stored keywords can still contain the initial reply from before an unsuccessful lookup.
        IEnumerable<string> Iw2vSource.KeyWords => MemoryAnswer.Length > 0 ? KeyWords : [Question];
        /// <summary>
        /// 记忆检索文本包括玩家原话、可采用的回答和历史宿主记录。
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<string> EmbeddingTexts => [Question, MemoryAnswer, .. HostEvidence];
    }
}
