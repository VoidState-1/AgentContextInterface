namespace ACI.LLM;

public class ParsedAction
{
    public required string WindowId { get; set; }

    public required string ActionId { get; set; }

    public Dictionary<string, object>? Parameters { get; set; }
}