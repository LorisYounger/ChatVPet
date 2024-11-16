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
    public class Tool : IInCheck, IKeyWords
    {
        public Tool()
        {
            KeyWords = new Dictionary<string, int>();
            Args = new List<Arg>();
        }
        public Tool(string code, string descriptive, Func<Dictionary<string, string>, string?> toolFunction, List<Arg> args, ILocalization localization, bool isImportant = false)
        {
            Code = code;
            Descriptive = descriptive;
            IsImportant = isImportant;
            ToolFunction = toolFunction;
            Args = args;
            var Words = localization.WordSplit(descriptive);
            KeyWords = IKeyWords.GetKeyWords(Words);
            WordsCount = Words.Length;
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

        public virtual int InCheck(string message, string[] keywords_list, Dictionary<string, int> keywords_dict)
        {
            if (IsImportant)
            {
                return 0;
            }
            return Math.Max(10, IInCheck.IgnoreValue - ((IKeyWords)this).Score(keywords_dict, keywords_list.Length));
        }

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
        /// 是否是重要的工具,必须加入到工具库中
        /// </summary>
        [JsonIgnore] public bool IsImportant { get; set; } = false;
        [JsonIgnore] public Dictionary<string, int> KeyWords { get; set; }
        [JsonIgnore] public int WordsCount { get; set; }
    }
}
