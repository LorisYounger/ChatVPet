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
    AIAPIAskFunction = (_, _, _) => new() { Reply = "[expression:gentle]\nanswer" }
};
VPetChatProcess.AIAPIAskResult Summary(string text) => new()
{ Reply = JsonConvert.SerializeObject(new { summary = text, diary = "this batch diary" }) };
void Ask(VPetChatProcess process, string message) => process.Ask(message, _ => { }, new());
Dialogue Entry(VPetChatProcess process, string question) => new(question, "answer", process.Localization) { SaveId = process.SaveId };

var p = NewProcess();
var requests = new List<(string System, List<ChatMessage> History, string Input)>();
var summaryInputs = new List<string>();
p.SummaryAPIAskFunction = (_, messages, _) => { summaryInputs.Add(messages[^1].Content); return Summary("recap " + summaryInputs.Count); };
p.AIAPIAskFunction = (system, messages, _) =>
{
    requests.Add((system, messages.SkipLast(1).ToList(), messages[^1].Content));
    return new() { Reply = "[expression:gentle]\nanswer" };
};
for (int i = 1; i <= 4; i++) { p.HostState = "changing state " + i; Ask(p, "question " + i); }
Check(requests[3].History.Count == 6, "History grows beyond the old one-turn sliding limit");
Check(requests[2].History.SequenceEqual(requests[3].History.Take(4)), "Old history messages remain byte-for-byte stable");
Check(requests.All(x => x.System == "fixed instructions"), "Dynamic state never changes fixed instructions");
Check(requests[3].History[0].Content == requests[0].Input, "History preserves the original state snapshot");
Ask(p, "question 5");
Check(p.Summaries.Count == 1 && p.DiaryEntries.Count == 1, "One compression publishes both recap and diary");
Check(requests[4].History.Count == 2 && requests[4].History[0].Content == requests[3].Input, "Raw tail starts exactly after the summary boundary");
Check(requests[4].Input.Contains("[参考:较早对话提要") && requests[4].Input.Contains("recap 1]") && !JsonConvert.SerializeObject(requests[4].History).Contains("recap 1"), "Summary is injected once in the current reference, never as historical dialogue");
Check(p.Dialogues.Count == 5 && p.Dialogues.Take(3).All(x => !x.IsActive), "Compression keeps source records for recall");
Ask(p, "question 6");
Check(summaryInputs.Count == 1 && requests[4].History.SequenceEqual(requests[5].History.Take(2)), "Between compressions raw history stays stable while current reference carries the summary");
Ask(p, "question 7"); Ask(p, "question 8");
Check(summaryInputs.Count == 2 && JObject.Parse(summaryInputs[1])["previousSummary"]!.Value<string>() == "recap 1", "Next compression receives the previous cumulative recap");
Check(JObject.Parse(summaryInputs[1])["dialogues"]!.Count() == 3 && !summaryInputs[1].Contains("question 1"), "Each compression only summarizes the new consecutive batch");
Check(requests[^1].History[0].Content == requests[6].Input, "Second compression advances the boundary without a gap");

// Persist exactly the fields stored by the plugin, then reconstruct the process.
string saved = JsonConvert.SerializeObject(new { dialogues = p.Dialogues, diaries = p.DiaryEntries, summaries = p.Summaries });
var data = JObject.Parse(saved);
var restored = NewProcess();
restored.Dialogues = data["dialogues"]!.ToObject<List<Dialogue>>()!;
restored.DiaryEntries = data["diaries"]!.ToObject<List<DiaryEntry>>()!;
restored.Summaries = data["summaries"]!.ToObject<List<ConversationSummary>>()!;
Ask(restored, "after restart");
Check(restored.Dialogues[^1].Turn!.Summary == "recap 2" && restored.Dialogues[^1].Turn!.History[0].Content == requests[6].Input, "Restart preserves both the recap and its exact history boundary");
Check(restored.Dialogues[0].DialogueId == p.Dialogues[0].DialogueId, "Source identifiers survive serialization");
restored.SaveId = "another-save";
Ask(restored, "other save");
Check(restored.Dialogues[^1].Turn!.Summary == "" && restored.Dialogues[^1].Turn!.History.Count == 0, "A different save receives neither recap nor history from the first save");
restored.SaveId = "default";
restored.DeleteMemory(restored.Dialogues[0]);
Check(restored.ReadSummary() == "" && restored.DiaryEntries.Count == 1, "Deleting a covered source invalidates the cumulative recap and its own diary");
Ask(restored, "after deletion");
Check(restored.Dialogues[^1].Turn!.History[0].Content == requests[1].Input, "After invalidation surviving source history is available for rebuilding");

