using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 本地化方法
    /// </summary>
    public interface ILocalization
    {
        /// <summary>
        /// 工具集如下
        /// </summary>
        string ToolSetFollows { get; }
        /// <summary>
        /// 数据库如下
        /// </summary>
        string DataBaseFollows { get; }
        /// <summary>
        /// 回复格式如下
        /// </summary>
        string ResponseFormatFollows { get; }
        /// <summary>
        /// 回复
        /// </summary>
        string Response { get; }
        /// <summary>
        /// 回复内容
        /// </summary>
        string ResponseContent { get; }
        /// <summary>
        /// 工具调用
        /// </summary>
        string ToolCall { get; }
        /// <summary>
        /// 工具json
        /// </summary>
        string ToolCallContent { get; }
        /// <summary>
        /// 文本分词
        /// </summary>
        /// <param name="text">文本</param>
        /// <returns>分词后文本</returns>
        string[] WordSplit(string text);
        /// <summary>
        /// 工具`{0}`返回
        /// </summary>
        string ToolReturn { get; }
        /// <summary>
        /// 生成系统消息
        /// </summary>
        /// <param name="system">原系统消息</param>
        /// <param name="tools">工具列表</param>
        /// <param name="database">数据库</param>
        /// <returns>系统消息</returns>
        public string ToSystemMessage(string system, List<Tool> tools, List<string> database)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(system);
            sb.AppendLine();
            sb.AppendLine(ToolSetFollows);
            sb.AppendLine("```json");
            //易读的json格式方便AI理解
            sb.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(tools, Newtonsoft.Json.Formatting.Indented));
            sb.AppendLine("```");
            sb.AppendLine(DataBaseFollows);
            sb.AppendLine("```json");
            sb.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(database));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine(ResponseFormatFollows);
            sb.AppendLine("```");
            sb.AppendLine(Response);
            sb.AppendLine(ResponseContent);
            sb.AppendLine(ToolCall);
            sb.AppendLine(ToolCallContent);
            sb.AppendLine("```");
            return sb.ToString();
        }
        /// <summary>
        /// 简体中文
        /// </summary>
        public class LChineseSimple : ILocalization
        {
            public string ToolSetFollows => "工具集如下:";

            public string DataBaseFollows => "数据库如下:";

            public string ResponseFormatFollows => "回复格式如下:";

            public string Response => "回复:";

            public string ResponseContent => "{回复内容}";

            public string ToolCall => "工具调用:";

            public string ToolCallContent => "[{\"Code\":\"toolcode\",\"Args\":[{\"argsname\":\"argscontent\"}]}]";

            public string ToolReturn => "工具 `{0}` 返回:\n```\n{1}\n```\n";

            public JiebaNet.Segmenter.JiebaSegmenter Segmenter = new JiebaNet.Segmenter.JiebaSegmenter();
            public string[] WordSplit(string text) => Segmenter.Cut(text).ToArray();
        }
        /// <summary>
        /// 英语
        /// </summary>
        public class LEnglish : ILocalization
        {
            public string ToolSetFollows => "The toolset is as follows:";

            public string DataBaseFollows => "The database is as follows:";

            public string ResponseFormatFollows => "The response format is as follows.";

            public string Response => "Response:";

            public string ResponseContent => "{Content of Response}";

            public string ToolCall => "Tool Call:";

            public string ToolCallContent => "[{\"Code\":\"toolcode\",\"Args\":[{\"argsname\":\"argscontent\"}]}]";

            public string ToolReturn => "Tool `{0}` return:\n```\n{1}\n```\n";

            public string[] WordSplit(string text) => text.ToLower().Split(' ').Select(x => x.Trim([',', '[', ']', '(', ')']))
                .Where(x => x.Length != 0).ToArray();
        }
        /// <summary>
        /// 繁体中文
        /// </summary>
        public class LChineseTraditional : ILocalization
        {
            public string ToolSetFollows => "工具集如下:";

            public string DataBaseFollows => "數據庫如下:";

            public string ResponseFormatFollows => "回覆格式如下:";

            public string Response => "回覆:";

            public string ResponseContent => "{回覆內容}";

            public string ToolCall => "工具調用:";

            public string ToolCallContent => "[{\"Code\":\"toolcode\",\"Args\":[{\"argsname\":\"argscontent\"}]}]";

            public string ToolReturn => "工具 `{0}` 返回:\n```\n{1}\n```\n";

            public JiebaNet.Segmenter.JiebaSegmenter Segmenter = new JiebaNet.Segmenter.JiebaSegmenter();
            public string[] WordSplit(string text) => Segmenter.Cut(text).ToArray();
        }
    }
}
