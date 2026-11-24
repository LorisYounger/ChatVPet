using ChatVPet.ChatProcess;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException(name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
var promptDirectory = Path.Combine(AppContext.BaseDirectory, "Prompts");
var protocolBlocks = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(promptDirectory, "protocol.md")), @"```text\s*\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline);
for (int index = 0; index < 2; index++)
{
    bool specialized = index == 1;
    string template = File.ReadAllText(Path.Combine(promptDirectory, specialized ? "system-chatvpet-template.txt" : "system-process-template.txt")).Replace("\r\n", "\n").Trim();
    Check(protocolBlocks[index].Groups[1].Value.Replace("\r\n", "\n").Trim() == template, $"Protocol document matches {(specialized ? "ChatVPet" : "full")} template");
    Check(ChatProtocol.RenderSystem("咪露", "小林", specialized).Replace("\r\n", "\n").Trim() == template.Replace("{name}", "咪露").Replace("{hostname}", "小林"), $"Embedded template renders configured identity ({specialized})");
}
Check(ChatProtocol.RenderSystem() != ChatProtocol.RenderSystem(useChatVPetPrompt: true), "ChatVPet selection uses its own template");
Check(ChatProtocol.RenderSystem(useChatVPetPrompt: true).StartsWith("你叫萝莉斯,") && ChatProtocol.RenderSystem(useChatVPetPrompt: true).Contains("称呼主人为“主人”"), "ChatVPet template renders default identity");

VPetChatProcess NewProcess() => new()
{
    Localization = new ILocalization.LEnglish(), SystemDescription = "fixed instructions",
    MaxHistoryCount = 1, MaxHistoryBeforeCompress = 3, DiaryKeepRecentCount = 1,
    MemoryLimit = 0, MaxKnowledgeCount = 0,
    AIAPIAskFunction = (_, _, _, _) => new() { Reply = "[expression:gentle]\nanswer" }
};
VPetChatProcess.AIAPIAskResult Summary(string text) => new()
{ Reply = JsonConvert.SerializeObject(new { summary = text, diary = "this batch diary" }) };
void Ask(VPetChatProcess process, string message) => process.Ask(message, _ => { }, new());
Dialogue Entry(VPetChatProcess process, string question) => new(question, "answer", process.Localization) { SaveId = process.SaveId };

var p = NewProcess();
var requests = new List<(string System, List<string[]> History, string Input)>();
var summaryInputs = new List<string>();
p.SummaryAPIAskFunction = (_, _, input, _) => { summaryInputs.Add(input); return Summary("recap " + summaryInputs.Count); };
p.AIAPIAskFunction = (system, history, input, _) =>
{
    requests.Add((system, history, input));
    return new() { Reply = "[expression:gentle]\nanswer" };
};
for (int i = 1; i <= 4; i++) { p.HostState = "changing state " + i; Ask(p, "question " + i); }
Check(requests[3].History.Count == 3, "History grows beyond the old one-turn sliding limit");
Check(requests[2].History[0].SequenceEqual(requests[3].History[0]), "Old history messages remain byte-for-byte stable");
Check(requests.All(x => x.System == "fixed instructions"), "Dynamic state never changes fixed instructions");
Check(!JsonConvert.SerializeObject(requests[3].History).Contains("changing state"), "Past state snapshots are not repeated in history");
Ask(p, "question 5");
Check(p.Summaries.Count == 1 && p.DiaryEntries.Count == 1, "One compression publishes both recap and diary");
Check(requests[4].History.Count == 1 && requests[4].History[0][0] == "question 4", "Raw tail starts exactly after the summary boundary");
Check(requests[4].Input.Contains("[参考:较早对话提要") && requests[4].Input.Contains("recap 1]") && !JsonConvert.SerializeObject(requests[4].History).Contains("recap 1"), "Summary is injected once in the current reference, never as historical dialogue");
Check(p.Dialogues.Count == 5 && p.Dialogues.Take(3).All(x => !x.IsActive), "Compression keeps source records for recall");
Ask(p, "question 6");
Check(summaryInputs.Count == 1 && requests[4].History.SelectMany(x => x).SequenceEqual(requests[5].History.Take(1).SelectMany(x => x)), "Between compressions raw history stays stable while current reference carries the summary");
Ask(p, "question 7"); Ask(p, "question 8");
Check(summaryInputs.Count == 2 && JObject.Parse(summaryInputs[1])["previousSummary"]!.Value<string>() == "recap 1", "Next compression receives the previous cumulative recap");
Check(JObject.Parse(summaryInputs[1])["dialogues"]!.Count() == 3 && !summaryInputs[1].Contains("question 1"), "Each compression only summarizes the new consecutive batch");
Check(requests[^1].History[0][0] == "question 7", "Second compression advances the boundary without a gap");

