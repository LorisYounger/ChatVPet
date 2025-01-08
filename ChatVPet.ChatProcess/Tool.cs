using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 可用工具
    /// </summary>
    /// virtual 意味着你可以继承重写个动态工具库
    /// 不想重写也可以使用 ToolFunction 来实现
    public class Tool : IInCheck, Iw2vSource
    {
        public Tool()
        {
            KeyWords = "";
            Args = new List<Arg>();
        }
        public Tool(string code, string descriptive, Func<Dictionary<string, string>, string?> toolFunction, List<Arg> args, ILocalization localization, double important_muit = 2, double important_plus = 0)
        {
            Code = code;
            Descriptive = descriptive;
            ImportanceWeight_Muti = (float)important_muit;
            ImportanceWeight_Plus = (float)important_plus;
            ToolFunction = toolFunction;
            Args = args;
            KeyWords = string.Join(" ", localization.WordSplit(descriptive));
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
            /// 参数描述, 可以在开头写上期望类型; 例如: [int] 期望一个整数
            /// </summary>
            public string Description { get; set; } = "";
        }
        /// <summary>
        /// 可选参数
        /// </summary>
        public virtual List<Arg> Args { get; set; }
        /// <summary>
        /// 工具描述
        /// </summary>
        public virtual string Descriptive { get; set; } = "";
        /// <summary>
        /// 关键字组
        /// </summary>
        [JsonIgnore]
        public string KeyWords { get; set; }
        /// <summary>
        /// 向量
        /// </summary>
        [JsonIgnore]
        public float[]? Vector { get; set; }
        public float InCheck(string message, float similarity) => IInCheck.InCheck(message, similarity, this);

    }
}
