using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Panuon.WPF.UI;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;
using static VPet_Simulator.Core.Main;
using static VPet_Simulator.Core.WorkTimer;

namespace VPet.Plugin.ChatVPet
{
    public partial class CVPPlugin
    {

        /// <summary>
        /// 让桌宠自己买东西吃
        /// </summary>
        public string? ToolTakeItem(Food item)
        {
            //看是什么模式
            if (MW.Set.EnableFunction)
            {//$10以内的食物允许赊账
                if (item.Price >= 10 && item.Price >= MW.Core.Save.Money)
                {//买不起                   
                    return "您没有足够金钱来购买 {0}\n您需要 {1:f2} 金钱来购买\n您当前 {2:f2} 拥有金钱"
                        .Translate(item.TranslateName, item.Price, MW.Core.Save.Money);
                }
                //看看是否超模
                if (MW.HashCheck && item.IsOverLoad())
                {
                    return null;
                }
                MW.TakeItem(item);
            }
            MW.DisplayFoodAnimation(item.GetGraph(), item.ImageSource);
            return null;
        }
        /// <summary>
        /// 让桌宠自己工作
        /// </summary>
        public string? ToolDoWork(GraphHelper.Work work)
        {
            MW.Dispatcher.Invoke(() => MW.Main.ToolBar.StartWork(work.Double(MW.Set["workmenu"].GetInt("double_" + work.Name, 1))));
            return null;
        }

        public string? ToolStopWork(Dictionary<string, string> args)
        {
            MW.Main.WorkTimer.Stop(reason: FinishWorkInfo.StopReason.MenualStop);
            return null;
        }
        public string? ToolDance(Dictionary<string, string> args)
        {
            var ig = MW.Core.Graph.FindGraphs("music", AnimatType.A_Start, MW.Core.Save.Mode);
            if (ig != null && ig.Count != 0)
            {
                MW.Main.CountNomal = 0;
                MW.Main.Display(ig[Function.Rnd.Next(ig.Count)], () =>
                MW.Main.DisplayBLoopingToNomal("music", Function.Rnd.Next(5, 20)));
            }
            return null;
        }
        public string? ToolTouchHead(Dictionary<string, string> args)
        {
            MW.Main.DisplayTouchHead();
            return null;
        }
        public string? ToolTouchBody(Dictionary<string, string> args)
        {
            MW.Main.DisplayTouchBody();
            return null;
        }
        public string? ToolIdel(Dictionary<string, string> args)
        {
            if (Function.Rnd.Next(2) == 0)
                MW.Main.DisplayIdel();
            else
                MW.Main.DisplayIdel_StateONE();
            return null;
        }
        public string? ToolSleep(Dictionary<string, string> args)
        {
            MW.Main.WorkTimer.Stop(reason: FinishWorkInfo.StopReason.MenualStop);
            var m = MW.Main;
            if (m.State == Main.WorkingState.Nomal)
                m.DisplaySleep(true);
            else if (m.State != Main.WorkingState.Sleep)
            {
                m.WorkTimer.Stop(() => m.DisplaySleep(true), WorkTimer.FinishWorkInfo.StopReason.MenualStop);
            }
            return null;
        }
        public string? ToolWakeup(Dictionary<string, string> args)
        {
            var m = MW.Main;
            if (m.State == Main.WorkingState.Sleep)
            {
                if (m.Core.Save.Mode == IGameSave.ModeType.Ill)
                    return null;
                m.State = WorkingState.Nomal;
                m.Display(GraphType.Sleep, AnimatType.C_End, m.DisplayNomal);
            }
            return null;
        }
        public string? ToolMove(Dictionary<string, string> args)
        {
            MW.Main.DisplayMove();
            return null;
        }
        public int temptoken = 0;
        /// <summary>
        /// 调用 OpenAI 的方法
        /// </summary>
        public VPetChatProcess.AIAPIAskResult OpenAIAsk(string system, List<string[]> historys, string message, List<ToolUse> tools)
        {
            if (OpenAIConfig == null)
                throw new Exception("请先前往设置中设置 GPT API".Translate());
            var config = OpenAIConfig;

            List<ChatMessage> messages = [new SystemChatMessage(system)];
            foreach (var h in historys)
            {
                messages.Add(new UserChatMessage(h[0]));
                messages.Add(new AssistantChatMessage(h[1]));
            }
            messages.Add(new UserChatMessage(message));

            var options = new ChatCompletionOptions();
            options.Temperature = (float)config.Temperature;
            options.MaxOutputTokenCount = config.MaxTokens;
            options.PresencePenalty = (float)config.PresencePenalty;
            options.FrequencyPenalty = (float)config.FrequencyPenalty;

            foreach (var tool in tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                functionName: tool.Code,
                functionDescription: tool.Descriptive,
                functionParameters: BinaryData.FromObjectAsJson(tool.Parameters)));
            }

            var client = CreateOpenAIChatClient();

            var completion = client.CompleteChat(messages, options).Value;
            var reply = string.Concat(completion.Content.Select(x => x.Text));

            temptoken = completion.Usage?.TotalTokenCount ?? 0;
            TotalTokensUsage += temptoken;
            TokenCount = temptoken;
            if (AllowSubmit)
            {
                upsysmessage = system;
                upquestion = message;
                uphistory = historys;
            }

