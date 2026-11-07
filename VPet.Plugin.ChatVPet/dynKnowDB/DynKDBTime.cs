using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet.dynKnowDB
{
    /// <summary>
    /// 桌宠动态知识库
    /// </summary>
    public class DynKDBTime : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBTime(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            KeyWords = localization.WordSplit("当前 时间 今天 现在 日期".Translate());
            ImportanceWeight_Plus = 0.15f;
            ImportanceWeight_Muti = 1.5f;
        }

        /// <summary>
        /// 用于 Embedding 向量生成的语义描述文本
        /// </summary>
        [JsonIgnore]
        public override IEnumerable<string> EmbeddingTexts =>
            [
            "现在几点了".Translate(),
            "今天是什么时候".Translate(),
            "现在是几月几号".Translate(),
            "今天星期几".Translate(),
            "现在什么时间了".Translate(),
            "今天是哪天".Translate(),
            "告诉我现在的日期和时间".Translate(),
            ];

        public override string KnowledgeData
        {
            get => "当前时间是".Translate() + ' ' + DateTime.Now.ToShortDateString() + ' ' + DateTime.Now.ToShortTimeString();
        }

    }
}
