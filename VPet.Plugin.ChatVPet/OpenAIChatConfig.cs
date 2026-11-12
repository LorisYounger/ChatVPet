namespace VPet.Plugin.ChatVPet
{
    public class OpenAIChatConfig
    {
        public string ApiKey { get; set; } = "";
        public string ApiUrl { get; set; } = "";
        public string EmbeddingApiUrl { get; set; } = "";
        public string EmbeddingModel { get; set; } = "Qwen/Qwen3-Embedding-0.6B";
        public string EmbeddingApiKey { get; set; } = "";
        public string EmbeddingVersion { get; set; } = "";
        public string EmbeddingInstruction { get; set; } = "Given a conversation, retrieve relevant knowledge, memories and game tools.";
        public string RerankApiUrl { get; set; } = "";
        public string RerankApiKey { get; set; } = "";
        public string RerankModel { get; set; } = "Qwen/Qwen3-Reranker-0.6B";
        public string RerankVersion { get; set; } = "";
        public bool RerankEnabled { get; set; } = true;
        public int RetrievalTimeoutSeconds { get; set; } = 20;
        public string ToolApiUrl { get; set; } = "";
        public string ToolApiKey { get; set; } = "";
        public string ToolModel { get; set; } = "";
        public bool ScreenshotEnabled { get; set; }
        public string ScreenshotScope { get; set; } = "pet";
        public bool AllowQuit { get; set; }

        public string WebProxy { get; set; } = "";
        public string Model { get; set; } = "";
        public double Temperature { get; set; } = 1;
        public int MaxTokens { get; set; } = 2048;
    }
}
