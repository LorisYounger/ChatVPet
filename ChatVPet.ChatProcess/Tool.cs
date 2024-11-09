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
        }
        public Tool(string code, string descriptive, Func<Dictionary<string,string>, string?> toolFunction, ILocalization localization, bool isImportant = false)
        {
            Code = code;
            Descriptive = descriptive;
            IsImportant = isImportant;
            ToolFunction = toolFunction;
            var Words = localization.WordSplit(descriptive);
            KeyWords = IKeyWords.GetKeyWords(Words);
            WordsCount = Words.Length;
        }


        /// <summary>
        /// 执行工具代码, 如果有返回值, 则返回值为工具的输出
        /// 注: 若无需AI进行二次处理,请退回null !!
        /// </summary>
        public Func<Dictionary<string, string>, string?>? ToolFunction;

        /// <summary>
        /// 执行工具代码
        /// </summary>
        /// <param name="args">AI给的参数</param>
        /// <returns> 如果有返回值, 则返回值为工具的输出
        /// 注: 若无需AI进行二次处理,请退回null !!</returns>
        public virtual string? RunToolFunction(Dictionary<string, string> args) => ToolFunction?.Invoke(args);

        public virtual int InCheck(string message, string[] keywords_list, Dictionary<string, int> keywords_dict)
        {
            throw new NotImplementedException();
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
        public virtual List<Arg> Args { get; set; } = new List<Arg>();
        /// <summary>
        /// 工具描述
        /// </summary>
        public virtual string Descriptive { get; set; } = "";
        /// <summary>
        /// 是否是重要的工具,必须加入到工具库中
        /// </summary>
        public bool IsImportant { get; set; } = false;
        public Dictionary<string, int> KeyWords { get; set; }
        public int WordsCount { get; set; }
    }
}
