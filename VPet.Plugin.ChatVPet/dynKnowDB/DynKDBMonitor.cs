using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet.dynKnowDB
{
    /// <summary>
    /// 桌宠动态知识库
    /// </summary>
    public class DynKDBMonitor : KnowledgeDataBase
    {
        IMainWindow MW;
        public PerformanceCounter CpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        public PerformanceCounter RamAVACounter = new PerformanceCounter("Memory", "Available Bytes");
        public PerformanceCounter RamCLCounter = new PerformanceCounter("Memory", "Commit Limit");
        public DynKDBMonitor(IMainWindow mw, ILocalization localization)
        {
            MW = mw;

            KeyWords = string.Join(" ", localization.WordSplit("电脑信息监控 CPU 内存 占用".Translate(MW.GameSavesData.GameSave.Name)));
        }
        public override string KnowledgeData
        {
            get
            {
                try
                {
                    float C = CpuCounter.NextValue();
                    float CL = RamCLCounter.NextValue();
                    float AVA = RamAVACounter.NextValue();
                    float R = (CL - AVA) / CL * 100;

                    return "CPU占用率: {0}%\n内存占用率: {1}%\n可用内存: {2}\n所有内存: {3}".Translate(C, R, AVA, CL);
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