// Persist exactly the fields stored by the plugin, then reconstruct the process.
string saved = JsonConvert.SerializeObject(new { dialogues = p.Dialogues, diaries = p.DiaryEntries, summaries = p.Summaries });
var data = JObject.Parse(saved);
var restored = NewProcess();
restored.Dialogues = data["dialogues"]!.ToObject<List<Dialogue>>()!;
restored.DiaryEntries = data["diaries"]!.ToObject<List<DiaryEntry>>()!;
restored.Summaries = data["summaries"]!.ToObject<List<ConversationSummary>>()!;
Ask(restored, "after restart");
Check(restored.Dialogues[^1].Turn!.Summary == "recap 2" && restored.Dialogues[^1].Turn!.History[0][0] == "question 7", "Restart preserves both the recap and its exact history boundary");
Check(restored.Dialogues[0].DialogueId == p.Dialogues[0].DialogueId, "Source identifiers survive serialization");
restored.SaveId = "another-save";
Ask(restored, "other save");
Check(restored.Dialogues[^1].Turn!.Summary == "" && restored.Dialogues[^1].Turn!.History.Count == 0, "A different save receives neither recap nor history from the first save");
restored.SaveId = "default";
restored.DeleteMemory(restored.Dialogues[0]);
Check(restored.ReadSummary() == "" && restored.DiaryEntries.Count == 1, "Deleting a covered source invalidates the cumulative recap and its own diary");
Ask(restored, "after deletion");
Check(restored.Dialogues[^1].Turn!.History[0][0] == "question 2", "After invalidation surviving source history is available for rebuilding");

p = NewProcess();
p.Dialogues = JsonConvert.DeserializeObject<List<Dialogue>>("[{\"Question\":\"old archived\",\"Answer\":\"old answer\",\"IsActive\":false},{\"Question\":\"recent\",\"Answer\":\"answer\"}]")!;
Ask(p, "legacy save");
Check(p.Dialogues[^1].Turn!.History[0][0] == "old archived", "Old diary-only archives are not lost when no cumulative recap exists");
p.MaxHistoryBeforeCompress = 0;
Ask(p, "disabled");
Check(p.Dialogues[^1].Turn!.History.Count == 1, "Explicitly disabling compression restores the configured recent-turn limit");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(1, 4).Select(i => Entry(p, "source " + i)));
foreach (string invalid in new[] { "not json", "{}", "{\"summary\":12,\"diary\":\"x\"}", "{\"summary\":\"\",\"diary\":\"x\"}", JsonConvert.SerializeObject(new { summary = new string('x', 2401), diary = "x" }) })
{
    p.SummaryAPIAskFunction = (_, _, _, _) => new() { Reply = invalid };
    p.CompressHistory();
    Check(p.Summaries.Count == 0 && p.DiaryEntries.Count == 0 && p.Dialogues.All(x => x.IsActive) && p.LastSummaryError.Length > 0, "Invalid summary preserves all source history: " + invalid[..Math.Min(20, invalid.Length)]);
}
p.SummaryAPIAskFunction = (_, _, _, _) => throw new IOException("offline");
Ask(p, "continue after failed summary");
Check(p.Dialogues[^1].Turn!.History.Count == 4, "Summary service failure permits chatting with intact history below the hard limit");
p.RequestCharacterLimit = 20;
int chatCalls = 0;
p.AIAPIAskFunction = (_, _, _, _) => { chatCalls++; return new() { Reply = "[expression:gentle]\nanswer" }; };
bool rejected = false;
try { Ask(p, "too much history"); } catch (InvalidOperationException) { rejected = true; }
Check(rejected && chatCalls == 0 && p.Dialogues.Count == 5, "Hard limit stops an oversized request without truncating history or adding a turn");

