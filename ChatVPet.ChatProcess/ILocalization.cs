using System.Linq;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 本地化方法
    /// </summary>
    public interface ILocalization
    {
        /// <summary>
        /// 数据库如下
        /// </summary>
        string DataBaseFollows { get; }
        /// <summary>
        /// 回复
        /// </summary>
        string Response { get; }
        /// <summary>
        /// 工具调用
        /// </summary>
        string ToolCall { get; }
        /// <summary>
        /// 文本分词
        /// </summary>
        /// <param name="text">文本</param>
        /// <returns>分词后文本</returns>
        string[] WordSplit(string text);
        /// <summary>
        /// 工具`{0}`返回
        /// </summary>
        string ToolReturn { get; }
        /// <summary>
        /// 停用词
        /// </summary>
        string[] StopWords { get; }

        // ── 日记功能相关字符串 ──────────────────────────────────────────

        /// <summary>
        /// 日记摘要压缩时发给 LLM 的系统提示
        /// </summary>
        string DiaryCompressionSystemPrompt { get; }

        /// <summary>
        /// 日记摘要压缩时发给 LLM 的用户消息模板（{0} 为对话文本）
        /// </summary>
        string DiaryCompressionUserPrompt { get; }

        /// <summary>
        /// 注入系统提示时日记段落的标题行
        /// </summary>
        string DiaryHeader { get; }

        /// <summary>
        /// 日记工具：调整指定日记条目权重 的描述
        /// </summary>
        string DiaryAdjustWeightToolDesc { get; }

        /// <summary>
        /// 日记工具 diary_id 参数描述
        /// </summary>
        string DiaryArgIdDesc { get; }

        /// <summary>
        /// 日记工具 delta 参数描述
        /// </summary>
        string DiaryArgDeltaDesc { get; }

        /// <summary>
        /// 简体中文
        /// </summary>
        public class LChineseSimple : ILocalization
        {
            public string DataBaseFollows => "数据库如下:";

            public string Response => "回复:";

            public string ToolCall => "工具调用:";

            public string ToolReturn => "工具 `{0}` 返回:\n```\n{1}\n```\n";

            public string DiaryCompressionSystemPrompt =>
                "你是一个记忆整理助手。请将提供的对话历史整理成一段简洁的日记摘要，重点记录重要的事件、情感和信息，使用第一人称（\"我\"）撰写，不要遗漏关键细节。";

            public string DiaryCompressionUserPrompt =>
                "请为以下对话历史生成一段日记摘要：\n\n{0}";

            public string DiaryHeader => "【历史日记记录】";

            public string DiaryAdjustWeightToolDesc =>
                "调整指定日记条目的重要性权重。diary_id 为日记条目的 ID（见系统提示中 # 后面的十六进制整数，例如 #1a），delta 为浮点数调整量（正数增加，负数减少）。";

            public string DiaryArgIdDesc => "日记条目 ID（系统提示中 # 后面的十六进制整数，例如 1a 或 2b）";
            public string DiaryArgDeltaDesc => "权重调整量，正数增加，负数减少，例如 0.5 或 -0.5";

            public JiebaNet.Segmenter.JiebaSegmenter Segmenter = new JiebaNet.Segmenter.JiebaSegmenter();
            public string[] WordSplit(string text) => Segmenter.Cut(text).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            public string[] StopWords => [.. Properties.Resources.StopWord_zh_hans.Split(' '), .. Properties.Resources.StopWord_en.Split(' ')];
        }
        /// <summary>
        /// 英语
        /// </summary>
        public class LEnglish : ILocalization
        {
            public string DataBaseFollows => "The database is as follows:";

            public string Response => "Response:";

            public string ToolCall => "Tool Call:";

            public string ToolReturn => "Tool `{0}` return:\n```\n{1}\n```\n";

            public string DiaryCompressionSystemPrompt =>
                "You are a memory organizer assistant. Summarize the provided conversation history into a concise diary entry, focusing on important events, emotions, and information. Write in first person (\"I\") and do not omit key details.";

            public string DiaryCompressionUserPrompt =>
                "Please generate a diary summary for the following conversation history:\n\n{0}";

            public string DiaryHeader => "[Historical Diary Entries]";

            public string DiaryAdjustWeightToolDesc =>
                "Adjust the importance weight of a specific diary entry. diary_id is the entry ID (shown as #<hex> in the system prompt, e.g. #1a), delta is a float adjustment (positive to increase, negative to decrease).";

            public string DiaryArgIdDesc => "Diary entry ID (the hex integer after # in the system prompt, e.g. 1a or 2b)";
            public string DiaryArgDeltaDesc => "Weight adjustment amount, positive to increase, negative to decrease, e.g. 0.5 or -0.5";

            public string[] WordSplit(string text) => text.ToLower().Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            public string[] StopWords => Properties.Resources.StopWord_en.Split(' ');
        }
        /// <summary>
        /// 繁体中文
        /// </summary>
        public class LChineseTraditional : ILocalization
        {
            public string DataBaseFollows => "數據庫如下:";

            public string Response => "回覆:";

            public string ToolCall => "工具調用:";

            public string ToolReturn => "工具 `{0}` 返回:\n```\n{1}\n```\n";

            public string DiaryCompressionSystemPrompt =>
                "你是一個記憶整理助手。請將提供的對話歷史整理成一段簡潔的日記摘要，重點記錄重要的事件、情感和資訊，使用第一人稱（「我」）撰寫，不要遺漏關鍵細節。";

            public string DiaryCompressionUserPrompt =>
                "請為以下對話歷史生成一段日記摘要：\n\n{0}";

            public string DiaryHeader => "【歷史日記記錄】";

            public string DiaryAdjustWeightToolDesc =>
                "調整指定日記條目的重要性權重。diary_id 為日記條目的 ID（見系統提示中 # 後面的十六進制整數，例如 #1a），delta 為浮點數調整量（正數增加，負數減少）。";

            public string DiaryArgIdDesc => "日記條目 ID（系統提示中 # 後面的十六進制整數，例如 1a 或 2b）";
            public string DiaryArgDeltaDesc => "權重調整量，正數增加，負數減少，例如 0.5 或 -0.5";

            public JiebaNet.Segmenter.JiebaSegmenter Segmenter = new JiebaNet.Segmenter.JiebaSegmenter();
            public string[] WordSplit(string text) => Segmenter.Cut(text).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            public string[] StopWords => [.. Properties.Resources.StopWord_zh_hant.Split(' '), .. Properties.Resources.StopWord_en.Split(' ')];

        }
    }
}
