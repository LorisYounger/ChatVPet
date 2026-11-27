namespace ChatVPet.ChatProcess;

/// <summary>A fixed recap of the continuous conversation through one stored dialogue.</summary>
public class ConversationSummary
{
    public string SaveId { get; set; } = "default";
    public bool Allow { get; set; }
    public string Text { get; set; } = "";
    public string ThroughDialogueId { get; set; } = "";
}