p = NewProcess();
p.Dialogues = JsonConvert.DeserializeObject<List<Dialogue>>("[{\"Question\":\"old archived\",\"Answer\":\"old answer\",\"IsActive\":false},{\"Question\":\"recent\",\"Answer\":\"answer\"}]")!;
Ask(p, "legacy save");
Check(p.Dialogues[^1].Turn!.History[0].Content == "old archived", "Old diary-only archives are not lost when no cumulative recap exists");
p.MaxHistoryBeforeCompress = 0;
Ask(p, "disabled");
Check(p.Dialogues[^1].Turn!.History.Count == 2, "Explicitly disabling compression restores the configured recent-turn limit");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(1, 4).Select(i => Entry(p, "source " + i)));
foreach (string invalid in new[] { "not json", "{}", "{\"summary\":12,\"diary\":\"x\"}", "{\"summary\":\"\",\"diary\":\"x\"}", JsonConvert.SerializeObject(new { summary = new string('x', 2401), diary = "x" }) })
{
    p.SummaryAPIAskFunction = (_, _, _) => new() { Reply = invalid };
    p.CompressHistory();
    Check(p.Summaries.Count == 0 && p.DiaryEntries.Count == 0 && p.Dialogues.All(x => x.IsActive) && p.LastSummaryError.Length > 0, "Invalid summary preserves all source history: " + invalid[..Math.Min(20, invalid.Length)]);
}
p.SummaryAPIAskFunction = (_, _, _) => throw new IOException("offline");
Ask(p, "continue after failed summary");
Check(p.Dialogues[^1].Turn!.History.Count == 8, "Summary service failure permits chatting with intact history below the hard limit");
p.RequestCharacterLimit = 20;
int chatCalls = 0;
p.AIAPIAskFunction = (_, _, _) => { chatCalls++; return new() { Reply = "[expression:gentle]\nanswer" }; };
bool rejected = false;
try { Ask(p, "too much history"); } catch (InvalidOperationException) { rejected = true; }
Check(rejected && chatCalls == 0 && p.Dialogues.Count == 5, "Hard limit stops an oversized request without truncating history or adding a turn");

p = NewProcess();
p.MaxHistoryBeforeCompress = 100;
p.HistoryCharacterBudget = 200;
p.DiaryKeepRecentCount = 10;
p.Dialogues.AddRange(Enumerable.Range(0, 5).Select(i => Entry(p, i + new string('x', 90))));
p.SummaryAPIAskFunction = (_, _, _) => Summary("short recap");
p.CompressHistory();
Check(p.Summaries.Count == 1 && p.Summaries[0].ThroughDialogueId == p.Dialogues[3].DialogueId, "Character trigger frees space even before the turn-count trigger and retains the newest full turn");

p = NewProcess();
p.RequestCharacterLimit = 2200;
p.Dialogues.AddRange(Enumerable.Range(0, 20).Select(i => Entry(p, i + new string('x', 250))));
var batches = new List<JObject>();
p.SummaryAPIAskFunction = (system, messages, _) =>
{
    string input = messages[^1].Content;
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
p.SummaryAPIAskFunction = (_, _, _) => Summary("cached sources recap");
p.CompressHistory();
p.Retrieval.Select("cache", p.Dialogues, 2);
Check(p.Retrieval.EmbeddingRequests == embeddingsBefore, "Publishing a recap preserves existing source vector and query caches");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "pending " + i)));
p.Dialogues[1].Turn = new() { OperationsFinished = true, Receipts = [new() { Tool = "buy", Status = "unknown" }] };
string pendingInput = "";
p.SummaryAPIAskFunction = (_, messages, _) => { pendingInput = messages[^1].Content; return Summary("safe prefix"); };
p.CompressHistory();
Check(JObject.Parse(pendingInput)["dialogues"]!.Count() == 1 && p.Summaries[0].ThroughDialogueId == p.Dialogues[0].DialogueId, "An unresolved action prevents compression from jumping across its turn");

