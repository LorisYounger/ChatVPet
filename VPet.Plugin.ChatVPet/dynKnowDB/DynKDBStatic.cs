using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System.Collections.Generic;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet.dynKnowDB
{
    /// <summary>
    /// 桌宠动态知识库
    /// </summary>
    public class DynKDBStatic : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBStatic(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            KeyWords = localization.WordSplit("桌宠名称 {0} 金钱 钱 数据 体力 心情 饱腹度 口渴度 好感度 当前状态 饥饿 饿 口渴 渴".Translate(MW.GameSavesData.GameSave.Name));
            ImportanceWeight_Plus = 0.1f;
            ImportanceWeight_Muti = 1.5f;
        }

        /// <summary>
        /// 用于 Embedding 向量生成的语义描述文本，使用自然语言关键词而非格式化状态数据，
        /// 以便与"你还剩多少钱"、"现在体力多少"等自然语言查询更好地语义对齐。
        /// </summary>
        [JsonIgnore]
        public override IEnumerable<string> EmbeddingTexts =>
            [
            "你现在渴不渴啊".Translate(),
            "你还剩多少钱".Translate(),
            "你心情怎么样".Translate(),
            "今天玩得开心吗".Translate(),
            "饿不饿,想不想吃东西".Translate(),
            "你现在体力还剩多少".Translate(),
            "你的好感度是多少".Translate(),
            "你现在几级了".Translate(),
            "你现在饱腹度怎么样".Translate(),
            "桌宠现在的状态如何".Translate(),
            ];

        public override string KnowledgeData
        {
            get
            {
                var save = MW.GameSavesData.GameSave;
                return "桌宠名称:{0}, 金钱 ${1:f1}, Lv {2} ({3:f1}/{4:f1}), 体力 {5:f1}/{6:f1}({7:p0}), 心情 {8:f1}/{9:f1}({10:p0}), 饱腹度 {11:f1}/{6:f1}({12:p0}), 口渴度 {13:f1}/{6:f1}({14:p0}), 好感度 {15:f1}"
                      .Translate(save.Name, save.Money, save.Level, save.Exp, save.LevelUpNeed(), save.Strength, save.StrengthMax, (save.Strength / save.StrengthMax),
                      save.Feeling, save.FeelingMax, (save.Feeling / save.FeelingMax), save.StrengthFood, save.StrengthFood / save.StrengthMax,
                      save.StrengthDrink, save.StrengthDrink / save.StrengthMax, save.Likability);
            }
        }
    }
}