            return new VPetChatProcess.AIAPIAskResult
            {
                Reply = reply,
                ToolCalls = ParseToolCalls(completion.ToolCalls)
            };
        }

        public float[][] OpenAIEmbedding(IEnumerable<string> text)
        {
            if (OpenAIConfig == null)
                throw new Exception("请先前往设置中设置 Embedding API".Translate());

            var client = GetClient();
            return client.GenerateEmbeddings(text).Value.Select(x => x.ToFloats().ToArray()).ToArray();
        }
        public EmbeddingClient? _embeddingClient;
        private readonly object _clientLock = new object();
        private EmbeddingClient GetClient()
        {
            if (OpenAIConfig == null)
                throw new Exception("请先前往设置中设置 GPT API".Translate());
            lock (_clientLock)
            {
                if (_embeddingClient == null)
                {
                    var options = new OpenAIClientOptions { Endpoint = new Uri(OpenAIConfig.EmbeddingApiUrl) };
                    _embeddingClient = new EmbeddingClient(OpenAIConfig.EmbeddingModel, new ApiKeyCredential(OpenAIConfig.EmbeddingApiKey), options);
                }
                return _embeddingClient;
            }
        }
        public ChatClient? _chatClient;
        private ChatClient CreateOpenAIChatClient()
        {
            if (OpenAIConfig == null)
                throw new Exception("请先前往设置中设置 AI API".Translate());
            var endpoint = BuildOpenAIEndpoint(OpenAIConfig.ApiUrl);
            var cacheKey = $"{endpoint}|{OpenAIConfig.Model}|{GetCacheSafeKey(OpenAIConfig.ApiKey)}|{OpenAIConfig.WebProxy}";
            lock (_clientLock)
            {
                if (_chatClient == null)
                {
                    var options = new OpenAIClientOptions
                    {
                        Endpoint = new Uri(endpoint)
                    };
                    if (!string.IsNullOrWhiteSpace(OpenAIConfig.WebProxy))
                    {
                        var handler = new HttpClientHandler
                        {
                            Proxy = new WebProxy(OpenAIConfig.WebProxy),
                            UseProxy = true
                        };
                        options.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
                    }
                    _chatClient = new ChatClient(OpenAIConfig.Model, new ApiKeyCredential(OpenAIConfig.ApiKey), options);
                }
                return _chatClient;
            }
        }

        private static string BuildOpenAIEndpoint(string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                return "https://api.openai.com/v1";

            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
                return apiUrl.TrimEnd('/');

            var segments = uri.AbsolutePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            // 兼容传入 /v1/chat/completions 的配置，抽取到基础 endpoint(/v1) 给 OpenAI SDK 使用。
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

        private static string GetCacheSafeKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return string.Empty;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// 转换返回的工具调用为 ToolCallResult 列表
        /// </summary>
        private static List<ToolCallResult> ParseToolCalls(IReadOnlyList<ChatToolCall> openAIToolCalls)
        {
            List<ToolCallResult> result = [];
            if (openAIToolCalls == null)
                return result;
            foreach (var call in openAIToolCalls)
            {
                var code = call.FunctionName;
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                Dictionary<string, string> args = new Dictionary<string, string>();
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
        public void RunDIY(string content)
        {
            if (content.Contains(@":\"))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = content;
                    startInfo.UseShellExecute = false;
                    Process.Start(startInfo);
                }
                catch
                {
                    try
                    {
                        try
                        {
                            Process.Start(content);
                        }
                        catch
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = content,
                                UseShellExecute = true
                            };
                            Process.Start(psi);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                    }
                }
            }
            else if (content.Contains("://"))
            {
                try
                {
                    ExtensionFunction.StartURL(content);
                }
                catch (Exception e)
                {
                    MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                }
            }
            else
            {
                try
                {
                    System.Windows.Forms.SendKeys.SendWait(content);
                }
                catch (Exception e)
                {
                    MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                }
            }
        }

        public string? ToolModifyState(Dictionary<string, string> args)
        {
            if (args.TryGetValue("exp", out string? exp))
            {
                if (int.TryParse(exp, out int expi))
                {
                    var max = MW.GameSavesData.GameSave.LevelUpNeed() * 0.1;
                    SideMessage += "\n" + "经验值".Translate() + " " + ExtensionFunction.ValueToPlusPlus(expi, 1 / 4, 6);
                    MW.Core.Save.Exp += Math.Max(-max, Math.Min(expi, max));
                    return null;
                }
            }
            if (args.TryGetValue("money", out string? money))
            {
                if (double.TryParse(money, out double moneyi))
                {
                    SideMessage += "\n" + "金钱".Translate() + " " + ExtensionFunction.ValueToPlusPlus(moneyi, 1 / 100, 6);
                    MW.Core.Save.Money += Math.Max(-1000, Math.Min(moneyi, 1000));
                    return null;
                }
            }
            if (args.TryGetValue("feeling", out string? feeling))
            {
                if (double.TryParse(feeling, out double feelingi))
                {
                    SideMessage += "\n" + "心情".Translate() + " " + ExtensionFunction.ValueToPlusPlus(feelingi, 1 / 3, 6);
                    var max = MW.Core.Save.FeelingMax * 0.5;
                    MW.Core.Save.Feeling += Math.Max(-max, Math.Min(feelingi, max));
                    return null;
                }
            }
            if (args.TryGetValue("likability", out string? likability))
            {
                if (double.TryParse(likability, out double likabilityi))
                {
                    SideMessage += "\n" + "好感度".Translate() + " " + ExtensionFunction.ValueToPlusPlus(likabilityi, 1.5, 6);
                    MW.Core.Save.Likability += Math.Max(-10, Math.Min(likabilityi, 10));
                    return null;
                }
            }
            return null;
        }
    }
}
