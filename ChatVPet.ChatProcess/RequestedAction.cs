using Newtonsoft.Json;

namespace ChatVPet.ChatProcess;

// The operation model interprets language; the host checks these concrete conditions at execution time.
public class RequestedAction
{
    public string Id { get; set; } = "";
    public string Tool { get; set; } = "";
    public string Target { get; set; } = "";
    public string Evidence { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [JsonProperty("display_index")] public int? DisplayIndex { get; set; }
    [JsonProperty("max_total")] public double? MaxTotal { get; set; }
    [JsonProperty("max_unit")] public double? MaxUnit { get; set; }
    [JsonProperty("min_remaining")] public double? MinRemaining { get; set; }
    [JsonProperty("exclusive_total")] public bool ExclusiveTotal { get; set; }
    [JsonProperty("exclusive_unit")] public bool ExclusiveUnit { get; set; }
    [JsonProperty("unresolved_conditions")] public string UnresolvedConditions { get; set; } = "";

    public string CheckTarget(string target, IReadOnlyList<string> originalItems)
    {
        if (Target != target) return "目标与已确认的本轮要求不符。";
        if (DisplayIndex is int index && (index < 1 || index > originalItems.Count || originalItems[index - 1] != target))
            return "无法按原展示顺序确认目标，需要澄清。";
        return "";
    }

    public string CheckPurchase(double price, double balance, double alreadySpent)
    {
        double total = price * Quantity;
        if (!double.IsFinite(total) || total < 0 || !double.IsFinite(balance)) return "现价或余额不可用。";
        if (total > balance) return "余额不足，未购买。";
        if (MaxTotal is double budget && (ExclusiveTotal ? total + alreadySpent >= budget : total + alreadySpent > budget))
            return "超过本轮总预算，未购买。";
        if (MaxUnit is double unit && (ExclusiveUnit ? price >= unit : price > unit)) return "现价不符合单价条件，未购买。";
        if (MinRemaining is double remaining && balance - total < remaining) return "购买后的余额不符合保留金额条件，未购买。";
        return "";
    }
}

public partial class ChatTurn
{
    public List<RequestedAction> Actions { get; set; } = [];

    public HostReceipt PlanAction(Dictionary<string, string> args)
    {
        RequestedAction request;
        try { request = JsonConvert.DeserializeObject<RequestedAction>(JsonConvert.SerializeObject(args))!; }
        catch (JsonException) { return new() { Status = "skipped", Facts = "行动条件格式不完整，请重新判断或澄清。" }; }
        if (request.Id.Length == 0 || request.Evidence.Length == 0 || !Original.Contains(request.Evidence, StringComparison.Ordinal)
            || request.Quantity <= 0 || request.Tool is not ("buy_item" or "eat_item" or "start_activity" or "stop_activity")
            || request.Tool != "stop_activity" && request.Target.Length == 0
            || new[] { request.MaxTotal, request.MaxUnit, request.MinRemaining }.Any(x => x.HasValue && (!double.IsFinite(x.Value) || x.Value < 0)))
            return new() { Status = "skipped", Facts = "行动目标、数量、预算或玩家原话依据不完整，需要澄清。" };
        if (request.UnresolvedConditions.Length > 0)
            return new() { Status = "skipped", Facts = "尚未核实的条件: " + request.UnresolvedConditions + "。没有授权执行。" };
        if (Actions.Any(x => x.Id == request.Id || x.Tool == request.Tool && x.Target == request.Target))
            return new() { Status = "skipped", Facts = "该行动要求已经登记，不重复或覆盖原条件。" };
        string error = request.CheckTarget(request.Target, OriginalDisplayedItems);
        if (error.Length > 0)
            return new() { Status = "skipped", Facts = error };
        Actions.Add(request);
        return new() { Status = "observed", Facts = "已登记本轮行动条件，尚未执行: " + JsonConvert.SerializeObject(request) };
    }

    public RequestedAction? PendingAction(string tool, string id) => Actions.Find(x => x.Tool == tool && x.Id == id
        && !Receipts.Take(Receipts.Count - 1).Any(r => r.Tool == tool && r.Arguments.GetValueOrDefault("request_id") == id));
}
