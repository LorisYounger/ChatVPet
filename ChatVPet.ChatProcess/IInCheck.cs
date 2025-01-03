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
        public static float IgnoreValue = 0.75f;

        /// <summary>
        /// 重要性,会乘法加权到排序分数上. 默认2, 越大越重要性越高
        /// </summary>
        float ImportanceWeight_Muti { get; }
        /// <summary>
        /// 重要性,会加法加权到排序分数上. 默认0, 越大越重要性越高
        /// </summary>
        float ImportanceWeight_Plus { get; }
        /// <summary>
        /// 检查是否处理, 返回排序分数,范围(0-1), 0为最高优先级, 大于等于IgnoreValue为不处理
        /// </summary>
        /// <param name="message">当前用户消息</param>
        /// <param name="similarity">相似度分数</param>
        float InCheck(string message, float similarity);

        public static float InCheck(string message, float similarity, IInCheck inCheck)
        {
            return Math.Max(0, 1 - similarity * inCheck.ImportanceWeight_Muti + inCheck.ImportanceWeight_Plus);
        }
    }
}
