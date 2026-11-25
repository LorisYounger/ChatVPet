using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChatVPet.Workbench;

internal static class DiagnosticText
{
    public static string Kind(string kind) => kind switch
    {
        "chat" => "ChatVPet 对话", "tool" => "操作模型", "summary" => "摘要模型",
        "vision" => "图片观察", "embedding" => "向量检索", "rerank" => "重排",
        "offline" => "离线演示", _ => kind
    };

    public static (string Input, string Output, string Status) Call(JToken entry)
    {
        var data = entry["data"]!;
        var text = new StringBuilder();
        if ((string?)entry["kind"] == "offline")
        {
            Message(text, 1, "system", "系统", data["system"]);
            int i = 1;
            foreach (var m in data["messages"] ?? new JArray())
                Message(text, ++i, (string?)m["Role"], (bool?)m["FromHost"] == true ? "宿主" : null, m["Content"]);
            return (text.ToString(), (string?)data["reply"] ?? "此条旧记录未保存模拟返回。", "离线模拟，本次没有发送 HTTP 请求。");
        }
        var request = data["Request"]!;
        int index = 0;
        foreach (var m in request["messages"] ?? new JArray())
        {
            var sources = data["Sources"] as JArray;
            string? source = sources != null && index < sources.Count ? (string?)sources[index] : null;
            Message(text, ++index, (string?)m["role"], source, m["content"]);
        }
        if (request["messages"] == null)
        {
            Section(text, "查询", (string?)request["query"]);
            Section(text, "输入文本", Fields(request["input"] ?? request["documents"]));
        }
        if (request["tools"] is JArray tools)
            foreach (var tool in tools)
                Section(text, "可用工具 · " + tool["function"]?["name"],
                    tool["function"]?["description"] + "\n" + Fields(tool["function"]?["parameters"]));

        bool finished = (bool?)data["Finished"] == true;
        string status = $"{data["Model"]} · " + (finished ? $"HTTP {data["StatusCode"]} · {(double?)data["Milliseconds"]:F0} ms" : "已准备请求，尚无返回");
        return (text.ToString(), Response(entry), status);
    }

