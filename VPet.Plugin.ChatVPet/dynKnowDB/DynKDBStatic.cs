using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet.dynKnowDB
{
    public class DynKDBStatic : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBStatic(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            var Words = localization.WordSplit("桌宠名称:{0},金钱,数据,体力,心情,饱腹度,口渴度,好感度,当前状态".Translate(MW.GameSavesData.GameSave.Name));

            KeyWords = IKeyWords.GetKeyWords(Words);
            WordsCount = Words.Length;
        }
        public override string KnowledgeData
        {
            get
            {
                var save = MW.GameSavesData.GameSave;
                return "桌宠名称:{0}, 金钱 ${1}, Lv {3} ({4}/{5}), 体力 {7}/{8}({9:f1}), 心情 {10}/{11}({12:f1}), 饱腹度 {13}/{14}({15:f1}), 口渴度 {16}/{17}({18:f1}), 好感度 {19}"
                      .Translate(save.Name, save.Money, save.Level, save.Exp, save.LevelUpNeed(), save.Strength, save.StrengthMax
                      );
            }
        }
    }
}
