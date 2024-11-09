using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// 检查是否加入知识库/消息/工具的接口
    /// </summary>
    internal interface IInCheck
    {
        /// <summary>
        /// 大于等于该值为不处理
        /// </summary>
        public static int IgnoreValue = 20000;
        /// <summary>
        /// 检查是否处理, 返回重要性, 0为最高优先级,  大于等于IgnoreValue为不处理
        /// </summary>
        /// <param name="message">当前用户消息</param>
        /// <param name="keywords_dict">当前用户消息 关键字字典,词:出现次数</param>
        /// <param name="keywords_list">当前用户消息 关键字列表,所有词</param>
        /// <returns>当前处理重要性0为最高</returns>
        int InCheck(string message, string[] keywords_list, Dictionary<string, int> keywords_dict);
    }
}