p = NewProcess();
p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "concurrent " + i)));
using (var started = new ManualResetEventSlim())
using (var release = new ManualResetEventSlim())
{
    int summaries = 0;
    p.SummaryAPIAskFunction = (_, _, _) => { summaries++; started.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return Summary("background recap"); };
    var work = Task.Run(p.CompressHistory);
    try
    {
        Check(started.Wait(TimeSpan.FromSeconds(5)), "Background compression starts");
        var next = Task.Run(() => Ask(p, "appended during compression"));
        Check(next.Wait(TimeSpan.FromSeconds(3)) && summaries == 1, "Background compression neither blocks the next turn nor duplicates the summary call");
    }
    finally { release.Set(); work.GetAwaiter().GetResult(); }
    Ask(p, "after publish");
    Check(p.Dialogues[^1].Turn!.History.Where(x => x.Role == "user").Select(x => x.Content).SequenceEqual(new[] { "concurrent 3", "appended during compression" }), "Publishing a background recap preserves turns appended during generation");
}

foreach (string mutation in new[] { "delete", "edit", "switch" })
{
    p = NewProcess();
    p.Dialogues.AddRange(Enumerable.Range(0, 4).Select(i => Entry(p, "race " + i)));
    using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    p.SummaryAPIAskFunction = (_, _, _) => { started.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return Summary("stale"); };
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

Check(ChatProtocol.UserMessage("薄荷现在叫什么？", summary: "薄荷旧名小青").Contains("若有主人更正则以后续更正为准"), "Current reference identifies the recap as older than subsequent corrections");

Check(!ChatProtocol.TryParse("[expression:gentle]\nFirst paragraph\n  [delegate][expression:gentle]  \r\nSecond paragraph", false, out _), "Body header rejection covers later lines and whitespace");
Check(ChatProtocol.TryParse("[expression:gentle]\nYou quoted [回执:买好了] as text.", false, out _), "Inline quoted brackets remain valid");
p = NewProcess();
int purchases = 0, resultAttempts = 0;
var wireRequests = new List<ChatMessage[]>();
p.Dialogues.Add(Entry(p, "earlier turn"));
p.HostState = "state before purchase";
p.Tools.Add(new ToolUse("buy", "buy bread", ["buy", "bread"], [], [], p.Localization)
{ Execute = (_, _) => { purchases++; return new() { Status = "applied", Facts = "bought bread for 8" }; } });
p.ToolAPIAskFunction = (_, _, _) => new() { ToolCalls = [new() { Code = "buy" }, new() { Code = "finish" }] };
p.AIAPIAskFunction = (_, messages, _) =>
{
    string input = messages[^1].Content;
    wireRequests.Add(messages.ToArray());
    if (!messages[^1].FromHost) return new() { Reply = "[delegate][expression:conscientious]\nI will buy bread." };
    resultAttempts++;
    return new() { Reply = resultAttempts <= 2 ? "[expression:gentle]\nBought it.\n[expression:gentle]\nBought it again." : "[expression:gentle]\nBought one bread." };
};
try { Ask(p, "buy bread"); } catch (InvalidDataException) { }
Check(purchases == 1 && resultAttempts == 2, "Invalid later header retries only the reply, never the completed purchase");
var purchaseId = p.Dialogues[^1].Turn!.TurnId;
Check(wireRequests[0].Select(m => m.Role).SequenceEqual(["user", "assistant", "user"])
    && wireRequests[1].Take(3).SequenceEqual(wireRequests[0])
    && wireRequests[1][3] == new ChatMessage("assistant", p.Dialogues[^1].Turn!.Initial.Raw)
    && wireRequests[1][4].FromHost && wireRequests[1][4].Content.StartsWith("[state before purchase]")
    && wireRequests[1][4].Content.Contains("[初答:I will buy bread.][回执:")
    && wireRequests[1][4].Content.EndsWith("\nbuy bread"), "Continuation includes exact H/U0/A0 followed by one host user");
Check(wireRequests[1].SequenceEqual(wireRequests[2]) && p.Dialogues[^1].ToMessages().Count() == 2,
    "Invalid continuation retries identical messages and contributes no assistant to history");
p.Dialogues = JsonConvert.DeserializeObject<List<Dialogue>>(JsonConvert.SerializeObject(p.Dialogues))!;
p.RetryReply(purchaseId, _ => { });
Check(wireRequests[1].SequenceEqual(wireRequests[3]), "Retry after loading preserves the complete request");
var committedMessages = p.Dialogues[^1].ToMessages().ToArray();
p.AIAPIAskFunction = (_, messages, _) => { wireRequests.Add(messages.ToArray()); return new() { Reply = "[expression:gentle]\nOK" }; };
Ask(p, "next question");
Check(wireRequests[^1].Skip(2).SkipLast(1).SequenceEqual(committedMessages)
    && committedMessages.Select(m => m.Role).SequenceEqual(["user", "assistant", "user", "assistant"])
    && committedMessages[^1].Content == "[expression:gentle]\nBought one bread.", "Next request replays the four accepted messages verbatim");
Check(purchases == 1 && resultAttempts == 3, "Explicit recovery after malformed continuation reuses the committed receipt");

p = NewProcess();
int captures = 0;
p.ScreenshotFunction = turn => { captures++; return new() { Status = "unknown", Facts = "暂未确认" }; };
p.AIAPIAskFunction = (_, messages, _) => new() { Reply = messages[^1].FromHost
    ? "[expression:gentle]\n" + (messages[^1].Content.Contains("731") ? "731" : "暂未确认")
    : messages[^1].Content.Contains("[观察:") ? "[expression:gentle]\n图片上写着731。" : "[expression:conscientious][screenshot]\n我看看。" };
p.Ask("看附图", "图上写着731", _ => { }, new());
Check(captures == 0 && p.Dialogues[^1].ToMessages().Count() == 2, "Attached observation answers directly without a screenshot");
Ask(p, "现在看看");
var shot = p.Dialogues[^1];
var beforeLate = shot.ToMessages().ToArray();
p.AIAPIAskFunction = (_, _, _) => new() { Reply = "[expression:gentle]\n收到。" };
Ask(p, "另一件事");
var intervening = p.Dialogues[^1];
var late = new HostReceipt { SaveId = p.SaveId, TurnId = shot.Turn!.TurnId, StepId = "screenshot", Status = "observed", Facts = "新观察：731" };
Check(captures == 1 && beforeLate.Length == 4 && !beforeLate[0].Content.Contains("暂未确认") && beforeLate[2].FromHost,
    "Active screenshot follows the real initial reply and is captured once");
Check(p.ReceiveReceipt(late, _ => { }) && !p.ReceiveReceipt(late, _ => { }) && captures == 1, "Late receipt is applied once without repeating the screenshot");
var updated = shot.ToMessages().ToArray();
Check(updated.Take(4).SequenceEqual(beforeLate) && updated.Length == 6 && updated[4].FromHost
    && updated[4].Content.Contains("新观察：731") && updated[5].Content.EndsWith("收到。"), "Late update appends a host user and a new assistant after the spoken uncertainty");

p.MaxHistoryBeforeCompress = 2;
p.DiaryKeepRecentCount = 1;
p.SummaryAPIAskFunction = (_, _, _) => Summary("partial");
p.CompressHistory();
Check(p.Summaries.Single().ThroughDialogueId == p.Dialogues[0].DialogueId, "Compression cannot separate a late update from an intervening turn");
Ask(p, "现在继续");
Check(p.Dialogues[^1].Turn!.History.SequenceEqual(beforeLate.Concat(intervening.ToMessages()).Concat(updated.Skip(4))),
    "After another player turn, a late update appears at the chronological end of the raw tail");

p = NewProcess();
p.MaxHistoryBeforeCompress = 99;
p.HistoryCharacterBudget = 99999;
p.RequestCharacterLimit = 3200;
p.SummaryAPIAskFunction = (_, _, _) => Summary("older turns");
for (int i = 0; i < 4; i++) Ask(p, i + new string('x', 490));
var longQuestion = new string('q', 1450);
Ask(p, longQuestion);
Check(p.Summaries.Count == 1 && p.Dialogues[^1].Turn!.InitialInput.EndsWith(longQuestion),
    "A long U0 triggers complete-turn compression below the soft thresholds");

p = NewProcess();
int moodCalls = 0;
p.AIAPIAskFunction = (_, _, _) => { moodCalls++; return new() { Reply = "[mood][expression:gentle]\n谢谢你。" }; };
Ask(p, "给你花");
p.RetryReply(p.Dialogues[^1].Turn!.TurnId, _ => { });
Check(moodCalls == 1 && p.Dialogues[^1].ToMessages().Count() == 2 && !p.Dialogues[^1].Turn!.PendingReply,
    "Silent mood adjustment stays a completed two-message turn on retry");

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
p.AIAPIAskFunction = (_, messages, _) => { memoryInput = messages[^1].Content; return new() { Reply = "[expression:gentle]\n知道了。" }; };
Ask(p, "薄荷和面包");
Check(memoryInput.Contains("薄荷不叫小青，现在叫云芽") && memoryInput.Contains("已购买1个，实付8") && !memoryInput.Contains("错误答案标记"), "Memory keeps player negation/correction and host purchase facts without failed lookup chatter");
Check(failedLookup.Answer.Contains("错误答案标记") && !string.Join("", failedLookup.EmbeddingTexts).Contains("错误答案标记"), "Failed lookup original remains saved but is excluded from retrieval scoring text");
failedLookup.Turn.Receipts[0].Status = "observed";
Check(failedLookup.MemoryAnswer == failedLookup.Answer, "Successful lookup is not discarded based on negative words");
failedLookup.Turn.Receipts[0].Status = "not_found";
p.MaxHistoryBeforeCompress = 1; p.DiaryKeepRecentCount = 0;
string evidenceInput = "";
p.SummaryAPIAskFunction = (_, messages, _) => { evidenceInput = messages[^1].Content; return Summary("云芽，买了1个面包。"); };
p.CompressHistory();
Check(evidenceInput.Contains("薄荷不叫小青，现在叫云芽") && evidenceInput.Contains("已购买1个，实付8") && !evidenceInput.Contains("错误答案标记"), "Summary receives player and host evidence without failed lookup answer");

p = NewProcess(); p.RecallMemoryLimit = 0;
string continuationInput = "";
p.AIAPIAskFunction = (_, messages, _) =>
{
    string input = messages[^1].Content;
    if (!input.Contains("[回执:")) return new() { Reply = "[expression:conscientious][recall]\n我翻翻。" };
    continuationInput = input;
    return new() { Reply = "[expression:gentle]\n这次没有找到新资料。" };
};
p.Dialogues.Add(Entry(p, "known source"));
p.Summaries.Add(new() { SaveId = p.SaveId, ThroughDialogueId = p.Dialogues[0].DialogueId, Text = "手铃叫浅滩" });
Ask(p, "手铃叫什么？");
Check(p.Dialogues[^1].Turn!.Receipts[0].Status == "not_found" && continuationInput.Contains("历史补查: 未找到;") && !continuationInput.Contains("历史补查: 已查到"), "Empty recall has an explicit not-found receipt");
Check(!continuationInput.Contains("手铃叫浅滩]") && p.Dialogues[^1].Turn!.InitialInput.Contains("手铃叫浅滩]"), "The original summary stays in U0 without being duplicated into an empty recall receipt");

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
        using var api = new ModelApiClient($"http://127.0.0.1:{port}", "test", "local-trace-secret", 5);
        var traceEvents = new List<JObject>();
        api.Trace = trace => traceEvents.Add(JObject.FromObject(trace));
        var result = api.Chat("fixed", [new("user", "question"), new("assistant", "initial"), new("user", "receipt", FromHost: true)], []);
        await server.WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.PromptTokens == 100 && result.CachedTokens == (reportsCache ? 80 : null), "HTTP usage distinguishes missing cache statistics from reported hits: " + reportsCache);
        Check(payload!["messages"]!.All(m => ((JObject)m).Count == 2) && payload["messages"]!.Select(x => x["role"]!.Value<string>()).SequenceEqual(new[] { "system", "user", "assistant", "user" }), "HTTP serializes the ordered continuation with role/content only");
        Check(traceEvents.Count == 2 && !traceEvents[0].Value<bool>("Finished") && traceEvents[1].Value<bool>("Finished")
            && traceEvents[1].Value<int>("StatusCode") == 200 && JToken.DeepEquals(payload, traceEvents[1]["Request"]),
            "Trace preserves the exact HTTP request and completion boundary");
        Check(!JsonConvert.SerializeObject(traceEvents).Contains("local-trace-secret") && traceEvents[1].Value<string>("Response")!.Contains("reply"),
            "Trace records the raw response without authorization credentials");
    }
    finally { listener.Stop(); }
}
Console.WriteLine($"{checks} context checks passed.");
