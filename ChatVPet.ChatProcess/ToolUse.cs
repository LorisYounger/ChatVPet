using Newtonsoft.Json;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 可用工具 (AIAPI ToolUse)
    /// </summary>
    /// virtual 意味着你可以继承重写个动态工具库
    /// 不想重写也可以使用 ToolFunction 来实现
    public class ToolUse : IInCheck, Iw2vSource
    {
        /// <summary>
        /// 工具类型
        /// </summary>
        [JsonProperty("type")]
        public virtual string Type { get; set; } = "function";

        /// <summary>
        /// 是否使用严格模式
        /// </summary>
        [JsonProperty("strict")]
        public virtual bool Strict { get; set; } = true;

        /// <summary>
        /// 初始化 ToolUse 类的新实例
        /// </summary>
        public ToolUse()
        {
            KeyWords = [];
            Args = new List<Arg>();
        }
        /// <summary>
        /// 初始化 ToolUse 类的新实例
        /// </summary>
        /// <param name="code">工具代码</param>
        /// <param name="descriptive">工具描述</param>
        /// <param name="keyword">关键字数组</param>
        /// <param name="toolFunction">工具功能委托</param>
        /// <param name="args">参数列表</param>
        /// <param name="localization">本地化接口</param>
        /// <param name="important_muit">重要性乘法权重</param>
        /// <param name="important_plus">重要性加法权重</param>
        public ToolUse(string code, string descriptive, string[] keyword, Func<Dictionary<string, string>, string?> toolFunction, List<Arg> args, ILocalization localization, double important_muit = 2, double important_plus = 0)
        {
            Code = code;
            Descriptive = descriptive;
            ImportanceWeight_Muti = (float)important_muit;
            ImportanceWeight_Plus = (float)important_plus;
            ToolFunction = toolFunction;
            Args = args;
            KeyWords = [.. keyword.Select(x => string.Join(" ", localization.WordSplit(x))), string.Join(" ", localization.WordSplit(descriptive))];
        }


        /// <summary>
        /// 执行工具代码, 如果有返回值, 则返回值为工具的输出
        /// 注: 若无需AI进行二次处理,请退回null !!
        /// </summary>
        [JsonIgnore] public Func<Dictionary<string, string>, string?>? ToolFunction;

        /// <summary>
        /// 执行工具代码
        /// </summary>
        /// <param name="args">AI给的参数</param>
        /// <returns> 如果有返回值, 则返回值为工具的输出
        /// 注: 若无需AI进行二次处理,请退回null !!</returns>
        public virtual string? RunToolFunction(Dictionary<string, string> args) => ToolFunction?.Invoke(args);
        [JsonIgnore]
        public float ImportanceWeight_Muti { get; set; } = 2;
        [JsonIgnore]
        public float ImportanceWeight_Plus { get; set; } = 0;

        /// <summary>
        /// 执行的代码
        /// </summary>
        [JsonProperty("name")]
        public virtual string Code { get; set; } = "";
        /// <summary>
        /// 参数
        /// </summary>
        public class Arg
        {
            /// <summary>
            /// 参数名称
            /// </summary>
            public string Name { get; set; } = "";
            /// <summary>
            /// 参数类型 (JSON Schema type)
            /// </summary>
            public string Type { get; set; } = "string";
            /// <summary>
            /// 参数描述, 可以在开头写上期望类型; 例如: [int] 期望一个整数
            /// </summary>
            public string Description { get; set; } = "";
            /// <summary>
            /// 是否必填
            /// </summary>
            public bool IsRequired { get; set; } = false;
        }
        /// <summary>
        /// 可选参数
        /// </summary>
        [JsonIgnore]
        public virtual List<Arg> Args { get; set; }
        /// <summary>
        /// 工具描述
        /// </summary>
        [JsonProperty("description")]
        public virtual string Descriptive { get; set; } = "";

        /// <summary>
        /// 函数参数定义
        /// </summary>
        [JsonProperty("parameters")]
        public virtual ParameterSchema Parameters
        {
            get
            {
                var normalizedArgs = (Args ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .GroupBy(x => x.Name)
                    .Select(x => x.Last())
                    .ToList();

                var properties = normalizedArgs.ToDictionary(
                    x => x.Name,
                    x => new ParameterProperty
                    {
                        Type = string.IsNullOrWhiteSpace(x.Type) ? "string" : x.Type,
                        Description = x.Description
                    });
                return new ParameterSchema
                {
                    Properties = properties,
                    Required = [.. normalizedArgs.Where(x => x.IsRequired).Select(x => x.Name)]
                };
            }
        }
        [JsonIgnore]
        /// <summary>
        /// 关键字组
        /// </summary>
        public IEnumerable<string> KeyWords { get; set; }

        /// <summary>
        /// 嵌入向量的原始文本列表（工具名、描述、参数信息），每项独立生成一个向量
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<string> EmbeddingTexts =>
        [
            Code,
            Descriptive,
            .. (Args ?? []).Select(x => $"{x.Name} {x.Description}")
        ];


        /// <summary>
        /// 向量
        /// </summary>
        [JsonIgnore]
        public float[]? Vector { get; set; }
        /// <summary>
        /// 每个 EmbeddingTexts 对应的嵌入向量缓存
        /// </summary>
        [JsonIgnore]
        public float[][]? EmbeddingVectors { get; set; }

        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

        public class ParameterSchema
        {
            [JsonProperty("type")]
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string Type { get; set; } = "object";

            [JsonProperty("properties")]
            [System.Text.Json.Serialization.JsonPropertyName("properties")]
            public Dictionary<string, ParameterProperty> Properties { get; set; } = [];

            [JsonProperty("required")]
            [System.Text.Json.Serialization.JsonPropertyName("required")]
            public List<string> Required { get; set; } = [];

            [JsonProperty("additionalProperties")]
            [System.Text.Json.Serialization.JsonPropertyName("additionalProperties")]
            public bool AdditionalProperties { get; set; } = false;
        }

        public class ParameterProperty
        {
            [JsonProperty("type")]
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string Type { get; set; } = "string";

            [JsonProperty("description")]
            [System.Text.Json.Serialization.JsonPropertyName("description")]
            public string Description { get; set; } = "";
        }
    }
}
