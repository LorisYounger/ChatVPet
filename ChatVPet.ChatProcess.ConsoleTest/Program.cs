using ChatVPet.ChatProcess;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using static ChatVPet.ChatProcess.ToolUse;

namespace ChatVPet.ChatProcess.ConsoleTest;

internal sealed class Program
{
    private static readonly string ConfigPath = GetConfigPath();
    private static readonly string[] BaseKnowledge =
    [
        "项目: ChatVPet.ChatProcess",
        "本程序用于手动测试 Ask/ToolUse/Embedding/历史回填流程。",
        "当工具被触发时，会在控制台打印日志。"
    ];

    private readonly VPetChatProcess _process = new();
    private OpenAIChatConfig _config = new();

    private readonly object _clientLock = new();
    private ChatClient? _chatClient;
    private EmbeddingClient? _embeddingClient;
    private HttpClient? _proxyHttpClient;
    private HttpClientHandler? _proxyHttpHandler;

    private static void Main()
    {
        new Program().Run();
    }

    private void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== ChatVPet.ChatProcess 控制台测试 ===");
        Console.WriteLine($"配置文件: {ConfigPath}");

        LoadOrInitConfig();
        InitProcess();

        Console.WriteLine("输入消息直接发送。命令: /help /config /save /reload /system /tools /history /exit");
        while (true)
        {
            Console.Write("\n[Input]> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.StartsWith('/'))
            {
                if (HandleCommand(input.Trim()))
                    break;
                continue;
            }

            SendMessage(input);
        }
    }

    private void InitProcess()
    {
        _process.AIAPIAskFunction = OpenAIAsk;
        _process.AIAPIEmbeddingFunction = OpenAIEmbeddings;
        _process.CalImportanceFunction = message =>
        {
            Console.WriteLine($"[Stage:Importance] Q={message[0]} | A={message[1]}");
            return 0.5f;
        };
        _process.W2VEngine = new W2VEngine(_process);//提前初始化，避免在获取向量时才创建客户端导致的性能问题
        ApplyConfigToProcess(rebuildIndexes: true);
        _process.W2VEngine.GetQueryVector(_process.KnowledgeDataBases);
        _process.W2VEngine.GetQueryVector(_process.Tools);
        _process.W2VEngine.GetQueryVector(_process.Dialogues);
        Console.WriteLine($"[Stage:Init] Tool 数量: {_process.Tools.Count}, Knowledge 数量: {_process.KnowledgeDataBases.Count}");
    }

    private bool HandleCommand(string command)
    {
        switch (command)
        {
            case "/help":
                Console.WriteLine("/help      查看帮助");
                Console.WriteLine("/config    交互修改 OpenAI 配置");
                Console.WriteLine("/save      保存当前配置");
                Console.WriteLine("/reload    从文件重新加载配置");
                Console.WriteLine("/system    查看/设置系统提示词");
                Console.WriteLine("/tools     查看当前工具列表");
                Console.WriteLine("/history   查看当前对话历史条数");
                Console.WriteLine("/exit      退出");
                return false;
            case "/config":
                EditConfigInteractive();
                ResetClients();
                ApplyConfigToProcess(rebuildIndexes: true);
                return false;
            case "/save":
                SaveConfig();
                return false;
            case "/reload":
                LoadConfig();
                ResetClients();
                ApplyConfigToProcess(rebuildIndexes: true);
                return false;
            case "/system":
                Console.WriteLine("当前 SystemPrompt:\n" + _process.SystemDescription);
                Console.Write("是否修改? (y/N): ");
                if (ReadYes())
                {
                    Console.Write("输入新 SystemPrompt: ");
                    _config.SystemPrompt = Console.ReadLine() ?? "";
                    _process.SystemDescription = _config.SystemPrompt;
                    Console.WriteLine("已更新 SystemPrompt。可执行 /save 保存。");
                }
                return false;
            case "/tools":
                for (var i = 0; i < _process.Tools.Count; i++)
                {
                    var t = _process.Tools[i];
                    Console.WriteLine($"[{i}] {t.Code} - {t.Descriptive}");
                }
                return false;
            case "/history":
                Console.WriteLine($"Dialogues: {_process.Dialogues.Count}");
                return false;
            case "/exit":
                return true;
            default:
                Console.WriteLine("未知命令，输入 /help 查看可用命令。");
                return false;
        }
    }