p = NewProcess();
p.MaxHistoryBeforeCompress = 100;
p.HistoryCharacterBudget = 200;
p.DiaryKeepRecentCount = 10;
p.Dialogues.AddRange(Enumerable.Range(0, 5).Select(i => Entry(p, i + new string('x', 90))));
p.SummaryAPIAskFunction = (_, _, _, _) => Summary("short recap");
p.CompressHistory();
Check(p.Summaries.Count == 1 && p.Summaries[0].ThroughDialogueId == p.Dialogues[3].DialogueId, "Character trigger frees space even before the turn-count trigger and retains the newest full turn");

p = NewProcess();
p.RequestCharacterLimit = 2200;
p.Dialogues.AddRange(Enumerable.Range(0, 20).Select(i => Entry(p, i + new string('x', 250))));
var batches = new List<JObject>();
p.SummaryAPIAskFunction = (system, _, input, _) =>
{
    Check(system.Length + input.Length <= p.RequestCharacterLimit, "Imported history compression respects its request budget");
    batches.Add(JObject.Parse(input));
    return Summary("imported recap");
};
p.CompressHistory();
Check(batches.Count > 1 && batches.SelectMany(x => x["dialogues"]!).Select(x => x["Question"]!.Value<string>())
    .SequenceEqual(p.Dialogues.Take(p.Dialogues.FindIndex(x => x.DialogueId == p.Summaries[0].ThroughDialogueId) + 1).Select(x => x.Question)),
    "Large old saves are processed in bounded consecutive batches without user retries");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "cache source " + i)));
p.AIAPIEmbeddingFunction = inputs => inputs.Select(_ => new float[] { 1, 0 }).ToArray();
p.Retrieval.Select("cache", p.Dialogues, 2);
long embeddingsBefore = p.Retrieval.EmbeddingRequests;
p.SummaryAPIAskFunction = (_, _, _, _) => Summary("cached sources recap");
p.CompressHistory();
p.Retrieval.Select("cache", p.Dialogues, 2);
Check(p.Retrieval.EmbeddingRequests == embeddingsBefore, "Publishing a recap preserves existing source vector and query caches");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "pending " + i)));
p.Dialogues[1].Turn = new() { OperationsFinished = true, Receipts = [new() { Tool = "buy", Status = "unknown" }] };
string pendingInput = "";
p.SummaryAPIAskFunction = (_, _, input, _) => { pendingInput = input; return Summary("safe prefix"); };
p.CompressHistory();
Check(JObject.Parse(pendingInput)["dialogues"]!.Count() == 1 && p.Summaries[0].ThroughDialogueId == p.Dialogues[0].DialogueId, "An unresolved action prevents compression from jumping across its turn");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "concurrent " + i)));
using (var started = new ManualResetEventSlim())
using (var release = new ManualResetEventSlim())
{
    int summaries = 0;
    p.SummaryAPIAskFunction = (_, _, _, _) => { summaries++; started.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return Summary("background recap"); };
    var work = Task.Run(p.CompressHistory);
    try
    {
        Check(started.Wait(TimeSpan.FromSeconds(5)), "Background compression starts");
        var next = Task.Run(() => Ask(p, "appended during compression"));
        Check(next.Wait(TimeSpan.FromSeconds(3)) && summaries == 1, "Background compression neither blocks the next turn nor duplicates the summary call");
    }
    finally { release.Set(); work.GetAwaiter().GetResult(); }
    Ask(p, "after publish");
    Check(p.Dialogues[^1].Turn!.History.Select(x => x[0]).SequenceEqual(new[] { "concurrent 3", "appended during compression" }), "Publishing a background recap preserves turns appended during generation");
}

foreach (string mutation in new[] { "delete", "edit", "switch" })
{
    p = NewProcess();
    p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "race " + i)));
    using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    p.SummaryAPIAskFunction = (_, _, _, _) => { started.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return Summary("stale"); };
    var work = Task.Run(p.CompressHistory);
    try
    {
        Check(started.Wait(TimeSpan.FromSeconds(5)), "Concurrent mutation test starts: " + mutation);
        if (mutation == "delete") p.DeleteMemory(p.Dialogues[0]);
        if (mutation == "edit") p.Dialogues[0].Answer = "corrected";
        if (mutation == "switch") p.SaveId = "different-save";
    }
    finally { release.Set(); work.GetAwaiter().GetResult(); }
    Check(p.Summaries.Count == 0 && p.DiaryEntries.Count == 0, "Changed source/scope rejects stale compression: " + mutation);
}

