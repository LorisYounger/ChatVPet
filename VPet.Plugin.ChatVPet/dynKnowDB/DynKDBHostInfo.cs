using ChatVPet.ChatProcess;
using LinePutScript;
using LinePutScript.Localization.WPF;
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
            KeyWords = string.Join(" ", localization.WordSplit("主人 当前用户 名字 生日".Translate(MW.GameSavesData.GameSave.Name)));
        }
        public override string KnowledgeData
        {
            get
            {
                return "主人(当前用户}: {0}\n生日: {1}".Translate(MW.GameSavesData.GameSave.HostName, MW.GameSavesData.GetDateTime("HostBDay", MW.GameSavesData[(gdat)"birthday"]));
            }
        }
    }
}
