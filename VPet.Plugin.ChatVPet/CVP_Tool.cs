using ChatVPet.ChatProcess;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VPet.Plugin.ChatVPet;

public partial class CVPPlugin
{
    private ModelApiClient? chatApi, toolApi, embeddingApi, rerankApi;
    public long ChatTokens, ToolTokens, SummaryTokens;

    public void ConfigureModels()
    {
        var config = OpenAIConfig!;
        chatApi?.Dispose(); toolApi?.Dispose(); embeddingApi?.Dispose(); rerankApi?.Dispose();
        chatApi = string.IsNullOrWhiteSpace(config.ApiUrl) ? null : new ModelApiClient(config.ApiUrl, config.Model, config.ApiKey, 90, config.WebProxy);
        toolApi = string.IsNullOrWhiteSpace(config.ToolApiUrl) ? null : new ModelApiClient(config.ToolApiUrl, config.ToolModel, config.ToolApiKey, 60, config.WebProxy);
        embeddingApi = string.IsNullOrWhiteSpace(config.EmbeddingApiUrl) ? null : new ModelApiClient(config.EmbeddingApiUrl, config.EmbeddingModel, config.EmbeddingApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        rerankApi = !config.RerankEnabled || string.IsNullOrWhiteSpace(config.RerankApiUrl) ? null : new ModelApiClient(config.RerankApiUrl, config.RerankModel, config.RerankApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        var process = VPetChatProcess;
        process.SystemDescription = ChatProtocol.RenderSystem(MW.Core.Save!.Name, MW.Core.Save!.HostName);
        process.AIAPIAskFunction = OpenAIAsk;
        process.ToolAPIAskFunction = toolApi == null ? null : (system, history, input, tools) =>
        {
            var result = toolApi.Chat(system, history, input, tools, 0.2, config.MaxTokens);
            ToolTokens += result.Tokens;
            return result;
        };
        process.SummaryAPIAskFunction = chatApi == null ? null : (system, history, input, tools) =>
        {
            var result = chatApi.Chat(system, history, input, tools, 0.2, config.MaxTokens);
            SummaryTokens += result.Tokens;
            return result;
        };
        process.AIAPIEmbeddingFunction = embeddingApi == null ? null : embeddingApi.Embed;
        process.AIAPIRerankFunction = rerankApi == null ? null : rerankApi.Rerank;
        process.Retrieval.EmbeddingIdentity = config.EmbeddingApiUrl + "|" + config.EmbeddingModel + "|" + config.EmbeddingVersion;
        process.Retrieval.RerankIdentity = config.RerankApiUrl + "|" + config.RerankModel + "|" + config.RerankVersion;
        process.Retrieval.QueryInstruction = config.EmbeddingInstruction;
        process.Retrieval.Clear();
        W2VEngine.ClearVectors(process.KnowledgeDataBases);
        W2VEngine.ClearVectors(process.Tools);
        W2VEngine.ClearVectors(process.Dialogues);
        W2VEngine.ClearVectors(process.DiaryEntries);
        process.MaxHistoryCount = MaxHistoryCount;
        process.MaxKnowledgeCount = MaxKnowledgeCount;
        process.MaxToolCount = MaxToolCount;
        process.MaxToolRecallCount = MaxRecallCount;
        process.MaxHistoryBeforeCompress = MaxHistoryBeforeCompress;
        process.DiaryKeepRecentCount = DiaryKeepRecentCount;
        process.DiaryDecayRate = DiaryDecayRate;
    }

    public VPetChatProcess.AIAPIAskResult OpenAIAsk(string system, List<string[]> history, string input, List<ToolUse> tools)
    {
        if (chatApi == null) throw new InvalidOperationException("请先配置聊天 API。");
        var result = chatApi.Chat(system, history, input, tools, OpenAIConfig!.Temperature, OpenAIConfig.MaxTokens);
        temptoken = (int)result.Tokens;
        ChatTokens += temptoken; TotalTokensUsage += temptoken; TokenCount = temptoken;
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
}
