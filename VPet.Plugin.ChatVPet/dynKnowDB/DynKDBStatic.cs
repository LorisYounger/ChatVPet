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
    /// <summary>
    /// 桌宠动态知识库
    /// </summary>
    public class DynKDBStatic : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBStatic(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            KeyWords = string.Join(" ", localization.WordSplit("桌宠名称:{0} 金钱(钱) 数据 体力 心情 饱腹度 口渴度 好感度 当前状态 饥饿 饿 口渴 渴".Translate(MW.GameSavesData.GameSave.Name)));
            ImportanceWeight_Plus = 0.1f;
            ImportanceWeight_Muti = 1.5f;
        }
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
