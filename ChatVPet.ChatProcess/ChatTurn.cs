using System.Globalization;

namespace ChatVPet.ChatProcess;

public sealed record ChatMessage(string Role, string Content, bool FromHost = false);

public sealed class ResultExchange
{
    public long Sequence { get; set; }
    public int Revision { get; set; }
    public string TrainingInput { get; set; } = "";
    public string Input { get; set; } = "";
    public string Reply { get; set; } = "";
}

public partial class ChatTurn
{
    public string TurnId { get; set; } = Guid.NewGuid().ToString("N");
    public long Sequence { get; set; }
    public string TrainingProtocolVersion { get; set; } = "";
    public bool Allow { get; set; }
    public bool InputAuthorized { get; set; }
    public bool ResultsAuthorized { get; set; }
    public int ToolRound { get; set; }
    public string WaitingStep { get; set; } = "";
    public string EffectiveCandidate { get; set; } = "";
    public int AnswerVersion { get; set; }
    public string FirstReply { get; set; } = "";
    public string ProcessVersion { get; set; } = "";
    public string Template { get; set; } = "";
    public string Language { get; set; } = "";
    public string ChatModel { get; set; } = "";
    public string ToolModel { get; set; } = "";
    public string Original { get; set; } = "";
    public string System { get; set; } = "";
    public string Summary { get; set; } = "";
    public string State { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Observation { get; set; } = "";
    public string Source { get; set; } = "";
    public string HostEvents { get; set; } = "";
    public string InitialInput { get; set; } = "";
    public string TrainingInitialInput { get; set; } = "";
    public List<ResultExchange> Results { get; set; } = [];
    public int ConclusionRevision { get; set; }
    public string ResultReference { get; set; } = "";
    public ChatHeader Initial { get; set; } = new();
    public List<ChatMessage> History { get; set; } = [];
    public List<ChatMessage>? TrainingHistory { get; set; }
    // Visible order is independent of retrieval ranking.
    public List<string> DisplayedItems { get; set; } = [];
    public List<string> OriginalDisplayedItems { get; set; } = [];
    public List<int> UsedDiaryIds { get; set; } = [];
    public List<HostReceipt> Receipts { get; set; } = [];
    public IEnumerable<string> GetWrittenFiles()
    {
        return Receipts.Where(r => r.Tool == "write_file" && r.Status == "applied")
            .Select(r => r.WrittenFile ?? PreviousWritePath(r.Facts))
            .OfType<string>().Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);

        // Earlier hosts only saved the final absolute path in the successful write receipt.
        static string? PreviousWritePath(string facts)
        {
            const string prefix = "已写入 ", suffix = " 个字符）。";
            int countStart = facts.LastIndexOf('（');
            if (!facts.StartsWith(prefix, StringComparison.Ordinal) || !facts.EndsWith(suffix, StringComparison.Ordinal)
                || countStart <= prefix.Length || countStart >= facts.Length - suffix.Length
                || !int.TryParse(facts.AsSpan(countStart + 1, facts.Length - suffix.Length - countStart - 1),
                    NumberStyles.None, CultureInfo.InvariantCulture, out _)) return null;
            return facts[prefix.Length..countStart];
        }
    }
    [Newtonsoft.Json.JsonIgnore]
    public bool PendingReply => OperationsFinished && !QuitCompleted && (Initial.NeedsResult || Initial.Has("quit"))
        && PresentedRevision != ReceiptRevision;

    internal long LastSequence => Results.LastOrDefault(r => r.Reply.Length > 0)?.Sequence ?? Sequence;

    internal string HistoryInitialInput() =>
        ChatProtocol.UserMessage(Original, State, observation: Observation, language: Language, events: HostEvents);

    internal string HistoryResultInput(ResultExchange result)
    {
        if (result.TrainingInput.Length > 0) return result.TrainingInput;
        if (ResultReference.Length > 0)
            return ChatProtocol.UserMessage(Original, State, initial: Initial.Body, receipt: ResultReference, language: Language);
        return result.Input.Length > 800 ? result.Input[..800] + "…" : result.Input;
    }

    internal IEnumerable<(long Order, ChatMessage Message)> OrderedMessages(bool compactHistory = false)
    {
        yield return (Sequence, new("user", compactHistory ? HistoryInitialInput() : InitialInput));
        yield return (Sequence, new("assistant", Initial.Raw));
        foreach (var result in Results.Where(x => x.Reply.Length > 0))
        {
            yield return (result.Sequence, new("user", compactHistory ? HistoryResultInput(result) : result.Input, FromHost: true));
            yield return (result.Sequence, new("assistant", result.Reply));
        }
    }
    public int PresentedRevision { get; set; }
    public int ReceiptRevision { get; set; }
    public bool OperationsFinished { get; set; }
    public bool InitialPresented { get; set; }
    public bool QuitCompleted { get; set; }
}

public sealed class ReplyDisplay
{
    public string Animation { get; set; } = "";
    public bool Fallback { get; set; }
}

public class HostReceipt
{
    public string? WrittenFile { get; set; }
    public double? MoneySpent { get; set; }
    public string TurnId { get; set; } = "";
    public string StepId { get; set; } = "";
    public string Tool { get; set; } = "";
    public Dictionary<string, string> Arguments { get; set; } = [];
    public string Status { get; set; } = "unknown";
    public string Facts { get; set; } = "";
    public List<string> DisplayedItems { get; set; } = [];
    public bool IsTerminal => Status != "unknown";
    public string ToContext(string? language = null) =>
        $"{Tool}: {ChatProtocol.ReceiptStatusLabel(Status, language)}; {Facts}";
}
