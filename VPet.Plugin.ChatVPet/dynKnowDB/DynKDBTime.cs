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
    public class DynKDBTime : KnowledgeDataBase
    {
        IMainWindow MW;
        public DynKDBTime(IMainWindow mw, ILocalization localization)
        {
            MW = mw;
            KeyWords = string.Join(" ", localization.WordSplit("当前 时间 今天 现在 日期".Translate(MW.GameSavesData.GameSave.Name)));
            ImportanceWeight_Plus = 0.15f;
            ImportanceWeight_Muti = 1.5f;
        }
        public override string KnowledgeData
        {
            get => DateTime.Now.ToString();
        }

    }
}