p = NewProcess();
var rich = Entry(p, "buy the second item");
rich.Turn = new()
{
    InitialInput = "old reference and state", ResultInput = "old recap and reference",
    Initial = new() { Raw = "[expression:gentle]\nI will check" }, Observation = "a blue cup",
    Receipts = [new() { Tool = "list_items", Status = "observed", Facts = "1: milk; 2: bread" }, new() { Tool = "buy", Status = "failed", Facts = "not enough money" }, new() { Tool = "历史补查", Status = "observed", Facts = "irrelevant old diary" }]
};
var pair = rich.ToMessages();
Check(pair[0].Contains("a blue cup") && pair[1].Contains("1: milk; 2: bread") && pair[1].Contains("not enough money") && pair[1].Contains("I will check"), "Compact history retains observations, displayed order, initial reply and actual execution results");
Check(!string.Join("", pair).Contains("old reference") && !string.Join("", pair).Contains("old recap") && !string.Join("", pair).Contains("irrelevant old diary"), "Compact history excludes previously injected references and recaps");

Check(ChatProtocol.UserMessage("薄荷现在叫什么？", summary: "薄荷旧名小青").Contains("若有主人更正则以后续更正为准"), "Current reference identifies the recap as older than subsequent corrections");

Check(!ChatProtocol.TryParse("[expression:gentle]\nFirst paragraph\n  [delegate][expression:gentle]  \r\nSecond paragraph", false, out _), "Body header rejection covers later lines and whitespace");
Check(ChatProtocol.TryParse("[expression:gentle]\nYou quoted [回执:买好了] as text.", false, out _), "Inline quoted brackets remain valid");
p = NewProcess();
int purchases = 0, resultAttempts = 0;
p.Tools.Add(new ToolUse("buy", "buy bread", ["buy", "bread"], [], [], p.Localization)
{ Execute = (_, _) => { purchases++; return new() { Status = "applied", Facts = "bought bread for 8" }; } });
p.ToolAPIAskFunction = (_, _, _, _) => new() { ToolCalls = [new() { Code = "buy" }, new() { Code = "finish" }] };
p.AIAPIAskFunction = (_, _, input, _) =>
{
    if (!input.Contains("[回执:")) return new() { Reply = "[delegate][expression:conscientious]\nI will buy bread." };
    resultAttempts++;
    return new() { Reply = resultAttempts <= 2 ? "[expression:gentle]\nBought it.\n[expression:gentle]\nBought it again." : "[expression:gentle]\nBought one bread." };
};
try { Ask(p, "buy bread"); } catch (InvalidDataException) { }
Check(purchases == 1 && resultAttempts == 2, "Invalid later header retries only the reply, never the completed purchase");
p.RetryReply(p.Dialogues[^1].Turn!.TurnId, _ => { });
Check(purchases == 1 && resultAttempts == 3, "Explicit recovery after malformed continuation reuses the committed receipt");

