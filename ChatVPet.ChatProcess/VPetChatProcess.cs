using Newtonsoft.Json;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// ChatVPet 聊天处理流程
    /// </summary>
    public partial class VPetChatProcess
    {
        /// <summary>
        /// AIAPI 调用结果
        /// </summary>
        public class AIAPIAskResult
        {
            public string Reply { get; set; } = "";
            public bool Completed { get; set; } = true;
            public long Tokens { get; set; }
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
        public string SystemDescription { get; set; } = ChatProtocol.RenderSystem();

        /// <summary>
        /// 消息最大历史记录数
        /// </summary>
        public int MaxHistoryCount { get; set; } = 10;

        /// <summary>
        /// 消息最大知识库数
        /// </summary>
        public int MaxKnowledgeCount { get; set; } = 4;

        /// <summary>
        /// 消息最大工具数
        /// </summary>
        public int MaxToolCount { get; set; } = 4;

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
        /// ChatVPet 聊天处理流程
        /// </summary>
        public VPetChatProcess()
        {
            Localization = new ILocalization.LChineseSimple();
            Retrieval = new ContextRetrieval(this);
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
                    KnowledgeDataBases.Add(new KnowledgeDataBase(knowledge, [], [], Localization));
            }
        }

        /// <summary>
        /// 向量相似度查询结果
        /// </summary>
        public class QuerySimilarResult
        {
            /// <summary>知识库命中条目及其评分（分数越低越相关）</summary>
            public List<(KnowledgeDataBase Item, float Score)> Knowledges { get; set; } = [];
            /// <summary>工具命中条目及其评分（分数越低越相关）</summary>
            public List<(ToolUse Item, float Score)> Tools { get; set; } = [];
            /// <summary>对话历史命中条目及其评分（分数越低越相关）</summary>
            public List<(Dialogue Item, float Score)> Dialogues { get; set; } = [];
        }

        /// <summary>
        /// 根据向量相似度检索与 <paramref name="query"/> 相关的知识库、工具和对话历史，
        /// 结果按评分升序排列（分数越低越相关），并过滤掉超出 <see cref="IInCheck.IgnoreValue"/> 阈值的条目。
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <returns>包含各类别已评分并排序结果的 <see cref="QuerySimilarResult"/></returns>
        /// <exception cref="InvalidOperationException"><see cref="W2VEngine"/> 未初始化时抛出</exception>
        public QuerySimilarResult QuerySimilar(string query)
        {
            lock (turnGate)
            {
                if (W2VEngine == null)
                    throw new InvalidOperationException("W2VEngine is null");

                var vector = W2VEngine.GetQueryVector(query);
                W2VEngine.GetQueryVector(KnowledgeDataBases);
                W2VEngine.GetQueryVector(Tools);
                W2VEngine.GetQueryVector(Dialogues);

                var knowledges = KnowledgeDataBases
                    .Select(x => (Item: x, Score: x.InCheck(query, W2VEngine.ComputeSimilarity(query, x, vector))))
                    .Where(x => x.Score < IInCheck.IgnoreValue)
                    .OrderBy(x => x.Score)
                    .ToList();

                var tools = Tools
                    .Select(x => (Item: x, Score: x.InCheck(query, W2VEngine.ComputeSimilarity(query, x, vector))))
                    .Where(x => x.Score < IInCheck.IgnoreValue)
                    .OrderBy(x => x.Score)
                    .ToList();

                var dialogues = Dialogues
                    .Select(x => (Item: x, Score: x.InCheck(query, W2VEngine.ComputeSimilarity(query, x, vector))))
                    .Where(x => x.Score < IInCheck.IgnoreValue)
                    .OrderBy(x => x.Score)
                    .ToList();

                return new QuerySimilarResult
                {
                    Knowledges = knowledges,
                    Tools = tools,
                    Dialogues = dialogues
                };
            }
        }

    }
}
