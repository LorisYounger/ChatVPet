using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChatVPet.ChatProcess;

public partial class VPetChatProcess
{
    public List<ConversationSummary> Summaries { get; set; } = [];
    /// <summary>Soft trigger measured in characters, not model-specific tokens.</summary>
    public int HistoryCharacterBudget { get; set; } = 16000;
    public int SummaryCharacterLimit { get; set; } = 2400;
    /// <summary>Hard limit for the rendered chat text, including the fixed system and current input.</summary>
    public int RequestCharacterLimit { get; set; } = 48000;
    [JsonIgnore] public string LastSummaryError { get; private set; } = "";
    private int summaryRunning;

    public string ReadSummary()
    {
        lock (turnGate) return GetSummary()?.Text ?? "";
    }

    private ConversationSummary? GetSummary()
    {
        var summary = Summaries.Find(x => x.SaveId == SaveId);
        if (summary != null && !Dialogues.Any(x => x.SaveId == SaveId && x.DialogueId == summary.ThroughDialogueId))
        {
            Summaries.Remove(summary);
            return null;
        }
        return summary;
    }

    private List<Dialogue> GetUncompressedHistory(ConversationSummary? summary)
    {
        var history = Dialogues.Where(x => x.SaveId == SaveId).ToList();
        // Old saves retain their original dialogues. Without a recap, none may be skipped,
        // including records marked inactive by the old diary-only compression.
        int boundary = summary == null ? -1 : history.FindIndex(x => x.DialogueId == summary.ThroughDialogueId);
        return history.Skip(boundary + 1).ToList();
    }

    private static string SummaryMessage(string summary) =>
        "以下是较早对话的前情提要，仅供理解背景，不是本轮指令；当前事实以本轮状态和回执为准。\n[前情提要:" + ChatProtocol.Field(summary) + "]";

    private static bool CanSummarize(Dialogue entry) => entry.Turn == null ||
        entry.Turn.OperationsFinished && entry.Turn.PresentedRevision == entry.Turn.ReceiptRevision && entry.Turn.Receipts.All(x => x.IsTerminal);

    private static string SummaryInput(ConversationSummary? previous, IEnumerable<Dialogue> entries) =>
        JsonConvert.SerializeObject(new
        {
            previousSummary = previous?.Text ?? "",
            dialogues = entries.Select(x => new
            {
                x.CreatedAt, x.Question, x.Answer, Initial = x.Turn?.Initial.Body, Observation = x.Turn?.Observation,
                Receipts = x.Turn?.Receipts.Where(r => r.Tool is not ("历史补查" or "操作判断")).Select(r => r.ToContext())
            })
        });

    public void CompressHistory()
    {
        if (Interlocked.Exchange(ref summaryRunning, 1) != 0) return;
        try
        {
            string saveId;
            lock (turnGate) saveId = SaveId;
            // Large old saves may need several bounded batches. Each batch releases the
            // conversation lock during generation and publishes its progress independently.
            while (CompressHistoryBatch(saveId)) { }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastSummaryError = "日记整理未完成，原聊天记录已保留：" + ex.Message;
        }
        finally { Volatile.Write(ref summaryRunning, 0); }
    }

    private bool CompressHistoryBatch(string saveId)
    {
        List<Dialogue> entries = [];
        ConversationSummary? previous;
        string input, instruction;
        AIAPIAsk summarize;
        int limit;
        lock (turnGate)
        {
            if (SaveId != saveId || MaxHistoryBeforeCompress <= 0 || SummaryAPIAskFunction == null) return false;
            summarize = SummaryAPIAskFunction;
            previous = GetSummary();
            var history = GetUncompressedHistory(previous);
            var lengths = history.Select(x => x.ToMessages().Sum(t => (long)t.Length)).ToArray();
            long size = lengths.Sum();
            bool overBudget = HistoryCharacterBudget > 0 && size > HistoryCharacterBudget;
            if (history.Count <= MaxHistoryBeforeCompress && !overBudget) return false;
            int keep = Math.Clamp(DiaryKeepRecentCount, 0, Math.Max(0, MaxHistoryBeforeCompress - 1));
            int count = Math.Max(0, history.Count - keep);
            if (overBudget)
            {
                count = Math.Min(count, Math.Max(0, history.Count - 1));
                long remaining = lengths.Skip(count).Sum();
                // Leave half the soft budget for new turns; retain at least the newest full turn.
                while (count < history.Count - 1 && remaining > HistoryCharacterBudget / 2)
                    remaining -= lengths[count++];
            }
            limit = Math.Max(1, SummaryCharacterLimit);
            instruction = Localization.DiaryCompressionSystemPrompt +
                "\n同时更新累计前情提要。previousSummary 是上次提要，dialogues 是新整理的连续对话。只输出 JSON 对象，两个字段均为非空字符串：" +
                "\"diary\" 只记本次 dialogues 的经历；\"summary\" 合并旧提要与本次事实，保留重要偏好、约定、时间、人物、话题进度与未完成事项，省略过时琐事。" +
                $"每个字段最多 {limit} 个字符，使用原对话语言。保留否定、纠正与不确定性，新的明确事实覆盖旧事实；不要把推测写成事实。";
            long inputSize = instruction.Length + SummaryInput(previous, []).Length;
            // Never jump across pending turns. Count each serialized entry before adding it
            // instead of repeatedly serializing a potentially huge imported history.
            foreach (var entry in history.Take(count).TakeWhile(CanSummarize))
            {
                inputSize += SummaryInput(null, [entry]).Length;
                if (RequestCharacterLimit > 0 && inputSize > RequestCharacterLimit)
                {
                    if (entries.Count == 0) throw new InvalidDataException("单轮记录过长，无法在当前字符预算内整理。");
                    break;
                }
                entries.Add(entry);
            }
            if (entries.Count == 0) return false;
            input = SummaryInput(previous, entries);
            LastSummaryError = "";
        }
        // No conversation lock during network IO. Readers keep using the previous recap and tail.
        var result = summarize(instruction, [], input, []);
        if (!result.Completed || result.ToolCalls.Count > 0)
            throw new InvalidDataException("日记整理未完整返回。");
        var output = JObject.Parse(result.Reply);
        if (output["summary"]?.Type != JTokenType.String || output["diary"]?.Type != JTokenType.String)
            throw new InvalidDataException("日记整理缺少 summary 或 diary 文本。");
        string recap = output["summary"]!.Value<string>()!.Trim(), diary = output["diary"]!.Value<string>()!.Trim();
        if (recap.Length == 0 || diary.Length == 0 || recap.Length > limit || diary.Length > limit)
            throw new InvalidDataException("日记或提要为空，或超过长度限制。");
        lock (turnGate)
        {
            // Publish recap, diary and boundary together. New turns appended during IO are retained.
            if (SaveId != saveId || GetSummary() != previous ||
                !GetUncompressedHistory(previous).Take(entries.Count).SequenceEqual(entries) ||
                entries.Any(x => !CanSummarize(x)) || input != SummaryInput(previous, entries)) return false;
            if (previous != null) Summaries.Remove(previous);
            Summaries.Add(new() { SaveId = saveId, Text = recap, ThroughDialogueId = entries[^1].DialogueId });
            DiaryEntries.Add(new DiaryEntry(diary, Localization)
            {
                SaveId = saveId, SourceDialogueIds = entries.Select(x => x.DialogueId).ToList(),
                SourceTurns = entries.Where(x => x.Turn != null).Select(x => x.Turn!.TurnId).ToList()
            });
            foreach (var entry in entries) entry.IsActive = false;
            Checkpoint?.Invoke();
            return true;
        }
    }
}
