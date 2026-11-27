using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatVPet.ChatProcess.ConsoleTest;

internal static class FeedbackExport
{
    public static void Run(string root, string decisionsPath, string output, IEnumerable<string> exclusions, DateTimeOffset? blindSince = null)
    {
        Directory.CreateDirectory(output);
        var reviews = File.ReadLines(decisionsPath).Where(x => !string.IsNullOrWhiteSpace(x)).Select(JObject.Parse)
            .ToDictionary(x => ((string)x["id"]!, (int)x["revision"]!));
        var files = new Dictionary<string,List<object>>();
        foreach (string name in new[] { "review", "train-sft", "train-preference", "blind", "blind-key", "sources", "diagnostics" }) files[name] = [];
        // Group by exported user directory (<UserId>). Shared source turns union groups before splitting.
        var members = new Dictionary<string,string>();
        var owners = new Dictionary<string,string>();
        var sessions = new List<(string Group, List<Dialogue> Dialogues)>();
        string Group(string id)
        {
            if (!members.TryGetValue(id, out var parent)) members[id] = parent = id;
            return parent == id ? id : members[id] = Group(parent);
        }
        foreach (var path in Directory.EnumerateFiles(root, "*.turns.json", SearchOption.AllDirectories).Order())
        {
            var group = Path.GetFileName(Path.GetDirectoryName(path)!);
            var dialogues = JObject.Parse(File.ReadAllText(path)).GetValue("dialogues", StringComparison.OrdinalIgnoreCase)!.ToObject<List<Dialogue>>()!;
            foreach (var d in dialogues)
            {
                if (owners.TryGetValue(d.DialogueId, out var prior)) members[Group(group)] = Group(prior);
                else owners[d.DialogueId] = group;
            }
            sessions.Add((group, dialogues));
        }
        var questions = new Dictionary<string,List<(string Text,string Group)>>();
        foreach (var (group, dialogues) in sessions)
        foreach (var d in dialogues.Where(x => x.Turn?.Allow == true))
        {
            string norm = Normalize(d.Question);
            foreach (var anchor in Anchors(norm))
            {
                if (!questions.TryGetValue(anchor, out var candidates)) questions[anchor] = candidates = [];
                var match = candidates.FirstOrDefault(other => Similar(norm, other.Text));
                if (match.Group != null) members[Group(group)] = Group(match.Group);
                else candidates.Add((norm,group));
            }
        }
        // Group-level holdout assignments can reserve an entire later cohort for blind testing.
        var blindGroups = sessions.GroupBy(x => Group(x.Group)).Where(g => blindSince.HasValue
            ? g.SelectMany(x => x.Dialogues).All(d => d.CreatedAt.ToUniversalTime() >= blindSince.Value.UtcDateTime)
            : SHA256.HashData(Encoding.UTF8.GetBytes(g.Key))[0] < 26).Select(g => g.Key).ToHashSet();
        var seen = new HashSet<string>();
        var external = new Dictionary<string,List<string>>();
        foreach (var path in exclusions)
            foreach (var line in File.ReadLines(path))
            {
                var row = JObject.Parse(line);
                var user = row["messages"]?.LastOrDefault(x => (string?)x["role"] == "user");
                if (user != null)
                {
                    string question = (string)user["content"]!;
                    IndexQuestion(question.StartsWith('[') && question.Contains('\n') ? question[(question.IndexOf('\n') + 1)..] : question);
                }
            }

        void IndexQuestion(string text)
        {
            string norm = Normalize(text);
            foreach (string anchor in Anchors(norm))
            {
                if (!external.TryGetValue(anchor, out var bucket)) external[anchor] = bucket = [];
                if (!bucket.Contains(norm)) bucket.Add(norm);
            }
        }
        foreach (var (group, dialogues) in sessions)
        foreach (var d in dialogues)
        {
            if (d.Turn is not { Allow: true } t) continue;
            foreach (var f in t.Feedback.Where(x => !x.Withdrawn && x.Revision > 0 &&
                (x.Votes.Count > 0 || x.HasSelection || x.Suggestion.Length + x.ExpectedReply.Length + x.ExpectedAction.Length + x.StopCondition.Length + x.Hypothesis.Length > 0)))
            {
                string family = Group(group);
                var source = new { id = f.Id, revision = f.Revision, stage = f.ResultRevision, group = family };
                void Skip(string reason) => files["diagnostics"].Add(new { source, reason });
                var prefix = t.TrainingPrefix(f.ResultRevision, out bool same, out string omission);
                if (prefix == null) { Skip(omission); continue; }
                var prompt = prefix.Select(x => new { role = x.Role, content = x.Content }).ToList();
                var frame = new { source, system = t.System, prompt, original = f.Answer(t, ""), f.Target, f.Votes,
                    f.HasSelection, f.Accepted, f.Rejected, f.Suggestion, f.ExpectedReply, f.ExpectedAction, f.StopCondition,
                    f.Hypothesis, f.Candidates, f.Animation, f.Fallback, t.ChatModel, t.ToolModel, t.TrainingProtocolVersion, template = t.Template, process = t.ProcessVersion, sameInput = same };
                if (Unsafe.IsMatch(JsonConvert.SerializeObject(frame))) { Skip("疑似原始资料"); continue; }
                files["review"].Add(frame);
                if (!reviews.TryGetValue((f.Id, f.Revision), out var review) || review.Value<bool?>("approved") != true ||
                    review.Value<bool?>("facts") != true || review.Value<bool?>("privacy") != true) { Skip("待本修订审核"); continue; }
                if (review.Value<bool?>("historyChecked") != true && prefix.Any(x => x.Role == "assistant"))
                { Skip("前文 assistant 尚未逐条复核"); continue; }
                var selected = f.Answer(t, f.HasSelection ? f.Accepted : f.Target);
                var candidate = f.Candidates.FirstOrDefault(x => x.Id == (f.HasSelection ? f.Accepted : f.Target));
                string chosen = (string?)review["correctedReply"] ?? (f.ExpectedReply.Length > 0 ? f.ExpectedReply : selected);
                bool conditioned = f.Hypothesis.Length > 0 || review.Value<bool?>("newFacts") == true;
                string condition = (string?)review["condition"] ?? "";
                if (conditioned && (condition.Length == 0 || !new[] { f.Suggestion, f.Hypothesis, f.ExpectedAction, f.StopCondition, candidate?.Direction }
                    .Any(text => text?.Contains(condition, StringComparison.Ordinal) == true)))
                { Skip("新条件或假设，须独立核对新输入"); continue; }
                if (f.ExpectedReply.Length == 0 && !f.HasSelection && f.Vote != 1 && review["correctedReply"] == null)
                { Skip("只改做法或建议，转路由和工具审核"); continue; }
                if (!ChatProtocol.TryParse(chosen, f.ResultRevision != 0, out _) || Unsafe.IsMatch(chosen)) { Skip("目标回复未通过检查"); continue; }
                string question = Normalize(t.Original);
                if (Anchors(question).Any(a => external.TryGetValue(a, out var bucket) && bucket.Any(other => Similar(question, other))))
                { Skip("与已有语料近重复"); continue; }
                string signature = Normalize(string.Join("\n", prefix.Select(x => x.Content)) + "\n" + chosen);
                if (!seen.Add(signature)) { Skip("重复样本"); continue; }
                bool blind = blindGroups.Contains(family);
                string split = blind ? "blind" : "train-sft";
                // A directed candidate belongs to its editing task unless independently blind-reviewed on the original input.
                bool directed = candidate?.Direction.Length > 0 && review.Value<bool?>("blindOriginal") != true;
                if (directed)
                {
                    prompt.Add(new { role = "assistant", content = f.Answer(t, candidate!.Parent) });
                    prompt.Add(new { role = "user", content = "本次改答要求：" + candidate.Direction });
                    if (review.Value<bool?>("historyChecked") != true) { Skip("改答任务需复核前答"); continue; }
                }
                if (conditioned) prompt.Add(new { role = "user", content = condition });
                var messages = prompt.Append(new { role = "assistant", content = chosen });
                if (blind)
                {
                    files["blind"].Add(new { system = t.System, messages = prompt });
                    files["blind-key"].Add(new { row = files["blind"].Count, expected = chosen });
                }
                else files[split].Add(new { system = t.System, messages });
                files["sources"].Add(new { source, split, row = files[split].Count, author = review["correctedReply"] != null ? "reviewer" :
                    f.ExpectedReply.Length > 0 ? "player" : "model", review = (string?)review["reviewer"] });
                if (blind || conditioned || !same || !f.HasSelection || f.Votes.GetValueOrDefault(f.Rejected) != -1 || directed || chosen != selected ||
                    f.Accepted == f.Rejected || (f.Candidates.FirstOrDefault(c => c.Id == f.Rejected)?.Direction.Length > 0 && review.Value<bool?>("blindOriginal") != true)) continue;
                string rejected = f.Answer(t, f.Rejected);
                if (chosen == rejected || !ChatProtocol.TryParse(rejected, f.ResultRevision != 0, out _)) continue;
                files["train-preference"].Add(new { system = t.System, prompt, chosen, rejected });
                files["sources"].Add(new { source, split = "train-preference", row = files["train-preference"].Count });
            }
        }
        var turns = sessions.SelectMany(x => x.Dialogues).Select(d => d.Turn).OfType<ChatTurn>().DistinctBy(t => t.TurnId).ToList();
        var eligible = turns.Where(t => t.Allow).ToList();
        var signals = eligible.SelectMany(t => t.Feedback).Where(f => !f.Withdrawn).ToList();
        var votes = signals.Where(f => f.ResultRevision == 0 && f.Votes.ContainsKey("")).ToList();
        var bills = Directory.EnumerateFiles(root,"*.request.json",SearchOption.AllDirectories).Select(p => JObject.Parse(File.ReadAllText(p))).ToList();
        var report = new {
            players = sessions.Select(x => x.Group).Distinct().Count(), turns = turns.Count, authorizedTurns = eligible.Count,
            evaluatedTurns = signals.Where(f => f.Votes.Count > 0).Select(f => f.TurnId).Distinct().Count(),
            originalVotes = votes.Count, originalLikes = votes.Count(f => f.Votes[""] == 1),
            selectedTurns = signals.Where(f => f.HasSelection).Select(f => f.TurnId).Distinct().Count(),
            generatedCandidates = signals.Sum(f => f.Candidates.Count(c => c.Author == "model")),
            detailed = signals.Count(f => f.ExpectedReply.Length+f.ExpectedAction.Length+f.StopCondition.Length+f.Hypothesis.Length>0),
            actionOnly = signals.Count(f => f.ExpectedReply.Length == 0 && f.ExpectedAction.Length+f.StopCondition.Length>0),
            firstModelOutputs = bills.Sum(b => b.Value<int>("FirstAttempts")), firstValid = bills.Sum(b => b.Value<int>("FirstValid")),
            unseenFormatRetries = bills.Sum(b => b.Value<int>("FormatRetries")), runtimeFailures = bills.Count(b => (bool?)b["Result"]?["Failed"] == true),
            preferencePairs = files["train-preference"].Count, blindRows = files["blind"].Count,
            technicalUsage = bills.SelectMany(b => b["Items"] ?? new JArray()).GroupBy(i => (string?)i["Kind"])
                .Select(g => new { kind = g.Key, tokens = g.Sum(i => i.Value<long>("Tokens")) }),
            blindSince, excludedCorpora = exclusions.Select(Path.GetFileName), historyPolicy = "review every supervised assistant message"
        };
        File.WriteAllText(Path.Combine(output,"report.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
        foreach (var (name, rows) in files)
        {
            string path = Path.Combine(output, name + ".jsonl");
            File.WriteAllLines(path + ".tmp", rows.Select(x => JsonConvert.SerializeObject(x)), new UTF8Encoding(false));
            File.Move(path + ".tmp", path, true);
            Console.WriteLine($"{name}: {rows.Count}");
        }
    }

    private static readonly Regex Unsafe = new(@"data:(?:image|audio)/|image_url|-----BEGIN .*PRIVATE KEY|Bearer\s+[\w.-]+|(?:原始窗口内容|窗口清单|进程清单|process_list|window_list)\s*[:：]", RegexOptions.IgnoreCase);
    private static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant(), @"[\p{P}\s]+", "");
    private static IEnumerable<string> Anchors(string text)
    {
        if (text.Length < 8) { yield return text; yield break; }
        yield return text[..4]; yield return text[^4..]; yield return text.Substring(text.Length / 2 - 2, 4);
    }
    private static bool Similar(string a, string b)
    {
        if (a == b) return true;
        if (Math.Min(a.Length, b.Length) < 8 || Math.Min(a.Length,b.Length) < Math.Max(a.Length,b.Length) * 0.8) return false;
        var left = Enumerable.Range(0,a.Length-2).Select(i => a.Substring(i,3)).ToHashSet();
        var right = Enumerable.Range(0,b.Length-2).Select(i => b.Substring(i,3)).ToHashSet();
        return left.Intersect(right).Count() >= 0.85 * left.Union(right).Count();
    }
}
