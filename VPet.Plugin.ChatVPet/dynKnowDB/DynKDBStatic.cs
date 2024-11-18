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
            var Words = localization.WordSplit("桌宠名称:{0},金钱(钱),数据,体力,心情,饱腹度,口渴度,好感度,当前状态".Translate(MW.GameSavesData.GameSave.Name));

            KeyWords = IKeyWords.GetKeyWords(Words);
            WordsCount = Words.Length;
        }
        public override string KnowledgeData
        {
            get
            {
                var save = MW.GameSavesData.GameSave;
                return "桌宠名称:{0}, 金钱 ${1}, Lv {2} ({3}/{4}), 体力 {5}/{6}({7:f1}), 心情 {8}/{9}({10:f1}), 饱腹度 {11}/{6}({12:f1}), 口渴度 {13}/{6}({14:f1}), 好感度 {15}"
                      .Translate(save.Name, save.Money, save.Level, save.Exp, save.LevelUpNeed(), save.Strength, save.StrengthMax, (save.Strength / save.StrengthMax),
                      save.Feeling, save.FeelingMax, (save.Feeling / save.FeelingMax), save.StrengthFood, save.StrengthFood / save.StrengthMax,
                      save.StrengthDrink, save.StrengthDrink / save.StrengthMax, save.Likability);
            }
        }
    }
}
