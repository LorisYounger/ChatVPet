namespace ChatVPet.ChatProcess;

public sealed class ReplyCandidate
{
    public string Id { get; set; } = "";
    public string Author { get; set; } = "model";
    public double Temperature { get; set; } = 1;
    public int? Seed { get; set; }
    public string Parent { get; set; } = "";
    public string Direction { get; set; } = "";
    public string Raw { get; set; } = "";
    public bool Duplicate { get; set; }
}

public sealed class ReplyFeedback
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TurnId { get; set; } = "";
    // Zero identifies the initial reply. Positive revisions identify a frozen result exchange.
    public int ResultRevision { get; set; }
    public int Revision { get; set; }
    public string Target { get; set; } = "";
    public Dictionary<string,int> Votes { get; set; } = [];
    public int Vote => Votes.GetValueOrDefault(Target);
    public string Rejected { get; set; } = "";
    public bool HasSelection { get; set; }
    public string Accepted { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string ExpectedReply { get; set; } = "";
    public string ExpectedAction { get; set; } = "";
    public string StopCondition { get; set; } = "";
    public string Hypothesis { get; set; } = "";
    public string Animation { get; set; } = "";
    public bool Fallback { get; set; }
    public bool Withdrawn { get; set; }
    public List<ReplyCandidate> Candidates { get; set; } = [];

    public string Answer(ChatTurn turn, string id) => id.Length > 0 ? Candidates.First(x => x.Id == id).Raw :
        ResultRevision == 0 ? turn.FirstReply : turn.Results.First(x => x.Revision == ResultRevision).Reply;
}

public partial class ChatTurn
{
    public List<ReplyFeedback> Feedback { get; set; } = [];

    public List<ChatMessage>? TrainingPrefix(int revision, out bool same, out string omission)
    {
        same = true;
        omission = "";
        if (!Allow || !InputAuthorized || Observation.Length > 0) { omission = "未授权或不可分离的输入"; return null; }
        var prefix = ReplyPrefix(revision);
        if (revision == 0) return prefix;
        if (!ResultsAuthorized) { omission = "回执引用未授权"; return null; }
        int index = History.Count + 2;
        foreach (var exchange in Results)
        {
            if (exchange.TrainingInput.Length == 0) { omission = "缺少可分离的工具文字结论"; return null; }
            same &= exchange.Input == exchange.TrainingInput;
            prefix[index] = new("user", exchange.TrainingInput, true);
            if (exchange.Revision == revision) break;
            index += 2;
        }
        return prefix;
    }

    public List<ChatMessage> ReplyPrefix(int revision)
    {
        List<ChatMessage> messages = [.. History, new("user", InitialInput)];
        if (revision == 0) return messages;
        messages.Add(new("assistant", Initial.Raw));
        foreach (var exchange in Results)
        {
            messages.Add(new("user", exchange.Input, true));
            if (exchange.Revision == revision && exchange.Reply.Length > 0) return messages;
            messages.Add(new("assistant", exchange.Reply));
        }
        throw new InvalidOperationException("找不到这个已完成的回执版本。");
    }
}

public partial class VPetChatProcess
{
    // The caller snapshots the prefix before releasing the session and owns the model for this request.
    public ChatHeader PreviewReply(ChatTurn turn, int revision, string direction, string parent = "")
    {
        var messages = turn.ReplyPrefix(revision);
        if (!string.IsNullOrWhiteSpace(direction))
        {
            messages.Add(new("assistant", parent.Length > 0 ? parent : revision == 0 ? turn.FirstReply : turn.Results.First(r => r.Revision == revision).Reply));
            messages.Add(new("user", ChatProtocol.RevisionRequestPrefix(turn.Language) + direction));
        }
        return GenerateReply(turn, messages, revision != 0);
    }

    public bool AdoptReply(ChatTurn turn, int version, int revision, string raw)
    {
        lock (turnGate)
        {
            var dialogue = Dialogues.LastOrDefault(x => x.SaveId == SaveId);
            if (dialogue?.Turn != turn || turn.SaveId != SaveId || turn.AnswerVersion != version || revision != 0
                || !turn.OperationsFinished || turn.Receipts.Count != 0 || !ChatProtocol.TryParse(turn.Initial.Raw, true, out _)
                || Dialogues.Any(d => d.SaveId == SaveId && d.Turn?.LastSequence > turn.Sequence)
                || !GetUncompressedHistory(GetSummary()).Contains(dialogue) || !ChatProtocol.TryParse(raw, true, out var header)) return false;
            turn.Initial = header;
            turn.AnswerVersion++;
            dialogue.Answer = raw;
            dialogue.KeyWords = [string.Join(" ", Localization.WordSplit(dialogue.Question)), string.Join(" ", Localization.WordSplit(raw))];
            Checkpoint?.Invoke();
            return true;
        }
    }
}
