using Newtonsoft.Json;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// ChatVPet 聊天处理流程
    /// </summary>
    public class VPetChatProcess
    {
        /// <summary>
        /// AIAPI 调用结果
        /// </summary>
        public class AIAPIAskResult
        {
            public string Reply { get; set; } = "";
            public List<ToolCall> ToolCalls { get; set; } = [];
        }

        /// <summary>
        /// AIAPI 调用方法
        /// </summary>
        /// <param name="system">系统消息</param>
        /// <param name="historys">历史消息</param>
        /// <param name="input">当前消息</param>
        /// <param name="tools">当前可用工具</param>
        /// <returns>返回结果</returns>
        public delegate AIAPIAskResult AIAPIAsk(string system, List<string[]> historys, string input, List<ToolUse> tools);

        /// <summary>
        /// AIAPI AI生成 调用方法
        /// </summary>
        [JsonIgnore] public AIAPIAsk? AIAPIAskFunction;
       
        public delegate float[] AIAPIEmbedding(string input);
        /// <summary>
        /// 生成 input 的词向量的方法
        /// </summary>
        [JsonIgnore] public AIAPIEmbedding? AIAPIEmbeddingFunction;

        /// <summary>
        /// 重要性计算方法 判断该段消息是否重要 (eg:可以通过机器学习为每个消息打分)
        /// </summary>
        /// <param name="message">消息[0]Ask [1]Reply</param>
        /// <returns>分数, 范围:0-1</returns>
        public delegate float CalculateImportance(string[] message);

        /// <summary>
        /// 重要性计算方法 判断该段消息是否重要
        /// </summary>
        [JsonIgnore] public CalculateImportance CalImportanceFunction = (x) => 0.5f;

        /// <summary>
        /// 知识数据库
        /// </summary>
        public List<KnowledgeDataBase> KnowledgeDataBases = new List<KnowledgeDataBase>();

        /// <summary>
        /// 聊天历史
        /// </summary>
        public List<Dialogue> Dialogues = new List<Dialogue>();

        /// <summary>
        /// 工具库
        /// </summary>
        [JsonIgnore]
        public List<ToolUse> Tools = new List<ToolUse>();

        /// <summary>
        /// 本地化方法
        /// </summary>
        [JsonIgnore] public ILocalization Localization;

        /// <summary>
        /// 系统描述
        /// </summary>
        public string SystemDescription { get; set; } = "";

        /// <summary>
        /// 消息最大历史记录数
        /// </summary>
        public int MaxHistoryCount { get; set; } = 10;

        /// <summary>
        /// 消息最大知识库数
        /// </summary>
        public int MaxKnowledgeCount { get; set; } = 10;

        /// <summary>
        /// 消息最大工具数
        /// </summary>
        public int MaxToolCount { get; set; } = 10;

        /// <summary>
        /// 工具调用最大轮次
        /// </summary>
        public int MaxToolRecallCount { get; set; } = 8;

        /// <summary>
        /// 词向量引擎
        /// </summary>
        public W2VEngine? W2VEngine { get; set; }

        /// <summary>
        /// ChatVPet 聊天处理流程
        /// </summary>
        public VPetChatProcess()
        {
            Localization = new ILocalization.LChineseSimple();
        }

        /// <summary>
        /// 添加知识库内容 (纯文本)
        /// </summary>
        /// <param name="knowledgedb">知识库(文本)</param>
        public void AddKnowledgeDataBase(params string[] knowledgedb)
        {
            foreach (var knowledge in knowledgedb)
            {
                if (KnowledgeDataBases.Find(x => x.KnowledgeData == knowledge) == null)
                    KnowledgeDataBases.Add(new KnowledgeDataBase(knowledge, [], Localization));
            }
        }

        /// <summary>
        /// 消息输入聊天内容并获得回复 [注:默认卡线程等待回复,如在UI线程,记得TaskNew]
        /// </summary>
        /// <param name="message">用户输入的聊天内容</param>
        /// <param name="ReturnResponse">返回的回复</param>
        /// <param name="control">运行控制程序</param>
        public void Ask(string message, Action<ProcessResponse> ReturnResponse, ProcessControl control)
        {
            if (AIAPIAskFunction == null)
                throw new Exception("AIAPIAskFunction is null");
            if (W2VEngine == null)
                throw new Exception("W2VEngine is null");
            if (control.ForceToStop)
                return;

            int position = 0;

            var queryVector = W2VEngine.GetQueryVector(message);
            W2VEngine.GetQueryVector(KnowledgeDataBases);
            W2VEngine.GetQueryVector(Tools);
            W2VEngine.GetQueryVector(Dialogues);

            var selectedKnowledge = SelectKnowledge(message, queryVector);
            var selectedTools = SelectTools(message, queryVector);
           var  currentSelectedTools = selectedTools;
            var selectedHistory = SelectHistory(message, queryVector);
            var sysmessage = Localization.ToSystemMessage(SystemDescription, selectedTools, selectedKnowledge);

            var history = new List<string[]>(selectedHistory);
            var currentMessage = message;
            var isToolMessage = false;

            var toolRecallRound = 0;
            while (true)
            {
                if (control.ForceToStop)
                    return;

                var modelResult = AIAPIAskFunction(sysmessage, history, currentMessage, selectedTools);
                if (modelResult == null)
                {
                    ReturnResponse.Invoke(new ProcessResponse()
                    {
                        Reply = "AIAPIAskFunction return null",
                        IsEnd = true,
                        IsError = true,
                        ListPosition = position++
                    });
                    return;
                }
                var reply = modelResult.Reply ?? "";
                var toolCalls = modelResult.ToolCalls ?? [];

                ReturnResponse.Invoke(new ProcessResponse()
                {
                    Reply = reply,
                    IsEnd = false,
                    IsError = false,
                    ListPosition = position++
                });

                var msgimp = isToolMessage ? 0 : CalImportanceFunction([currentMessage, reply]);
                Dialogues.Add(new Dialogue(currentMessage, reply, JsonConvert.SerializeObject(toolCalls), Localization, msgimp * 4, msgimp / 4));

                var toolReturn = ExecuteTools(toolCalls);
                if (string.IsNullOrWhiteSpace(toolReturn))
                {
                    if (isToolMessage)
                    {
                        var last = Dialogues.Last();
                        last.ImportanceWeight_Muti = CalImportanceFunction([last.Question, last.Answer]);
                    }

                    ReturnResponse.Invoke(new ProcessResponse()
                    {
                        Reply = "",
                        IsEnd = true,
                        IsError = false,
                        ListPosition = position++
                    });
                    return;
                }

                if (control.ForceToStop || control.StopBeforeNext)
                {
                    ReturnResponse.Invoke(new ProcessResponse()
                    {
                        Reply = "",
                        IsEnd = true,
                        IsError = false,
                        ListPosition = position++
                    });
                    return;
                }

                if (toolRecallRound >= MaxToolRecallCount)
                {
                    ReturnResponse.Invoke(new ProcessResponse()
                    {
                        Reply = "Tool recall round exceeded",
                        IsEnd = true,
                        IsError = true,
                        ListPosition = position++
                    });
                    return;
                }

                history.Add([currentMessage, BuildStructuredReply(reply, toolCalls)]);
                currentMessage = toolReturn;
                isToolMessage = true;
                toolRecallRound++;
            }
        }

        private List<string> SelectKnowledge(string message, float[] queryVector)
        {
            if (MaxKnowledgeCount <= 0)
                return [];

            return KnowledgeDataBases
                .Where(x => x.Vector != null)
                .Select(x => (x, score: x.InCheck(message, W2VEngine.ComputeCosineSimilarity(x.Vector!, queryVector))))
                .OrderBy(x => x.score)
                .Take(MaxKnowledgeCount)
                .Where(x => x.score < IInCheck.IgnoreValue)
                .Select(x => x.x.KnowledgeData)
                .ToList();
        }

        private List<ToolUse> SelectTools(string message, float[] queryVector)
        {
            if (MaxToolCount <= 0)
                return [];

            return Tools
                .Where(x => x.Vector != null)
                .Select(x => (x, score: x.InCheck(message, W2VEngine.ComputeCosineSimilarity(x.Vector!, queryVector))))
                .OrderBy(x => x.score)
                .Take(MaxToolCount)
                .Where(x => x.score < IInCheck.IgnoreValue)
                .Select(x => x.x)
                .ToList();
        }

        private List<string[]> SelectHistory(string message, float[] queryVector)
        {
            if (MaxHistoryCount <= 0 || Dialogues.Count == 0)
                return [];

            var candidates = Dialogues.Select((dialogue, index) => (dialogue, index)).ToList();
            var selected = new List<(Dialogue dialogue, int index)>();

            var nearCount = Math.Max(1, MaxHistoryCount / 3);
            while (nearCount-- > 0 && candidates.Count > 0)
            {
                selected.Add(candidates[^1]);
                candidates.RemoveAt(candidates.Count - 1);
            }

            if (candidates.Count > 0 && selected.Count < MaxHistoryCount)
            {
                var bySimilarity = candidates
                    .Where(x => x.dialogue.Vector != null)
                    .Select(x => (x.dialogue, x.index, score: x.dialogue.InCheck(message, W2VEngine.ComputeCosineSimilarity(x.dialogue.Vector!, queryVector))))
                    .OrderBy(x => x.score)
                    .ToList();

                foreach (var item in bySimilarity)
                {
                    if (selected.Count >= MaxHistoryCount || item.score >= IInCheck.IgnoreValue)
                        break;
                    selected.Add((item.dialogue, item.index));
                }
            }

            return selected
                .OrderBy(x => x.index)
                .Select(x => x.dialogue.ToMessages(Localization))
                .ToList();
        }

        /// <summary>
        /// 工具调用执行方法, 执行工具调用列表中的工具, 并返回工具的输出结果 (如果有)
        /// </summary>
        /// <param name="toolCalls"></param>
        /// <returns></returns>
        private string ExecuteTools(List<ToolCall> toolCalls)
        {
            var results = new List<string>();
            foreach (var tc in toolCalls)
            {
                var tool = Tools.Find(x => x.Code == tc.Code);
                if (tool == null)
                    continue;

                var ret = tool.RunToolFunction(tc.Args);
                if (!string.IsNullOrWhiteSpace(ret))
                    results.Add(string.Format(Localization.ToolReturn, JsonConvert.SerializeObject(tc), ret));
            }

            return string.Join("", results);
        }

        private string BuildStructuredReply(string reply, List<ToolCall> toolCalls)
        {
            return $"{Localization.Response}\n{reply}\n{Localization.ToolCall}\n{JsonConvert.SerializeObject(toolCalls)}";
        }
    }
}
