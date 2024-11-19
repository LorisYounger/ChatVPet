using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 聊天处理返回
    /// </summary>
    public class ProcessResponse
    {
        /// <summary>
        /// 显示给用户的消息, 为空则不显示
        /// </summary>
        public string Reply { get; set; } = "";
        /// <summary>
        /// 这是处理的第几轮
        /// </summary>
        public int ListPosition { get; set; }
        /// <summary>
        /// 是否是最终反馈, 允许用户输入新内容
        /// </summary>
        public bool IsEnd { get; set; } = false;
        /// <summary>
        /// 是否是错误, 如果为是, Anser是错误消息
        /// </summary>
        public bool IsError { get; set; } = false;

    }
    /// <summary>
    /// 处理控制
    /// </summary>
    public class ProcessControl
    {
        /// <summary>
        /// 是否强制停止处理循环
        /// </summary>
        public bool ForceToStop { get; set; } = false;
        /// <summary>
        /// True是否输出最后一轮 False不输出直接掐断
        /// </summary>
        public bool StopBeforeNext { get; set; } = false;
    }

    public class ToolCall
    {
        /// <summary>
        /// 推荐使用的json设置
        /// </summary>
        public static JsonSerializerSettings jsonsetting = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy() { ProcessDictionaryKeys = true, OverrideSpecifiedNames = false }
            },
            // 允许忽略大小写
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // 忽略报错
            Error = (sender, args) =>
            {
                Console.WriteLine($"Error: {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true; // 处理错误，避免抛出异常
            }
        };
        /// <summary>
        /// 调用工具方法
        /// </summary>
        public string Code { get; set; } = "";
        /// <summary>
        /// 调用参数
        /// </summary>
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
    }
}