    private void SendMessage(string message)
    {
        Console.WriteLine("[Stage:Ask] 开始调用 ChatProcess.Ask");
        var control = new ProcessControl();
        try
        {
            _process.Ask(message, response =>
            {
                Console.WriteLine($"[Stage:ProcessResponse] pos={response.ListPosition}, end={response.IsEnd}, error={response.IsError}");
                if (!string.IsNullOrWhiteSpace(response.Reply))
                    Console.WriteLine($"[Reply]\n{response.Reply}");
            }, control);

            Console.WriteLine($"[Stage:Ask] 完成，本次后历史条数: {_process.Dialogues.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Stage:Error] " + ex);
        }
    }

    private VPetChatProcess.AIAPIAskResult OpenAIAsk(string system, List<string[]> histories, string message, List<ToolUse> tools)
    {
        Console.WriteLine($"[Stage:OpenAIAsk] systemLen={system.Length}, history={histories.Count}, inputLen={message.Length}, selectedTools={tools.Count}");

        EnsureConfig();

        var messages = new List<ChatMessage> { new SystemChatMessage(system) };
        foreach (var h in histories)
        {
            messages.Add(new UserChatMessage(h[0]));
            messages.Add(new AssistantChatMessage(h[1]));
        }
        messages.Add(new UserChatMessage(message));

        var options = new ChatCompletionOptions
        {
            Temperature = (float)_config.Temperature,
            MaxOutputTokenCount = _config.MaxTokens,
            PresencePenalty = (float)_config.PresencePenalty,
            FrequencyPenalty = (float)_config.FrequencyPenalty
        };

        foreach (var tool in tools)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                functionName: tool.Code,
                functionDescription: tool.Descriptive,
                functionParameters: BinaryData.FromObjectAsJson(tool.Parameters)));
        }

        var completion = GetChatClient().CompleteChat(messages, options).Value;
        var reply = string.Concat(completion.Content.Select(x => x.Text));
        var toolCalls = ParseToolCalls(completion.ToolCalls);

        Console.WriteLine($"[Stage:OpenAIAsk] usageTokens={completion.Usage?.TotalTokenCount ?? 0}, toolCallCount={toolCalls.Count}");

        return new VPetChatProcess.AIAPIAskResult
        {
            Reply = reply,
            ToolCalls = toolCalls
        };
    }

    private float[][] OpenAIEmbeddings(IEnumerable<string> texts)
    {
        EnsureConfig();
        var textList = texts.ToList();
        Console.WriteLine($"[Stage:Embedding] batchCount={textList.Count}");
        try
        {
            var result = GetEmbeddingClient().GenerateEmbeddings(textList).Value;
            return result.Select(x => x.ToFloats().ToArray()).ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Embedding 调用失败，batchCount={textList.Count}", ex);
        }
    }

    private static List<ToolCallResult> ParseToolCalls(IReadOnlyList<ChatToolCall> openAIToolCalls)
    {
        var result = new List<ToolCallResult>();
        if (openAIToolCalls == null)
            return result;

        foreach (var call in openAIToolCalls)
        {
            var code = call.FunctionName;
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var args = new Dictionary<string, string>();
            var rawArgs = call.FunctionArguments.ToString();
            if (!string.IsNullOrWhiteSpace(rawArgs))
            {
                try
                {
                    var argsObj = JObject.Parse(rawArgs);
                    foreach (var p in argsObj.Properties())
                    {
                        args[p.Name] = p.Value.Type == JTokenType.String
                            ? p.Value.ToString()
                            : p.Value.ToString(Formatting.None);
                    }
                }
                catch (JsonReaderException)
                {
                    args["raw"] = rawArgs;
                }
            }

            result.Add(new ToolCallResult
            {
                Code = code,
                Args = args
            });
        }

        return result;
    }

    private string? ToolDance(Dictionary<string, string> args)
    {
        Console.WriteLine("[Stage:ToolUse] dance 被调用");
        return null;
    }

    private string? ToolIdle(Dictionary<string, string> args)
    {
        Console.WriteLine("[Stage:ToolUse] idle 被调用");
        return null;
    }

    private string? ToolMove(Dictionary<string, string> args)
    {
        Console.WriteLine("[Stage:ToolUse] move 被调用");
        return null;
    }

    private string? ToolModifyState(Dictionary<string, string> args)
    {
        Console.WriteLine("[Stage:ToolUse] modifystate 被调用，参数: " + JsonConvert.SerializeObject(args));
        return null;
    }

    private void LoadOrInitConfig()
    {
        if (File.Exists(ConfigPath))
        {
            LoadConfig();
            return;
        }

        Console.WriteLine("未检测到配置文件，开始初始化。\n");
        EditConfigInteractive();
        SaveConfig();
    }

    private void LoadConfig()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            _config = JsonConvert.DeserializeObject<OpenAIChatConfig>(json) ?? new OpenAIChatConfig();
            Console.WriteLine("已加载配置。当前模型: " + _config.Model);
        }
        catch (JsonException ex)
        {
            Console.WriteLine("配置文件格式无效或已损坏，无法读取: " + ex.Message);
            var backupPath = TryBackupInvalidConfig();
            if (!string.IsNullOrWhiteSpace(backupPath))
                Console.WriteLine("已将原配置备份到: " + backupPath);
            else
                Console.WriteLine("请删除或修复现有配置文件后重试。");

            _config = new OpenAIChatConfig();
            Console.WriteLine("将重新初始化配置。\n");
            EditConfigInteractive();
            SaveConfig();
        }
        catch (IOException ex)
        {
            Console.WriteLine("读取配置文件失败: " + ex.Message);
            Console.WriteLine("将使用默认配置并重新初始化。\n");
            _config = new OpenAIChatConfig();
            EditConfigInteractive();
            SaveConfig();
        }
    }

    private void SaveConfig()
    {
        var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
        Console.WriteLine("配置已保存。路径: " + ConfigPath);
    }

    private void EditConfigInteractive()
    {
        Console.WriteLine("直接回车=保持当前值。\n");
        _config.Localization = Prompt("Localization(zh-Hans/zh-Hant/en)", _config.Localization);
        _config.ApiKey = Prompt("ApiKey", _config.ApiKey, secret: true);
        _config.ApiUrl = Prompt("ApiUrl", _config.ApiUrl);
        _config.Model = Prompt("Model", _config.Model);
        var defaultEmbeddingKey = string.IsNullOrWhiteSpace(_config.EmbeddingApiKey) ? _config.ApiKey : _config.EmbeddingApiKey;
        _config.EmbeddingApiKey = Prompt("EmbeddingApiKey", defaultEmbeddingKey, secret: true);
        _config.EmbeddingApiUrl = Prompt("EmbeddingApiUrl", _config.EmbeddingApiUrl);
        _config.EmbeddingModel = Prompt("EmbeddingModel", _config.EmbeddingModel);
        _config.WebProxy = Prompt("WebProxy", _config.WebProxy);
        _config.SystemPrompt = Prompt("SystemPrompt", _config.SystemPrompt);

        if (double.TryParse(Prompt("Temperature", _config.Temperature.ToString()), out var temperature))
            _config.Temperature = temperature;
        if (int.TryParse(Prompt("MaxTokens", _config.MaxTokens.ToString()), out var maxTokens))
            _config.MaxTokens = maxTokens;
        if (double.TryParse(Prompt("PresencePenalty", _config.PresencePenalty.ToString()), out var pp))
            _config.PresencePenalty = pp;
        if (double.TryParse(Prompt("FrequencyPenalty", _config.FrequencyPenalty.ToString()), out var fp))
            _config.FrequencyPenalty = fp;
    }

    private static string Prompt(string field, string current, bool secret = false)
    {
        var display = secret ? Mask(current) : current;
        Console.Write($"{field} [{display}]: ");
        var input = secret ? ReadSecretLine() : Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
    }

    private static string ReadSecretLine()
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? "";

        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                    buffer.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                buffer.Append(key.KeyChar);
        }
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        if (value.Length <= 6)
            return new string('*', value.Length);
        return value[..3] + "***" + value[^3..];
    }

    private static bool ReadYes()
    {
        var text = Console.ReadLine();
        return string.Equals(text, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureConfig()
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
            throw new InvalidOperationException("ApiKey 为空，请执行 /config 配置后重试。");
        if (string.IsNullOrWhiteSpace(_config.EmbeddingApiKey))
            _config.EmbeddingApiKey = _config.ApiKey;
    }

    private EmbeddingClient GetEmbeddingClient()
    {
        lock (_clientLock)
        {
            _embeddingClient ??= new EmbeddingClient(
                _config.EmbeddingModel,
                new ApiKeyCredential(_config.EmbeddingApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(BuildOpenAIEndpoint(_config.EmbeddingApiUrl)) });
            return _embeddingClient;
        }
    }

    private static ILocalization BuildLocalization(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "zh-hant" => new ILocalization.LChineseTraditional(),
            "en" => new ILocalization.LEnglish(),
            _ => new ILocalization.LChineseSimple()
        };
    }

    private ChatClient GetChatClient()
    {
        lock (_clientLock)
        {
            if (_chatClient == null)
            {
                var endpoint = BuildOpenAIEndpoint(_config.ApiUrl);
                var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };

                if (!string.IsNullOrWhiteSpace(_config.WebProxy))
                {
                    _proxyHttpHandler = new HttpClientHandler
                    {
                        Proxy = new WebProxy(_config.WebProxy),
                        UseProxy = true
                    };
                    _proxyHttpClient = new HttpClient(_proxyHttpHandler);
                    options.Transport = new HttpClientPipelineTransport(_proxyHttpClient);
                }

                _chatClient = new ChatClient(_config.Model, new ApiKeyCredential(_config.ApiKey), options);
            }

            return _chatClient;
        }
    }

    private static string BuildOpenAIEndpoint(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return "https://api.openai.com/v1";

        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException(
                "ApiUrl must be a valid absolute URI and include the scheme, for example 'https://api.openai.com/v1'.",
                nameof(apiUrl));

        var segments = uri.AbsolutePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var chatIndex = Array.FindIndex(segments, x => x.Equals("chat", StringComparison.OrdinalIgnoreCase));
        if (chatIndex >= 0)
        {
            var prefix = string.Join("/", segments.Take(chatIndex));
            var endpoint = $"{uri.Scheme}://{uri.Authority}/{prefix}".TrimEnd('/');
            if (endpoint.Equals($"{uri.Scheme}://{uri.Authority}", StringComparison.OrdinalIgnoreCase))
                return endpoint + "/v1";
            return endpoint;
        }

        var baseEndpoint = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}".TrimEnd('/');
        if (uri.AbsolutePath == "/" || string.IsNullOrWhiteSpace(uri.AbsolutePath))
            return $"{uri.Scheme}://{uri.Authority}/v1";
        return baseEndpoint;
    }

    private void ResetClients()
    {
        lock (_clientLock)
        {
            _chatClient = null;
            _embeddingClient = null;
            _proxyHttpClient?.Dispose();
            _proxyHttpHandler?.Dispose();
            _proxyHttpClient = null;
            _proxyHttpHandler = null;
        }
    }

    private static string? TryBackupInvalidConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return null;

            var backupPath = ConfigPath + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(ConfigPath, backupPath, overwrite: false);
            return backupPath;
        }
        catch (IOException ex)
        {
            Console.WriteLine("备份损坏配置文件失败: " + ex.Message);
            return null;
        }
    }

    private static string GetConfigPath()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = AppContext.BaseDirectory;

        var configDirectory = Path.Combine(baseDirectory, "ChatVPet.ChatProcess.ConsoleTest");
        Directory.CreateDirectory(configDirectory);
        return Path.Combine(configDirectory, "OpenAISetting.console.json");
    }

    private void ApplyConfigToProcess(bool rebuildIndexes)
    {
        _process.SystemDescription = _config.SystemPrompt;
        _process.Localization = BuildLocalization(_config.Localization);

        if (!rebuildIndexes)
            return;

        _process.KnowledgeDataBases.Clear();
        _process.AddKnowledgeDataBase(BaseKnowledge);

        _process.Tools.Clear();
        _process.Tools.AddRange(_process.GetBuiltinDiaryTools());
        _process.Tools.Add(new ToolUse("dance", "让桌宠跳舞(测试用)", [], ToolDance, [], _process.Localization));
        _process.Tools.Add(new ToolUse("idle", "让桌宠发呆(测试用)", [], ToolIdle, [], _process.Localization));
        _process.Tools.Add(new ToolUse("move", "让桌宠自由移动(测试用)", [], ToolMove, [], _process.Localization));
        _process.Tools.Add(new ToolUse("modifystate", "调整状态(测试用)", [], ToolModifyState,
        [
            new Arg { Name = "exp", Type = "number", Description = "经验值增量" },
            new Arg { Name = "money", Type = "number", Description = "金钱增量" },
            new Arg { Name = "feeling", Type = "number", Description = "心情增量" },
            new Arg { Name = "likability", Type = "number", Description = "好感度增量" }
        ], _process.Localization));
    }
}
