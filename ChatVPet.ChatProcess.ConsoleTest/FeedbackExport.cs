using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatVPet.ChatProcess.ConsoleTest;

internal static class FeedbackExport
{
    private static readonly Regex Unsafe = new(
        @"data:(?:image|audio)/|image_url|-----BEGIN .*PRIVATE KEY|Bearer\s+[\w.-]+|(?:原始窗口内容|窗口清单|进程清单|process_list|window_list)\s*[:：]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void Run(string root, string output, IEnumerable<string>? exclusions = null)
    {
        Directory.CreateDirectory(output);

        var excludedQuestions = new HashSet<string>(StringComparer.Ordinal);
        if (exclusions != null)
        {
            foreach (string path in exclusions.Where(File.Exists))
            {
                foreach (string line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = JObject.Parse(line);
                    var user = row["messages"]?.LastOrDefault(x => (string?)x["role"] == "user");
                    string? content = (string?)user?["content"];
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    string question = content.StartsWith('[') && content.Contains('\n')
                        ? content[(content.IndexOf('\n') + 1)..]
                        : content;
                    excludedQuestions.Add(Normalize(question));
                }
            }
        }

        var sftRows = new List<object>();
        var preferenceRows = new List<object>();
        var feedbackRows = new List<object>();
        var skippedReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenSft = new HashSet<string>(StringComparer.Ordinal);
        var seenPref = new HashSet<string>(StringComparer.Ordinal);

        int players = 0;
        int totalTurns = 0;
        int authorizedTurns = 0;

        void CountSkip(string reason)
        {
            skippedReasons[reason] = skippedReasons.GetValueOrDefault(reason) + 1;
        }

        foreach (string path in Directory.EnumerateFiles(root, "*.turns.json", SearchOption.AllDirectories).Order())
        {
            players++;
            string playerId = Path.GetFileName(Path.GetDirectoryName(path)!) ?? "";
            var doc = JObject.Parse(File.ReadAllText(path));
            var dialogues = doc.GetValue("dialogues", StringComparison.OrdinalIgnoreCase)?.ToObject<List<Dialogue>>() ?? [];
            var summaries = doc.GetValue("summaries", StringComparison.OrdinalIgnoreCase)?.ToObject<List<ConversationSummary>>() ?? [];
            bool? summaryAllowed = summaries.LastOrDefault()?.Allow;

            foreach (var d in dialogues)
            {
                if (d.Turn == null) continue;
                totalTurns++;
                var t = d.Turn;
                if (!t.Allow)
                {
                    CountSkip("未授权回合");
                    continue;
                }
                authorizedTurns++;

                var stages = new List<(int Revision, string OriginalReply)>();
                if (!string.IsNullOrWhiteSpace(t.FirstReply))
                    stages.Add((0, t.FirstReply));
                foreach (var ex in t.Results.Where(r => !string.IsNullOrWhiteSpace(r.Reply)))
                    stages.Add((ex.Revision, ex.Reply));

                foreach (var (revision, originalReply) in stages)
                {
                    var prefix = t.TrainingPrefix(revision, out _, out string omission, dialogues, summaryAllowed);
                    if (prefix == null)
                    {
                        CountSkip(string.IsNullOrEmpty(omission) ? "未授权" : omission);
                        continue;
                    }

                    var prompt = prefix.Select(m => new { role = m.Role, content = m.Content }).ToList();
                    if (Unsafe.IsMatch(t.System) || prompt.Any(m => Unsafe.IsMatch(m.content)))
                    {
                        CountSkip("含敏感或原始隐私载荷");
                        continue;
                    }

                    if (excludedQuestions.Contains(Normalize(t.Original)))
                    {
                        CountSkip("命中外部排除集");
                        continue;
                    }

                    var fb = t.Feedback.LastOrDefault(x => x.ResultRevision == revision && !x.Withdrawn);
                    var source = new
                    {
                        player = playerId,
                        turnId = t.TurnId,
                        stage = revision,
                        chatModel = t.ChatModel,
                        toolModel = t.ToolModel,
                        template = t.Template,
                        feedbackId = fb?.Id,
                        feedbackRevision = fb?.Revision ?? 0
                    };

                    if (fb != null && (fb.Votes.Count > 0 || fb.HasSelection
                        || fb.Suggestion.Length + fb.ExpectedReply.Length + fb.ExpectedAction.Length + fb.StopCondition.Length + fb.Hypothesis.Length > 0))
                    {
                        feedbackRows.Add(new
                        {
                            source,
                            system = t.System,
                            prompt,
                            original = originalReply,
                            fb.Target,
                            fb.Votes,
                            fb.HasSelection,
                            fb.Accepted,
                            fb.Rejected,
                            fb.Suggestion,
                            fb.ExpectedReply,
                            fb.ExpectedAction,
                            fb.StopCondition,
                            fb.Hypothesis,
                            fb.Candidates,
                            fb.Animation,
                            fb.Fallback
                        });
                    }

                    string? chosen = null;
                    string author = "model";
                    if (fb != null && !string.IsNullOrWhiteSpace(fb.ExpectedReply))
                    {
                        chosen = fb.ExpectedReply;
                        author = "player";
                    }
                    else if (fb != null && fb.HasSelection)
                    {
                        chosen = fb.Answer(t, fb.Accepted);
                        author = fb.Accepted == "manual" ? "player" : "selected";
                    }
                    else
                    {
                        bool downvotedOriginal = fb != null && fb.Votes.TryGetValue("", out int vote) && vote < 0;
                        if (downvotedOriginal)
                        {
                            CountSkip("已点踩且无改写目标");
                            continue;
                        }
                        chosen = originalReply;
                    }

                    if (string.IsNullOrWhiteSpace(chosen)
                        || !ChatProtocol.TryParse(chosen, revision != 0, out _)
                        || Unsafe.IsMatch(chosen))
                    {
                        CountSkip("回复未通过协议或安全检查");
                        continue;
                    }

                    string sftSig = Normalize(string.Join("\n", prefix.Select(x => x.Content)) + "\n" + chosen);
                    if (seenSft.Add(sftSig))
                    {
                        var messages = prompt.Append(new { role = "assistant", content = chosen }).ToList();
                        sftRows.Add(new { system = t.System, messages, author, source });
                    }
                    else
                    {
                        CountSkip("重复 SFT 样本");
                    }

                    if (fb != null)
                    {
                        string? rejected = null;
                        if (!string.IsNullOrEmpty(fb.Rejected))
                            rejected = fb.Answer(t, fb.Rejected);
                        else if (fb.Votes.TryGetValue("", out int origVote) && origVote < 0 && chosen != originalReply)
                            rejected = originalReply;

                        if (!string.IsNullOrWhiteSpace(rejected)
                            && rejected != chosen
                            && ChatProtocol.TryParse(rejected, revision != 0, out _)
                            && !Unsafe.IsMatch(rejected))
                        {
                            string prefSig = Normalize(string.Join("\n", prefix.Select(x => x.Content)) + "\n" + chosen + "\n" + rejected);
                            if (seenPref.Add(prefSig))
                                preferenceRows.Add(new { system = t.System, prompt, chosen, rejected, source });
                        }
                    }
                }
            }
        }

        WriteJsonl(Path.Combine(output, "train-sft.jsonl"), sftRows);
        WriteJsonl(Path.Combine(output, "train-preference.jsonl"), preferenceRows);
        WriteJsonl(Path.Combine(output, "feedback.jsonl"), feedbackRows);

        var report = new
        {
            players,
            turns = totalTurns,
            authorizedTurns,
            sftRows = sftRows.Count,
            preferencePairs = preferenceRows.Count,
            feedbackRows = feedbackRows.Count,
            skippedRows = skippedReasons.Values.Sum(),
            skippedReasons
        };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(false));
        Console.WriteLine($"train-sft: {sftRows.Count}");
        Console.WriteLine($"train-preference: {preferenceRows.Count}");
        Console.WriteLine($"feedback: {feedbackRows.Count}");
    }

    private static void WriteJsonl(string path, IEnumerable<object> rows)
    {
        string tmp = path + ".tmp";
        File.WriteAllLines(tmp, rows.Select(x => JsonConvert.SerializeObject(x)), new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }

    private static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant(), @"[\p{P}\s]+", "");
}
