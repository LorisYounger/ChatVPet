using ChatVPet.ChatProcess;
using LinePutScript;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System.Collections.Generic;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet.dynKnowDB
{
    /// <summary>
    /// 桌宠动态知识库
    /// </summary>
    public class DynKDBHostInfo : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBHostInfo(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            KeyWords = localization.WordSplit("主人 当前用户 名字 生日".Translate(MW.GameSavesData.GameSave.Name));
        }

        /// <summary>
        /// 用于 Embedding 向量生成的语义描述文本
        /// </summary>
        [JsonIgnore]
        public override IEnumerable<string> EmbeddingTexts =>
            ["我是谁".Translate(),
            "今天是我的生日".Translate(),
            "生日快乐".Translate(),
            "我叫什么".Translate(),
            "我是你的什么".Translate()];

        public override string KnowledgeData
        {
            get
            {
                return "主人(当前用户}: {0}\n生日: {1}".Translate(MW.GameSavesData.GameSave.HostName, MW.GameSavesData.GetDateTime("HostBDay", MW.GameSavesData[(gdat)"birthday"]));
            }
        }
    }
}