    public static string Response(JToken entry)
    {
        var data = entry["data"]!;
        if ((string?)entry["kind"] == "offline") return (string?)data["reply"] ?? "此条旧记录未保存模拟返回。";
        bool finished = (bool?)data["Finished"] == true;
        var raw = (string?)data["Response"];
        var output = new StringBuilder();
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                var response = JObject.Parse(raw);
                if (response["choices"] is JArray choices && choices.Count > 0)
                {
                    var message = choices[0]["message"];
                    if (message?["content"] is { Type: JTokenType.String } content)
                        output.Append(content.Value<string>());
                    foreach (var call in message?["tool_calls"] ?? new JArray())
                    {
                        var args = (string?)call["function"]?["arguments"] ?? "";
                        try { args = Fields(JToken.Parse(args)); } catch (JsonException) { }
                        Section(output, "调用工具 · " + call["function"]?["name"], args);
                    }
                }
                else output.Append(Fields(response));
            }
            catch (JsonException) { output.Append(raw); }
        }
        Section(output, "请求错误", (string?)data["Error"]);
        if (output.Length == 0) output.Append(finished ? "本次没有返回正文。" : "尚未收到返回。");
        return output.ToString();
    }

    private static void Message(StringBuilder text, int number, string? role, string? source, JToken? content)
    {
        if (text.Length > 0) text.AppendLine().AppendLine();
        text.AppendLine($"{number:D2}  {role}" + (string.IsNullOrEmpty(source) ? "" : " · " + source));
        if (content is JArray parts)
            text.Append(string.Join("\n", parts.Select(p => (string?)p["type"] == "image_url" ? "〔本轮附图〕" : (string?)p["text"])));
        else text.Append((string?)content);
    }

    private static void Section(StringBuilder text, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (text.Length > 0) text.AppendLine().AppendLine();
        text.AppendLine(label).Append(value);
    }

    public static string Context(JToken snapshot)
    {
        var state = snapshot["checkpoint"];
        var turn = state?["dialogues"]?.Last?["Turn"];
        var text = new StringBuilder();
        Section(text, "当前桌宠", Host(state?["host"]));
        Section(text, "对话摘要", string.Join("\n", (state?["summaries"] ?? new JArray()).Select(s => (string?)s["Text"])));
        Section(text, "本轮初始输入", (string?)turn?["InitialInput"]);
        foreach (var result in turn?["Results"] ?? new JArray())
        {
            Section(text, "宿主续答输入", (string?)result["Input"]);
            Section(text, "续答正文", (string?)result["Reply"]);
        }
        foreach (var receipt in turn?["Receipts"] ?? new JArray())
            Section(text, "工具回执 · " + receipt["Tool"], receipt["Status"] + "  " + receipt["Facts"]);
        if (state?["settings"] is JObject settings)
            Section(text, "上下文预算", $"压缩阈值 {settings["MaxHistoryBeforeCompress"]} 轮，保留 {settings["DiaryKeepRecentCount"]} 轮\n历史 {settings["HistoryCharacterBudget"]} 字符，参考 {settings["ContextCharacterBudget"]} 字符，请求上限 {settings["RequestCharacterLimit"]} 字符");
        if (state?["retrieval"] is JObject retrieval)
            Section(text, "检索统计", $"向量请求 {retrieval["EmbeddingRequests"]} 次，重排请求 {retrieval["RerankRequests"]} 次\n缓存命中 {retrieval["CacheHits"]} 次，未命中 {retrieval["CacheMisses"]} 次");
        Section(text, "摘要错误", (string?)state?["LastSummaryError"]);
        Section(text, "检索错误", (string?)state?["LastRetrievalError"]);
        return text.ToString();
    }

    private static string Host(JToken? host)
    {
        if (host == null) return "";
        var items = (host["inventory"] ?? new JArray()).Select(x => $"{x["Name"]} × {x["Count"]}").ToList();
        return $"{host["petName"]}  {host["ownerName"]}\n余额 {host["money"]}  ·  饱腹 {host["food"]}  ·  心情 {host["feeling"]}  ·  好感 {host["affinity"]}\n背包  " +
            (items.Count == 0 ? "空" : string.Join("、", items));
    }

    public static string Config(JToken snapshot)
    {
        var opts = snapshot["options"]!;
        var api = snapshot["configuration"];
        var host = snapshot["checkpoint"]?["host"];
        return $"会话 API · {opts["Config"]}\n\n对话  {api?["Model"]}\n{api?["ApiUrl"]}\n\n操作 / 视觉 / 摘要  {api?["ToolModel"]}\n{api?["ToolApiUrl"]}\n\n向量  {api?["EmbeddingModel"]}\n{api?["EmbeddingApiUrl"]}\n\n重排  {api?["RerankModel"]}\n{api?["RerankApiUrl"]}\n\nMOD 来源  {Source(opts["ModDirectory"], "内置测试商品")}\n存档来源  {Source(opts["SaveFile"], "内置测试存档")}\n" +
            (host?["catalogCount"] == null ? "" : $"商品 {host["catalogCount"]} 种  ·  活动 {host["workCount"]} 项");
    }
    private static string Source(JToken? value, string fallback) => string.IsNullOrEmpty((string?)value) ? fallback : (string)value!;

    public static string Event(JToken entry)
    {
        var data = entry["data"]!;
        if (data is JObject obj && (obj["Request"] != null || (string?)entry["kind"] == "offline"))
        {
            var call = Call(entry);
            return $"{call.Status}\n\n发送内容\n{call.Input}\n\n模型返回\n{call.Output}";
        }
        switch ((string?)entry["kind"])
        {
            case "retrieval":
                var text = new StringBuilder();
                text.AppendLine($"{data["kind"]} · 候选 {data["candidateCount"]} · 上限 {data["limit"]}").AppendLine((string?)data["query"]);
                foreach (var item in data["ranked"] ?? new JArray())
                    Section(text, $"{((bool?)item["selected"] == true ? "入选" : "未选")}  召回 {item["recallScore"]}  重排 {item["rerankScore"]}", (string?)item["text"]);
                Section(text, "检索错误", (string?)data["error"]);
                return text.ToString();
            case "assistant": case "display": return (string?)entry["title"] ?? "";
            case "user": return data["text"] + "\n\n" + Host(data["before"]);
            case "turn":
                var turn = data["turn"];
                return $"{turn?["Original"]}\n\n{turn?["Initial"]?["Raw"]}\n\n" + string.Join("\n", (turn?["Receipts"] ?? new JArray()).Select(r => r["Tool"] + "  " + r["Facts"])) + "\n" + Host(data["after"]);
            case "review": return $"预期\n{data["expected"]}\n\n实际回复\n{data["actual"]}\n\n{Host(data["after"])}";
            default: return Fields(data);
        }
    }

    private static string Fields(JToken? node) => node switch
    {
        null => "",
        JObject obj => string.Join("\n", obj.Properties().Where(p => p.Value.Type != JTokenType.Null)
            .Select(p => p.Name + "  " + Fields(p.Value))),
        JArray array => string.Join("\n", array.Select(Fields)),
        _ => node.ToString()
    };
}
