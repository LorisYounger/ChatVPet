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
            public List<ToolCallResult> ToolCalls { get; set; } = [];
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

        public delegate float[][] AIAPIEmbeddings(IEnumerable<string> inputs);

        /// <summary>
        /// 生成 input 的词向量的方法
        /// </summary>
        [JsonIgnore] public AIAPIEmbeddings? AIAPIEmbeddingFunction;


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
        public int MaxToolRecallCount { get; set; } = 6;

        /// <summary>
        /// 词向量引擎
        /// </summary>
        public W2VEngine? W2VEngine { get; set; }

        // ── 日记功能 ────────────────────────────────────────────────────

        /// <summary>
        /// 已压缩的历史日记条目列表
        /// </summary>
        public List<DiaryEntry> DiaryEntries = new List<DiaryEntry>();

        /// <summary>
        /// 当 Dialogues 条数超过此值时触发历史压缩（<=0 表示禁用）
        /// </summary>
        public int MaxHistoryBeforeCompress { get; set; } = 24;

        /// <summary>
        /// 压缩时保留最近的对话条数（这些消息不参与摘要）
        /// </summary>
        public int DiaryKeepRecentCount { get; set; } = 10;

        /// <summary>
        /// 每次对话后对未被命中的日记条目施加的权重衰减率（0.05 = 衰减 5%）
        /// </summary>
        public float DiaryDecayRate { get; set; } = 0.03f;

        /// <summary>
        /// 每次对话最多向系统提示注入的日记条目数
        /// </summary>
        public int MaxDiaryInContext { get; set; } = 10;

        /// <summary>
        /// ChatVPet 聊天处理流程
        /// </summary>
        public VPetChatProcess()
        {
            Localization = new ILocalization.LChineseSimple();
        }

        /// <summary>
        /// 反序列化完成后同步 <see cref="DiaryEntry"/> 的静态 ID 计数器，
        /// 确保新建条目的 ID 不会与已加载的条目重复。
        /// </summary>
        [System.Runtime.Serialization.OnDeserialized]
        internal void OnDeserialized(System.Runtime.Serialization.StreamingContext _)
        {
            if (DiaryEntries.Count > 0)
                DiaryEntry.EnsureCounterAbove(DiaryEntries.Max(e => e.Id));
        }

        /// <summary>
        /// 获取内置日记工具列表（允许 LLM 通过 toolcall 调节日记权重）。
        /// 可将返回结果添加到 <see cref="Tools"/> 以启用该能力。
        /// </summary>
        public List<ToolUse> GetBuiltinDiaryTools()
        {
            return
            [
                new ToolUse(
                    code: "diary_adjust_weight",
                    descriptive: Localization.DiaryAdjustWeightToolDesc,
                    keyword: ["diary", "weight", "adjust", "日记", "权重"],
                    toolFunction: args =>
                    {
                        if (!args.TryGetValue("diary_id", out var diaryIdStr) || string.IsNullOrWhiteSpace(diaryIdStr))
                            return "error: missing or empty diary_id";
                        if (!int.TryParse(diaryIdStr.Trim().TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out var diaryId))
                            return "error: invalid diary_id (expected a hex integer, e.g. 1a or 2b)";
                        if (!args.TryGetValue("delta", out var deltaStr) || !float.TryParse(deltaStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var delta))
                            return "error: missing or invalid delta (expected a float)";

                        var entry = DiaryEntries.Find(e => e.Id == diaryId);
                        if (entry == null)
                            return $"error: diary entry '{diaryIdStr.Trim()}' not found";

                        entry.AdjustBaseWeight(delta);
                        return null;
                    },
                    args:
                    [
                        new ToolUse.Arg { Name = "diary_id", Type = "string", Description = Localization.DiaryArgIdDesc, IsRequired = true },
                        new ToolUse.Arg { Name = "delta", Type = "number", Description = Localization.DiaryArgDeltaDesc, IsRequired = true }
                    ],
                    localization: Localization)
            ];
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

            // 历史消息压缩（日记功能）
            TryCompressHistory();

            int position = 0;

            var queryVector = W2VEngine.GetQueryVector(message);
            W2VEngine.GetQueryVector(KnowledgeDataBases);
            W2VEngine.GetQueryVector(Tools);
            W2VEngine.GetQueryVector(Dialogues);
            W2VEngine.GetQueryVector(DiaryEntries);

            var selectedKnowledge = SelectKnowledge(message, queryVector);
            var selectedTools = SelectTools(message, queryVector);
            var selectedHistory = SelectHistory(message, queryVector);
            var selectedDiaries = SelectDiaryEntries(message, queryVector);
            var sysmessage = BuildSystemMessage(SystemDescription, selectedKnowledge, selectedDiaries);

            // 衰减未被命中的日记条目，命中的增加权重
            DecayAndUpdateDiaries(selectedDiaries);

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
                .Where(x => (x.EmbeddingVectors != null && x.EmbeddingVectors.Length > 0) || (x.Vector != null && x.Vector.Length > 0))
                .Select(x => (x, score: x.InCheck(message, W2VEngine!.ComputeSimilarity(message, x, queryVector))))
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
                .Where(x => (x.EmbeddingVectors != null && x.EmbeddingVectors.Length > 0) || (x.Vector != null && x.Vector.Length > 0))
                .Select(x => (x, score: x.InCheck(message, W2VEngine!.ComputeSimilarity(message, x, queryVector))))
                .OrderBy(x => x.score)
                .Take(MaxToolCount)
                .Where(x => x.score < IInCheck.IgnoreValue)
                .Select(x => x.x)
                .ToList();
        }

        private string BuildSystemMessage(string system, List<string> knowledge, List<DiaryEntry> diaries)
        {
            var sb = new System.Text.StringBuilder(system);

            if (knowledge.Count > 0)
                sb.Append($"\n\n{Localization.DataBaseFollows}\n```json\n{JsonConvert.SerializeObject(knowledge)}\n```");

            if (diaries.Count > 0)
            {
                sb.Append($"\n\n{Localization.DiaryHeader}");
                foreach (var d in diaries)
                    sb.Append($"\n{d.ToContextLine()}");
            }

            return sb.ToString();
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
                    .Where(x => (x.dialogue.EmbeddingVectors != null && x.dialogue.EmbeddingVectors.Length > 0) || (x.dialogue.Vector != null && x.dialogue.Vector.Length > 0))
                    .Select(x => (x.dialogue, x.index, score: x.dialogue.InCheck(message, W2VEngine!.ComputeSimilarity(message, x.dialogue, queryVector))))
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
        /// 检查是否需要压缩历史消息，若需要则调用 LLM 生成日记摘要
        /// </summary>
        private void TryCompressHistory()
        {
            if (MaxHistoryBeforeCompress <= 0 || AIAPIAskFunction == null)
                return;
            if (Dialogues.Count <= MaxHistoryBeforeCompress)
                return;

            var keepCount = Math.Max(0, Math.Min(DiaryKeepRecentCount, Dialogues.Count));
            var compressCount = Dialogues.Count - keepCount;
            if (compressCount <= 0)
                return;

            var toCompress = Dialogues.GetRange(0, compressCount);

            // 构建结构化对话文本以供 LLM 摘要，避免混用固定语言的角色标签
            var conversationText = JsonConvert.SerializeObject(
                toCompress.Select(d => new { question = d.Question, answer = d.Answer }),
                Formatting.Indented);

            var userPrompt = string.Format(Localization.DiaryCompressionUserPrompt, conversationText);
            var summaryResult = AIAPIAskFunction(
                Localization.DiaryCompressionSystemPrompt,
                [],
                userPrompt,
                []);

            // 仅当 LLM 返回有效摘要时才压缩；否则保留原始对话历史
            var summary = summaryResult?.Reply;
            if (string.IsNullOrWhiteSpace(summary))
                return;

            // 用 CalImportanceFunction 对摘要文本打分，作为日记的基础重要性权重
            // 摘要只有答案部分，问题留空
            var importance = CalImportanceFunction(["", summary]);
            // 将 0-1 的分数映射到合理的权重范围（1-4）
            var baseWeight = 1f + importance * 3f;

            var entry = new DiaryEntry(summary, Localization, baseWeight, DateTime.Now);
            W2VEngine?.GetQueryVector([entry]);
            DiaryEntries.Add(entry);

            // 移除已压缩的消息
            Dialogues.RemoveRange(0, compressCount);
        }

        /// <summary>
        /// 根据当前消息和查询向量，从 DiaryEntries 中检索最相关的日记条目
        /// </summary>
        private List<DiaryEntry> SelectDiaryEntries(string message, float[] queryVector)
        {
            if (MaxDiaryInContext <= 0 || DiaryEntries.Count == 0)
                return [];

            return DiaryEntries
                .Where(x => (x.EmbeddingVectors != null && x.EmbeddingVectors.Length > 0) || (x.Vector != null && x.Vector.Length > 0))
                .Select(x => (entry: x, score: x.InCheck(message, W2VEngine!.ComputeSimilarity(message, x, queryVector))))
                .OrderBy(x => x.score)
                .Take(MaxDiaryInContext)
                .Where(x => x.score < IInCheck.IgnoreValue)
                .Select(x => x.entry)
                .ToList();
        }

        /// <summary>
        /// 对所有日记条目应用衰减；被选中使用的条目执行命中回调
        /// </summary>
        private void DecayAndUpdateDiaries(IEnumerable<DiaryEntry> usedEntries)
        {
            if (DiaryDecayRate <= 0 || DiaryEntries.Count == 0)
                return;

            var usedSet = new HashSet<DiaryEntry>(usedEntries);
            foreach (var entry in DiaryEntries)
            {
                if (usedSet.Contains(entry))
                    entry.OnHit();
                else
                    entry.ApplyDecay(DiaryDecayRate);
            }
        }

        /// <summary>
        /// 工具调用执行方法, 执行工具调用列表中的工具, 并返回工具的输出结果 (如果有)
        /// </summary>
        /// <param name="toolCalls"></param>
        /// <returns></returns>
        private string ExecuteTools(List<ToolCallResult> toolCalls)
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

        private string BuildStructuredReply(string reply, List<ToolCallResult> toolCalls)
        {
            return $"{Localization.Response}\n{reply}\n{Localization.ToolCall}\n{JsonConvert.SerializeObject(toolCalls)}";
        }
    }
}
