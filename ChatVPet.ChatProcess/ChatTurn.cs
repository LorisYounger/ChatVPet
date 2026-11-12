namespace ChatVPet.ChatProcess;

public partial class ChatTurn
{
    public string SaveId { get; set; } = "";
    public string TurnId { get; set; } = Guid.NewGuid().ToString("N");
    public string Original { get; set; } = "";
    public string System { get; set; } = "";
    public string State { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Observation { get; set; } = "";
    public string InitialInput { get; set; } = "";
    public string ResultInput { get; set; } = "";
    public ChatHeader Initial { get; set; } = new();
    public List<string[]> History { get; set; } = [];
    // Visible order is independent of retrieval ranking.
    public List<string> DisplayedItems { get; set; } = [];
    public List<string> OriginalDisplayedItems { get; set; } = [];
    public List<int> UsedDiaryIds { get; set; } = [];
    public List<HostReceipt> Receipts { get; set; } = [];
    public int ReplyRevision { get; set; }
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
    public string ToContext() => $"{Tool}: {Status switch { "observed" => "已查到", "applied" => "已完成", "skipped" => "未执行", "failed" => "失败", _ => "尚未确认" }}; {Facts}";
}
