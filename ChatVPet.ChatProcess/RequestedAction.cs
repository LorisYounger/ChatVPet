using Newtonsoft.Json;

namespace ChatVPet.ChatProcess;

public class RequestedAction
{
    public string Target { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [JsonProperty("display_index")] public int? DisplayIndex { get; set; }
    [JsonProperty("max_total")] public double? MaxTotal { get; set; }
    [JsonProperty("max_unit")] public double? MaxUnit { get; set; }
    [JsonProperty("min_remaining")] public double? MinRemaining { get; set; }
    [JsonProperty("exclusive_total")] public bool ExclusiveTotal { get; set; }
    [JsonProperty("exclusive_unit")] public bool ExclusiveUnit { get; set; }

    public static bool TryParse(Dictionary<string, string> args, out RequestedAction action, out string error)
    {
        try { action = JsonConvert.DeserializeObject<RequestedAction>(JsonConvert.SerializeObject(args)) ?? new(); }
        catch (JsonException) { action = new(); error = "行动参数格式不完整，请核对后重试。"; return false; }
        static bool InvalidLimit(double? value) => value is double d && (!double.IsFinite(d) || d < 0);
        if (action.Target.Length == 0 || action.Quantity <= 0
            || InvalidLimit(action.MaxTotal) || InvalidLimit(action.MaxUnit) || InvalidLimit(action.MinRemaining))
        {
            error = "行动目标、数量或预算条件无效。";
            return false;
        }
        error = "";
        return true;
    }

    public string CheckTarget(string target, IReadOnlyList<string> originalItems)
    {
        if (Target != target) return "目标与要求不符。";
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
