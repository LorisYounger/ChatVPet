using ChatVPet.ChatProcess;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VPet.Plugin.ChatVPet;

public partial class CVPPlugin
{
    private ModelApiClient? chatApi, toolApi, embeddingApi, rerankApi, searchApi;
    private string activeSearchMode = "";
    public long ChatTokens, ToolTokens, SummaryTokens;
    public long ChatPromptTokens, ChatCachedTokens;
    public bool HasChatCacheUsage;
    public Action<string, ModelApiTrace>? TraceApi;

    public void ConfigureModels()
    {
        var config = OpenAIConfig!;
        chatApi?.Dispose(); toolApi?.Dispose(); embeddingApi?.Dispose(); rerankApi?.Dispose(); searchApi?.Dispose();
        chatApi = string.IsNullOrWhiteSpace(config.ApiUrl) ? null : new ModelApiClient(config.ApiUrl, config.Model, config.ApiKey, 90, config.WebProxy);
        toolApi = string.IsNullOrWhiteSpace(config.ToolApiUrl) ? null : new ModelApiClient(config.ToolApiUrl, config.ToolModel, config.ToolApiKey, 60, config.WebProxy);
        embeddingApi = string.IsNullOrWhiteSpace(config.EmbeddingApiUrl) ? null : new ModelApiClient(config.EmbeddingApiUrl, config.EmbeddingModel, config.EmbeddingApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        rerankApi = !config.RerankEnabled || string.IsNullOrWhiteSpace(config.RerankApiUrl) ? null : new ModelApiClient(config.RerankApiUrl, config.RerankModel, config.RerankApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        bool customSearchUrl = !string.IsNullOrWhiteSpace(config.WebSearchApiUrl);
        string searchUrl = customSearchUrl ? config.WebSearchApiUrl : config.ToolApiUrl;
        activeSearchMode = ModelApiClient.ResolveSearchMode(config.WebSearchMode, searchUrl);
        if (activeSearchMode == "searxng" && !customSearchUrl) activeSearchMode = "";
        string searchKey = !string.IsNullOrWhiteSpace(config.WebSearchApiKey) ? config.WebSearchApiKey : (customSearchUrl || activeSearchMode == "searxng" ? "" : config.ToolApiKey);
        string searchModel = !string.IsNullOrWhiteSpace(config.WebSearchModel) ? config.WebSearchModel : config.ToolModel;
        searchApi = activeSearchMode.Length == 0 || string.IsNullOrWhiteSpace(searchUrl) ? null : new ModelApiClient(searchUrl, searchModel, searchKey, Math.Max(config.RetrievalTimeoutSeconds, 30), config.WebProxy);
        if (chatApi != null) chatApi.Trace = trace => TraceApi?.Invoke("chat", trace);
        if (toolApi != null) toolApi.Trace = trace => TraceApi?.Invoke("operations", trace);
        if (embeddingApi != null) embeddingApi.Trace = trace => TraceApi?.Invoke("embedding", trace);
        if (rerankApi != null) rerankApi.Trace = trace => TraceApi?.Invoke("rerank", trace);
        if (searchApi != null) searchApi.Trace = trace => TraceApi?.Invoke("search", trace);
        var process = VPetChatProcess;
        process.SystemDescription = ChatProtocol.RenderSystem(MW.Core.Save!.Name, MW.Core.Save!.HostName, config.UseChatVPetPrompt);
        process.AIAPIAskFunction = OpenAIAsk;
        process.ToolAPIAskFunction = toolApi == null ? null : (system, messages, tools) =>
        {
            var result = toolApi.Chat(system, messages, tools, 0.2, config.MaxTokens);
            ToolTokens += result.Tokens;
            return result;
        };
        process.SummaryAPIAskFunction = toolApi == null ? null : (system, messages, tools) =>
        {
            var result = toolApi.Chat(system, messages, tools, 0.2, OpenAIChatConfig.SummaryMaxTokens);
            SummaryTokens += result.Tokens;
            return result;
        };
        process.AIAPIEmbeddingFunction = embeddingApi == null ? null : embeddingApi.Embed;
        process.AIAPIRerankFunction = rerankApi == null ? null : rerankApi.Rerank;
        var activeSearchApi = searchApi;
        string mode = activeSearchMode;
        process.WebSearchFunction = activeSearchApi == null ? null : (original, query, count) =>
        {
            long before = activeSearchApi.Tokens;
            string text = activeSearchApi.WebSearch(mode, query, count, original);
            ToolTokens += activeSearchApi.Tokens - before;
            return text.Length > 0
                ? new HostReceipt { Status = "observed", Facts = text }
                : new HostReceipt { Status = "not_found", Facts = "未检索到相关网页信息。" };
        };
        process.Retrieval.EmbeddingIdentity = config.EmbeddingApiUrl + "|" + config.EmbeddingModel + "|" + config.EmbeddingVersion;
        process.Retrieval.RerankIdentity = config.RerankApiUrl + "|" + config.RerankModel + "|" + config.RerankVersion;
        process.Retrieval.QueryInstruction = config.EmbeddingInstruction;
        process.ClearRetrievalCache();
        process.MaxHistoryCount = MaxHistoryCount;
        process.MaxKnowledgeCount = MaxKnowledgeCount;
        process.MaxToolCount = MaxToolCount;
        process.MaxToolRecallCount = MaxRecallCount;
        process.MaxHistoryBeforeCompress = MaxHistoryBeforeCompress;
        process.DiaryKeepRecentCount = DiaryKeepRecentCount;
        process.DiaryDecayRate = DiaryDecayRate;
        process.HistoryCharacterBudget = HistoryCharacterBudget;
        process.SummaryCharacterLimit = SummaryCharacterLimit;
        process.RequestCharacterLimit = RequestCharacterLimit;
    }

    public VPetChatProcess.AIAPIAskResult OpenAIAsk(string system, IReadOnlyList<ChatMessage> messages, List<ToolUse> tools)
    {
        if (chatApi == null) throw new InvalidOperationException("请先配置聊天 API。");
        var result = chatApi.Chat(system, messages, tools, OpenAIConfig!.Temperature, OpenAIConfig.MaxTokens);
        temptoken = (int)result.Tokens;
        ChatTokens += temptoken; TotalTokensUsage += temptoken; TokenCount = temptoken;
        if (result.CachedTokens.HasValue && result.PromptTokens.HasValue)
        {
            HasChatCacheUsage = true;
            ChatPromptTokens += result.PromptTokens.Value;
            ChatCachedTokens += result.CachedTokens.Value;
        }
        return result;
    }

    public string ObserveAttachedImage(string question, byte[] image)
    {
        if (toolApi == null) return "未配置视觉操作 API，无法读取图片。";
        try { return toolApi.Observe(question, image, image.Length > 2 && image[0] == 0xff && image[1] == 0xd8 ? "image/jpeg" : "image/png"); }
        catch (Exception) { return "图片识别服务不可用，未能读取图中内容。"; }
    }

    public string TestRetrievalApis()
    {
        if (embeddingApi == null || rerankApi == null) return "请填写并启用向量和重排端点。";
        var vectors = embeddingApi.Embed(["停一下工作", "结束活动"]);
        var scores = rerankApi.Rerank("停一下工作", ["结束活动", "开始工作", "购买面包"]);
        if (scores[0] <= scores[1] || scores[0] <= scores[2])
            throw new InvalidOperationException("重排返回的原候选索引或排序不符合试题预期，请检查评分接口。");
        return $"向量: {vectors.Length} 条，{vectors[0].Length} 维。重排: 结束活动 > 其他候选，分数 {string.Join(", ", scores.Select(x => x.ToString("F3")))}。";
    }

    public string TestWebSearchApi()
    {
        if (searchApi == null || activeSearchMode.Length == 0)
            return "联网搜索未启用或未识别到可用端点（请检查模式与 URL 后先保存设置）。";
        string result = searchApi.WebSearch(activeSearchMode, "VPet 虚拟桌宠模拟器", 3);
        if (result.Length == 0) return $"模式 {activeSearchMode} 请求成功，但未返回结果。";
        string preview = result.Length > 180 ? result[..180] + "…" : result;
        return $"模式 {activeSearchMode} 可用：\n{preview}";
    }
}