// Memory evidence keeps player corrections and host outcomes, not a failed lookup answer.
p = NewProcess();
p.MaxHistoryBeforeCompress = 0; p.MaxHistoryCount = 0; p.MemoryLimit = 5;
p.AIAPIEmbeddingFunction = inputs => inputs.Select(_ => new float[] { 1, 0 }).ToArray();
p.AIAPIRerankFunction = (_, docs) => docs.Select(_ => .9f).ToArray();
var failedLookup = new Dialogue("薄荷不叫小青，现在叫云芽。面包买到了吗？", "错误答案标记：没找到，所以没买。", p.Localization)
{
    SaveId = p.SaveId,
    Turn = new() { OperationsFinished = true, Receipts = [
        new() { Tool = "历史补查", Status = "not_found", Facts = "没有找到相关历史或日记。" },
        new() { Tool = "buy_item", Status = "applied", Facts = "已购买1个，实付8，未食用。" }] }
};
p.Dialogues.Add(failedLookup);
string memoryInput = "";
p.AIAPIAskFunction = (_, _, input, _) => { memoryInput = input; return new() { Reply = "[expression:gentle]\n知道了。" }; };
Ask(p, "薄荷和面包");
Check(memoryInput.Contains("薄荷不叫小青，现在叫云芽") && memoryInput.Contains("已购买1个，实付8") && !memoryInput.Contains("错误答案标记"), "Memory keeps player negation/correction and host purchase facts without failed lookup chatter");
Check(failedLookup.Answer.Contains("错误答案标记") && !string.Join("", failedLookup.EmbeddingTexts).Contains("错误答案标记"), "Failed lookup original remains saved but is excluded from retrieval scoring text");
failedLookup.Turn.Receipts[0].Status = "observed";
Check(failedLookup.MemoryAnswer == failedLookup.Answer, "Successful lookup is not discarded based on negative words");
failedLookup.Turn.Receipts[0].Status = "not_found";
p.MaxHistoryBeforeCompress = 1; p.DiaryKeepRecentCount = 0;
string evidenceInput = "";
p.SummaryAPIAskFunction = (_, _, input, _) => { evidenceInput = input; return Summary("云芽，买了1个面包。"); };
p.CompressHistory();
Check(evidenceInput.Contains("薄荷不叫小青，现在叫云芽") && evidenceInput.Contains("已购买1个，实付8") && !evidenceInput.Contains("错误答案标记"), "Summary receives player and host evidence without failed lookup answer");

p = NewProcess(); p.RecallMemoryLimit = 0;
string continuationInput = "";
p.AIAPIAskFunction = (_, _, input, _) =>
{
    if (!input.Contains("[回执:")) return new() { Reply = "[expression:conscientious][recall]\n我翻翻。" };
    continuationInput = input;
    return new() { Reply = "[expression:gentle]\n这次没有找到新资料。" };
};
p.Dialogues.Add(Entry(p, "known source"));
p.Summaries.Add(new() { SaveId = p.SaveId, ThroughDialogueId = p.Dialogues[0].DialogueId, Text = "手铃叫浅滩" });
Ask(p, "手铃叫什么？");
Check(p.Dialogues[^1].Turn!.Receipts[0].Status == "not_found" && continuationInput.Contains("历史补查: 未找到;") && !continuationInput.Contains("历史补查: 已查到"), "Empty recall has an explicit not-found receipt");
Check(continuationInput.Contains("手铃叫浅滩]") && p.Dialogues[^1].Turn!.InitialInput.Contains("手铃叫浅滩]"), "Both initial and continuation retain the same known summary even after an empty recall");

// Exercise the real HTTP adapter against a local fake, without using credentials or an external model.
foreach (bool reportsCache in new[] { true, false })
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    JObject? payload = null;
    var server = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        int length = 0;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Split(':')[1]);
        var buffer = new char[length];
        if (await reader.ReadBlockAsync(buffer, 0, buffer.Length) != length) throw new EndOfStreamException();
        payload = JObject.Parse(new string(buffer));
        string body = JsonConvert.SerializeObject(new
        {
            choices = new[] { new { finish_reason = "stop", message = new { content = "reply" } } },
            usage = new { total_tokens = 120, prompt_tokens = 100, prompt_tokens_details = reportsCache ? new { cached_tokens = 80 } : null }
        });
        var bytes = Encoding.UTF8.GetBytes(body);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(bytes);
    });
    try
    {
        using var api = new ModelApiClient($"http://127.0.0.1:{port}", "test", "", 5);
        var result = api.Chat("fixed", [["recap", ""], ["old question", "old answer"]], "dynamic current input", []);
        await server.WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.PromptTokens == 100 && result.CachedTokens == (reportsCache ? 80 : null), "HTTP usage distinguishes missing cache statistics from reported hits: " + reportsCache);
        Check(payload!["messages"]!.Select(x => x["role"]!.Value<string>()).SequenceEqual(new[] { "system", "user", "user", "assistant", "user" }), "HTTP message ordering sends recap without fabricating an assistant reply");
    }
    finally { listener.Stop(); }
}
Console.WriteLine($"{checks} context checks passed.");
