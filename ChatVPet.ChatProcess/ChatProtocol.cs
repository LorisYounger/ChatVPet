using System.Text;
using System.Text.RegularExpressions;

namespace ChatVPet.ChatProcess;

public class ChatHeader
{
    public string Raw { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Expression { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Unknown { get; set; } = [];
    public bool Has(string tag) => Tags.Contains(tag);
    public bool NeedsTools => Has("delegate") || Has("mood") || Has("affinity") || Has("animation") || Unknown.Count > 0;
    public bool NeedsResult => Has("delegate") || Has("recall") || Has("screenshot") || Unknown.Count > 0;
}

public static class ChatProtocol
{
    private static readonly string[] Order = ["delegate", "mood", "affinity", "expression", "recall", "screenshot", "animation", "quit"];
    private static readonly string[] Expressions = ["affectionate", "angry", "cheerful", "conscientious", "disgruntled", "embarrassed", "fearful", "gentle", "sad", "serious"];
    private static readonly Regex HeaderToken = new(@"\G\[([a-z][a-z0-9_]*)(?::([a-z]+))?\]", RegexOptions.Compiled);
    private static readonly string Template = ReadTemplate();

    private static string ReadTemplate()
    {
        using var stream = typeof(ChatProtocol).Assembly.GetManifestResourceStream("ChatVPet.SystemTemplate")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string RenderSystem(string? name = null, string? hostName = null) => Template
        .Replace("{name}", string.IsNullOrWhiteSpace(name) ? "萝莉斯" : name)
        .Replace("{hostname}", string.IsNullOrWhiteSpace(hostName) ? "主人" : hostName);

    public static bool TryParse(string output, bool continuation, out ChatHeader header)
    {
        header = new ChatHeader { Raw = output };
        int newline = output.IndexOf('\n');
        if (newline < 0) return false;
        string line = output[..newline].TrimEnd('\r');
        header.Body = output[(newline + 1)..].Trim();
        if (header.Body.Length == 0) return false;
        string firstBodyLine = header.Body.Split('\n', 2)[0].TrimEnd('\r');
        if (Regex.IsMatch(firstBodyLine, @"^(?:\[[a-z][a-z0-9_]*(?::[a-z]+)?\])+$")) return false;
        int position = 0, last = -1;
        while (position < line.Length)
        {
            var match = HeaderToken.Match(line, position);
            if (!match.Success) return false;
            string tag = match.Groups[1].Value;
            if (header.Tags.Contains(tag)) return false;
            header.Tags.Add(tag);
            int order = Array.IndexOf(Order, tag);
            if (order >= 0)
            {
                if (order <= last) return false;
                last = order;
            }
            else
                header.Unknown.Add(match.Value);
            if (tag == "expression")
            {
                header.Expression = match.Groups[2].Value;
                if (!Expressions.Contains(header.Expression)) return false;
            }
            else if (match.Groups[2].Success) return false;
            position += match.Length;
        }
        if (continuation) return header.Tags.Count == 1 && header.Expression != null;
        if (header.Expression == null && !header.Has("animation")) return false;
        return !header.Has("quit") || (header.Expression != null && header.Tags.Count == 2);
    }

    public static string Field(string text) => text.Replace('[', '(').Replace(']', ')')
        .Replace('\r', ' ').Replace('\n', ' ');

    public static string UserMessage(string original, string state = "", string reference = "", string observation = "", string initial = "", string receipt = "")
    {
        var prefix = new StringBuilder();
        if (state.Length > 0) prefix.Append('[').Append(Field(state)).Append(']');
        foreach (var field in new[] { ("参考", reference), ("观察", observation), ("初答", initial), ("回执", receipt) })
            if (field.Item2.Length > 0) prefix.Append('[').Append(field.Item1).Append(':').Append(Field(field.Item2)).Append(']');
        return prefix.Length == 0 ? original : prefix.Append('\n').Append(original).ToString();
    }

    public static string AffinityTier(double value) => value switch
    {
        >= 30000 => "深爱", >= 10000 => "挚爱", >= 3000 => "依恋", >= 1000 => "亲密", >= 500 => "亲近",
        >= 200 => "喜欢", >= 100 => "有好感", >= 51 => "熟悉", >= 11 => "初识", _ => "陌生"
    };

    public static double AffinityTarget(double current, double maximum, int strength)
    {
        var cap = Math.Min(Math.Floor(maximum), Math.Floor(2 + 0.03 * maximum));
        var points = Math.Floor(cap * Math.Abs(strength) / 100 + 0.5);
        return Math.Clamp(current + Math.Sign(strength) * points, 0, maximum);
    }
}
