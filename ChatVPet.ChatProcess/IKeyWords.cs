using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatVPet.ChatProcess
{
    public interface IKeyWords
    {
        /// <summary>
        /// 关键字组
        /// </summary>
        public Dictionary<string, int> KeyWords { get; set; }
        /// <summary>
        /// 文本字数
        /// </summary>
        public int WordsCount { get; set; }
        /// <summary>
        /// 获得相似性分数, 分数越高越相似
        /// </summary>
        /// <param name="otherkeyWords">关键字重要性字典</param>
        /// <returns>相似性分数</returns>
        public int Score(Dictionary<string, int> otherkeyWords, int otherWordsCount)
        {
            if (WordsCount == 0 || otherWordsCount == 0)
            {
                return -1;
            }
            int score = 0;
            foreach (var key in otherkeyWords)
            {
                if (KeyWords.ContainsKey(key.Key))
                {
                    score += KeyWords[key.Key] + key.Value;
                }
            }
            return score * 10000 / (WordsCount + otherWordsCount);
        }
        /// <summary>
        /// 获得关键字重要性字典
        /// </summary>
        /// <param name="words">文本列表</param>
        public static Dictionary<string, int> GetKeyWords(string[] words)
            => words.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
    }
}
