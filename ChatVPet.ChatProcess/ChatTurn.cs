namespace ChatVPet.ChatProcess;

public sealed record ChatMessage(string Role, string Content, bool FromHost = false);

public sealed class ResultExchange
{
    public long Sequence { get; set; }
    public int Revision { get; set; }
    public string Input { get; set; } = "";
    public string Reply { get; set; } = "";
}

public partial class ChatTurn
{
    public string SaveId { get; set; } = "";
    public string TurnId { get; set; } = Guid.NewGuid().ToString("N");
    public long Sequence { get; set; }
    public string Original { get; set; } = "";
    public string System { get; set; } = "";
    public string Summary { get; set; } = "";
    public string State { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Observation { get; set; } = "";
    public string InitialInput { get; set; } = "";
    public List<ResultExchange> Results { get; set; } = [];
    public string ResultReference { get; set; } = "";
    public ChatHeader Initial { get; set; } = new();
    public List<ChatMessage> History { get; set; } = [];
    // Visible order is independent of retrieval ranking.
    public List<string> DisplayedItems { get; set; } = [];
    public List<string> OriginalDisplayedItems { get; set; } = [];
    public List<int> UsedDiaryIds { get; set; } = [];
    public List<HostReceipt> Receipts { get; set; } = [];
    [Newtonsoft.Json.JsonIgnore]
    public bool PendingReply => OperationsFinished && !QuitCompleted && (Initial.NeedsResult || Initial.Has("quit"))
        && PresentedRevision != ReceiptRevision;

    internal long LastSequence => Results.LastOrDefault(r => r.Reply.Length > 0)?.Sequence ?? Sequence;

    internal IEnumerable<(long Order, ChatMessage Message)> OrderedMessages()
    {
        yield return (Sequence, new("user", InitialInput));
        yield return (Sequence, new("assistant", Initial.Raw));
        foreach (var result in Results.Where(x => x.Reply.Length > 0))
        {
            yield return (result.Sequence, new("user", result.Input, FromHost: true));
            yield return (result.Sequence, new("assistant", result.Reply));
        }
    }
    public int PresentedRevision { get; set; }
    public int ReceiptRevision { get; set; }
    public bool OperationsFinished { get; set; }
    public bool InitialPresented { get; set; }
    public bool QuitCompleted { get; set; }
}

public class HostReceipt
{
    public double? MoneySpent { get; set; }
    public string SaveId { get; set; } = "";
    public string TurnId { get; set; } = "";
    public string StepId { get; set; } = "";
    public string Tool { get; set; } = "";
    public Dictionary<string, string> Arguments { get; set; } = [];
    public string Status { get; set; } = "unknown";
    public string Facts { get; set; } = "";
    public List<string> DisplayedItems { get; set; } = [];
    public bool IsTerminal => Status != "unknown";
    public string ToContext() => Tool == "plan_action" && Status == "observed"
        ? "行动条件已登记，登记时尚未执行。"
        : $"{Tool}: {Status switch { "observed" => "已查到", "applied" => "已完成", "skipped" => "未执行", "failed" => "失败", "not_found" => "未找到", _ => "尚未确认" }}; {Facts}";
}
