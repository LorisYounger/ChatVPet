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

    public List<ChatMessage>? TrainingPrefix(int revision, out bool same, out string omission,
        IReadOnlyList<Dialogue>? sessionDialogues = null, bool? summaryAllowed = null)
    {
        same = true;
        omission = "";
        if (!Allow) { omission = "本条回复未获训练授权"; return null; }

        List<ChatMessage> history;
        if (TrainingHistory != null)
        {
            history = [.. TrainingHistory];
            same &= TrainingHistory.Count == History.Count;
        }
        else if (InputAuthorized)
        {
            history = [.. History];
        }
        else if (sessionDialogues != null && History.Count > 0)
        {
            history = FilterAuthorizedHistory(History, sessionDialogues, TurnId);
            same &= history.Count == History.Count;
        }
        else
        {
            history = [];
            same &= History.Count == 0;
        }

        string initialInput;
        if (TrainingInitialInput.Length > 0)
        {
            initialInput = TrainingInitialInput;
            same &= initialInput == InitialInput;
        }
        else if (InputAuthorized)
        {
            initialInput = InitialInput;
        }
        else
        {
            string authorizedSummary = summaryAllowed == true ? Summary : "";
            initialInput = ChatProtocol.UserMessage(Original, State, reference: "", observation: Observation,
                summary: authorizedSummary, language: Language, events: HostEvents);
            same &= initialInput == InitialInput;
        }

        List<ChatMessage> prefix = [.. history, new("user", initialInput)];
        if (revision == 0) return prefix;

        prefix.Add(new("assistant", Initial.Raw));
        foreach (var exchange in Results)
        {
            string trainingInput = exchange.TrainingInput.Length > 0 ? exchange.TrainingInput : exchange.Input;
            same &= exchange.Input == trainingInput;
            prefix.Add(new("user", trainingInput, true));
            if (exchange.Revision == revision && exchange.Reply.Length > 0) return prefix;
            prefix.Add(new("assistant", exchange.Reply));
        }
        throw new InvalidOperationException("找不到这个已完成的回执版本。");
    }

    private static List<ChatMessage> FilterAuthorizedHistory(List<ChatMessage> history, IReadOnlyList<Dialogue> sessionDialogues, string currentTurnId)
    {
        var authorizedContents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in sessionDialogues)
        {
            if (d.DialogueId == currentTurnId || d.Turn is not { Allow: true }) continue;
            foreach (var msg in d.ToMessages(compactHistory: true))
                authorizedContents.Add(msg.Content);
        }
        var filtered = new List<ChatMessage>(history.Count);
        int i = 0;
        while (i < history.Count)
        {
            if (history[i].Role == "user" && i + 1 < history.Count && history[i + 1].Role == "assistant")
            {
                if (authorizedContents.Contains(history[i].Content) && authorizedContents.Contains(history[i + 1].Content))
                {
                    filtered.Add(history[i]);
                    filtered.Add(history[i + 1]);
                }
                i += 2;
            }
            else
            {
                if (authorizedContents.Contains(history[i].Content))
                    filtered.Add(history[i]);
                i++;
            }
        }
        return filtered;
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
            var dialogue = Dialogues.LastOrDefault();
            if (dialogue?.Turn != turn || turn.AnswerVersion != version || revision != 0
                || !turn.OperationsFinished || turn.Receipts.Count != 0 || !ChatProtocol.TryParse(turn.Initial.Raw, true, out _)
                || Dialogues.Any(d => d.Turn?.LastSequence > turn.Sequence)
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
