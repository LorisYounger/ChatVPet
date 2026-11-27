using ChatVPet.ChatProcess;
using Newtonsoft.Json;
using System.Globalization;
using VPet.Plugin.ChatVPet;

namespace ChatVPet.ChatProcess.ConsoleTest;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length >= 4 && args[0] == "--export-feedback")
        {
            var rest = args.Skip(4).ToList();
            var since = rest.Find(x => x.StartsWith("--blind-since="));
            rest.RemoveAll(x => x.StartsWith("--blind-since="));
            FeedbackExport.Run(args[1], args[2], args[3], rest, since == null ? null : DateTimeOffset.Parse(since.Split('=')[1], CultureInfo.InvariantCulture));
            return;
        }
        if (args.Contains("--self-test"))
        {
            RetrievalChecks.Run();
            TurnChecks.Run();
            CacheChecks.Run();
            CompletionChecks.Run();
            return;
        }
        if (args.Length < 2 || args[0] is not ("--chat" or "--live-test" or "--context-test"))
        {
            Console.WriteLine("离线验证: --self-test\n手动联调: --chat <配置.json>\n真实接口检查: --live-test <配置.json 或 -> <测试图片.png> [用例名]\n历史压缩对照: --context-test <配置.json> [light/heavy]\n摘要使用操作 API，输出预算 8192；聊天模型只负责聊天。\n配置为 - 时从标准输入读取一行 JSON；所有游戏动作均为模拟。");
            return;
        }
        var config = JsonConvert.DeserializeObject<OpenAIChatConfig>(args[1] == "-" ? Console.ReadLine()! : File.ReadAllText(args[1]))!;
        using var chat = new ModelApiClient(config.ApiUrl, config.Model, config.ApiKey, 90, config.WebProxy);
        using var operations = string.IsNullOrWhiteSpace(config.ToolApiUrl) ? null : new ModelApiClient(config.ToolApiUrl, config.ToolModel, config.ToolApiKey, 60, config.WebProxy);
        using var embeddings = string.IsNullOrWhiteSpace(config.EmbeddingApiUrl) ? null : new ModelApiClient(config.EmbeddingApiUrl, config.EmbeddingModel, config.EmbeddingApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        using var rerank = !config.RerankEnabled || string.IsNullOrWhiteSpace(config.RerankApiUrl) ? null : new ModelApiClient(config.RerankApiUrl, config.RerankModel, config.RerankApiKey, config.RetrievalTimeoutSeconds, config.WebProxy);
        var process = new VPetChatProcess { SystemDescription = ChatProtocol.RenderSystem(useChatVPetPrompt: config.UseChatVPetPrompt) };
        process.AIAPIAskFunction = (system, messages, tools) => chat.Chat(system, messages, tools, config.Temperature, config.MaxTokens);
        if (operations != null) process.ToolAPIAskFunction = (system, messages, tools) => operations.Chat(system, messages, tools, 0.2, config.MaxTokens);
        if (operations != null) process.SummaryAPIAskFunction = (system, messages, tools) => operations.Chat(system, messages, tools, 0.2, OpenAIChatConfig.SummaryMaxTokens);
        if (embeddings != null) process.AIAPIEmbeddingFunction = embeddings.Embed;
        if (rerank != null) process.AIAPIRerankFunction = rerank.Rerank;
        process.Retrieval.EmbeddingIdentity = config.EmbeddingApiUrl + "|" + config.EmbeddingModel + "|" + config.EmbeddingVersion;
        process.Retrieval.RerankIdentity = config.RerankApiUrl + "|" + config.RerankModel + "|" + config.RerankVersion;
        process.Retrieval.QueryInstruction = config.EmbeddingInstruction;
        double balance = 10, price = 8;
        int inventory = 0;
        process.Tools.Add(new ToolUse("price", "查询面包当前售价和余额", ["钱", "买", "面包", "价格"], [], [], process.Localization)
        { ReadOnly = true, Execute = (_, _) => new() { Status = "observed", Facts = $"(模拟) 面包 (id:bread), 价格:{price}, 余额:{balance}", DisplayedItems = ["bread"] } });
        process.Tools.Add(new ToolUse("buy_item", "按玩家本轮明确要求立即购买商品入包（不自动食用）；执行前宿主复核数量、现价、余额与全部预算条件。", ["买", "购买", "面包", "预算"], [],
            [new() { Name = "target", Description = "目录中的准确商品 id", IsRequired = true },
             new() { Name = "quantity", Type = "integer", Description = "购买数量，未指定时为 1", IsRequired = true },
             new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个（从 1 开始）；无序号时省略" },
             new() { Name = "max_total", Type = "number", Description = "本轮所有购买合计预算；无限制时省略" },
             new() { Name = "max_unit", Type = "number", Description = "允许的最高单价；无限制时省略" },
             new() { Name = "min_remaining", Type = "number", Description = "购买后至少保留的余额；无限制时省略" },
             new() { Name = "exclusive_total", Type = "boolean", Description = "合计须严格低于 max_total 时传 true" },
             new() { Name = "exclusive_unit", Type = "boolean", Description = "单价须严格低于 max_unit 时传 true" }], process.Localization)
        {
            Execute = (turn, arguments) =>
            {
                if (!RequestedAction.TryParse(arguments, out var request, out var parseError))
                    return new() { Status = "skipped", Facts = parseError };
                string targetError = request.CheckTarget(request.Target, turn.OriginalDisplayedItems);
                if (targetError.Length > 0) return new() { Status = "skipped", Facts = targetError };
                if (request.Target != "bread") return new() { Status = "skipped", Facts = "没有对应商品。" };
                string error = request.CheckPurchase(price, balance, turn.Receipts.Sum(x => x.MoneySpent ?? 0));
                if (error.Length > 0) return new() { Status = "skipped", Facts = $"{error} 执行时单价:{price}, 数量:{request.Quantity}, 当前总价:{price * request.Quantity}, 余额:{balance}" };
                double total = price * request.Quantity;
                balance -= total; inventory += request.Quantity;
                return new() { Status = "applied", MoneySpent = total, Facts = $"(模拟) 已购买面包{request.Quantity}个，实付{total}，余额{balance}，库存{inventory}" };
            }
        });
        process.AllowTool = (turn, _) => turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
        if (args[0] == "--context-test")
        {
            if (operations == null)
            {
                Console.Error.WriteLine("历史压缩测试需要配置操作 API，用于生成日记和摘要。");
                Environment.ExitCode = 1;
                return;
            }
            Environment.ExitCode = ContextLiveChecks.Run(process, chat, operations,
                config.ToolModel, config,
                args.Skip(2).FirstOrDefault(x => x is "light" or "heavy"),
                () => { balance = 10; price = 8; inventory = 0; });
            return;
        }
        if (args[0] == "--live-test")
        {
            Environment.ExitCode = LiveChecks.Run(process, chat, operations, embeddings, rerank, config,
                args.ElementAtOrDefault(2), args.ElementAtOrDefault(3),
                (money, currentPrice, count) => { balance = money; price = currentPrice; inventory = count; },
                () => (balance, inventory));
            return;
        }
        Console.WriteLine("/exit 退出，/retry 只重试上一轮续答，/stats 检索统计。余额10，面包8。此入口会调用配置的模型服务。");
        void Present(ProcessResponse response)
        {
            if (response.DisplayedList != null) Console.WriteLine("[展示列表] " + response.DisplayedList);
            if (response.Reply.Length > 0) Console.WriteLine($"[{response.TurnId}] {response.Reply}");
        }
        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (input == null || input == "/exit") break;
            try
            {
                if (input == "/retry") { if (process.Dialogues.LastOrDefault()?.Turn is ChatTurn turn) process.RetryReply(turn.TurnId, Present); continue; }
                if (input == "/stats")
                {
                    var stats = process.Retrieval;
                    Console.WriteLine($"Embedding请求:{stats.EmbeddingRequests}, 重排请求:{stats.RerankRequests}, 重排候选:{stats.RerankCandidates}, 命中:{stats.CacheHits}, 未命中:{stats.CacheMisses}, P95:{stats.P95Milliseconds:F1}ms");
                    continue;
                }
                process.HostState = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                process.Ask(input, Present, new());
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }
    }
}
