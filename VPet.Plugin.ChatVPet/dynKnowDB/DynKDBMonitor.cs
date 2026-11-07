using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
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
            KeyWords = localization.WordSplit("电脑 信息 监控 状况 CPU 内存 占用".Translate(MW.GameSavesData.GameSave.Name));
        }

        /// <summary>
        /// 用于 Embedding 向量生成的语义描述文本
        /// </summary>
        [JsonIgnore]
        public override IEnumerable<string> EmbeddingTexts =>
            [
            "电脑状况如何".Translate(),
            "电脑占用高吗?".Translate(),
            "我还剩多少内存".Translate(),
            "帮我查看电脑信息".Translate(),
            "我内存占用有多少".Translate(),
            "CPU占用如何".Translate(),
            ];

        public override string KnowledgeData
        {
            get
            {
                try
                {
                    float C = CpuCounter.NextValue();
                    float CL = RamCLCounter.NextValue() / 1048576;
                    float AVA = RamAVACounter.NextValue() / 1048576;
                    float R = (CL - AVA) / CL * 100;

                    return "CPU占用率: {0}%\n内存占用率: {1}%\n可用内存: {2}MB\n所有内存: {3}MB".Translate(C, R, AVA, CL);
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
