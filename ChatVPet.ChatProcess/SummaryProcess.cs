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

    private static bool CanSummarize(Dialogue entry) => entry.Turn == null ||
        entry.Turn.OperationsFinished && !entry.Turn.PendingReply && entry.Turn.Receipts.All(x => x.IsTerminal);

    private static string SummaryInput(ConversationSummary? previous, IEnumerable<Dialogue> entries) =>
        JsonConvert.SerializeObject(new
        {
            previousSummary = previous?.Text ?? "",
            dialogues = entries.Select(x => new
            {
                x.CreatedAt, x.Question, Sequence = x.Turn?.Sequence, InitialAnswer = x.Turn?.Initial.Raw,
                Callbacks = x.Turn?.Results.Where(r => r.Reply.Length > 0).Select(r => new
                {
                    r.Sequence, HostInput = r.Input, ResultAnswer = x.MemoryAnswer.Length > 0 ? r.Reply : null
                }),
                Answer = x.Turn == null ? x.MemoryAnswer : null, Observation = x.Turn?.Observation,
                Receipts = x.Turn?.Receipts.Where(r => r.Tool is not ("历史补查" or "操作判断")).Select(r => r.ToContext())
            })
        });

    public void CompressHistory() => CompressHistory(null);

    private void CompressHistory(long? available)
    {
        if (Interlocked.Exchange(ref summaryRunning, 1) != 0) return;
        try
        {
            string saveId;
            lock (turnGate) saveId = SaveId;
            // Large old saves may need several bounded batches. Each batch releases the
            // conversation lock during generation and publishes its progress independently.
            while (CompressHistoryBatch(saveId, available)) { }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastSummaryError = "日记整理未完成，原聊天记录已保留：" + ex.Message;
        }
        finally { Volatile.Write(ref summaryRunning, 0); }
    }

    private bool CompressHistoryBatch(string saveId, long? available)
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
            var lengths = history.Select(x => x.ToMessages().Sum(t => (long)t.Content.Length)).ToArray();
            long size = lengths.Sum();
            bool overBudget = HistoryCharacterBudget > 0 && size > HistoryCharacterBudget;
            bool requestTooLarge = available.HasValue && size > available.Value;
            if (history.Count <= MaxHistoryBeforeCompress && !overBudget && !requestTooLarge) return false;
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
            if (requestTooLarge)
            {
                long remaining = lengths.Skip(count).Sum();
                while (remaining > available && count < history.Count) remaining -= lengths[count++];
            }
            limit = Math.Max(1, SummaryCharacterLimit);
            instruction = Localization.DiaryCompressionSystemPrompt +
                "\n同时更新累计前情提要。previousSummary 是上次提要，dialogues 是新整理的连续对话。只输出 JSON 对象，两个字段均为非空字符串：" +
                "\"diary\" 只记本次 dialogues 的经历；\"summary\" 合并旧提要与本次事实，保留重要偏好、约定、时间、人物、话题进度与未完成事项，省略过时琐事。" +
                $"每个字段最多 {limit} 个字符，使用原对话语言。保留否定、纠正与不确定性，新的明确事实覆盖旧事实；不要把推测写成事实。" +
                "主人明确给出的名字、偏好、更正和生活经历按 Question 记录，不需要游戏回执证明；只有宿主游戏操作的成败以 Receipts 为准。" +
                "InitialAnswer 是行动前的初答，Callbacks 按时间记录宿主 HostInput 与桌宠 ResultAnswer。Sequence 是本存档的发生顺序。HostInput 重复的问题属于同一轮。Observation 与 Receipts 来自宿主。Answer 是桌宠对白，不是权威事实。空 Answer 表示没有可采用的对白证据，不代表主人没说过或事情没发生。" +
                "桌宠说不记得、没找到或补查失败，不是否定或纠正：不得据此删除、降级为待核或覆盖 previousSummary 中已有的名字、约定和已完成操作。" +
                "保留本次与此前的时间范围，某轮没购买不等于从未购买；不得用笼统的‘以上未执行’否定此前明确成功的回执。";
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
            // Keep interleaved turns together when an older turn received a late callback.
            int safeCount = 0;
            long lastSequence = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var turn = entries[i].Turn;
                lastSequence = Math.Max(lastSequence, turn?.LastSequence ?? 0);
                if (i + 1 == history.Count || lastSequence <= (history[i + 1].Turn?.Sequence ?? 0))
                    safeCount = i + 1;
            }
            if (safeCount == 0) return false;
            entries.RemoveRange(safeCount, entries.Count - safeCount);
            input = SummaryInput(previous, entries);
            LastSummaryError = "";
        }
        // No conversation lock during network IO. Readers keep using the previous recap and tail.
        var result = summarize(instruction, [new("user", input)], []);
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
