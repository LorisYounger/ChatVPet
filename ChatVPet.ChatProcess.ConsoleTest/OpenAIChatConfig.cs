namespace ChatVPet.ChatProcess.ConsoleTest;

public class OpenAIChatConfig
{
    public string Localization { get; set; } = "zh-Hans";
    public string ApiKey { get; set; } = "";
    public string ApiUrl { get; set; } = "https://api.openai.com/v1";
    public string EmbeddingApiUrl { get; set; } = "https://api.openai.com/v1";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string EmbeddingApiKey { get; set; } = "";
    public string WebProxy { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string SystemPrompt { get; set; } = "你是一个用于测试 ChatVPet.ChatProcess 的助手，请根据上下文回答。";
    public double Temperature { get; set; } = 1;
    public int MaxTokens { get; set; } = 2048;
    public double PresencePenalty { get; set; } = 1;
    public double FrequencyPenalty { get; set; } = 0.2;
}
