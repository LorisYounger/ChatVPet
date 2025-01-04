using ChatGPT.API.Framework;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using VPet_Simulator.Windows.Interface;
using LinePutScript.Localization.WPF;
using ChatVPet.ChatProcess;
using Newtonsoft.Json;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using VPet_Simulator.Core;

namespace VPet.Plugin.ChatVPet
{
    public partial class CVPPlugin : MainPlugin
    {
        public VPetChatProcess VPetChatProcess = new VPetChatProcess();
        public CVPPlugin(IMainWindow mainwin) : base(mainwin)
        {
            //在MW里添加个公共变量的位置,方便插件之间的交互
            if (MW.DynamicResources.TryGetValue("CVPKnowledgeDataBase", out object? kdbo) && kdbo is List<KnowledgeDataBase> kdb)
            {
                VPetChatProcess.KnowledgeDataBases = kdb;
            }
            else
            {
                MW.DynamicResources["CVPKnowledgeDataBase"] = VPetChatProcess.KnowledgeDataBases;
                needinitknowledge = true;
            }
            if (MW.DynamicResources.TryGetValue("CVPTools", out object? tbo) && tbo is List<Tool> tb)
            {
                VPetChatProcess.Tools = tb;
            }
            else
            {
                MW.DynamicResources["CVPTools"] = VPetChatProcess.Tools;
                needinittool = true;
            }
        }
        public ChatGPTClient? CGPTClient;
        public SpeechRecognizer? Recognizer;
        bool needinitknowledge = false;
        bool needinittool = false;
        public CVPTTalkAPI? TalkAPI;

        public override void LoadPlugin()
        {            
            TalkAPI = new CVPTTalkAPI(this);
            MW.TalkAPI.Add(TalkAPI);
            var menuItem = new MenuItem()
            {
                Header = "ChatVPetProcess".Translate(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            menuItem.Click += (s, e) => { Setting(); };
            MW.Main.ToolBar.MenuMODConfig.Items.Add(menuItem);
            VPetChatProcess.GPTAskFunction = GPTAsk;

            Task.Run(() =>
            {
                try
                {
                    if (MW.DynamicResources.TryGetValue("CVPLC", out object? lc) && lc is ILocalization il)
                    {
                        VPetChatProcess.Localization = il;
                    }
                    else
                    {
                        if (LocalizeCore.CurrentCulture == "zh-Hans")
                        {
                            VPetChatProcess.Localization = new ILocalization.LChineseSimple();
                        }
                        else if (LocalizeCore.CurrentCulture == "zh-Hant")
                        {
                            VPetChatProcess.Localization = new ILocalization.LChineseTraditional();
                        }
                        else
                        {
                            VPetChatProcess.Localization = new ILocalization.LEnglish();
                        }
                    }


                    if (needinitknowledge)
                    {
                        //添加默认知识库
                        for (int i = 0; i < MW.Foods.Count; i++)
                        {
                            var x = MW.Foods[i];
                            VPetChatProcess.AddKnowledgeDataBase("物品名称: {0}\n类型: {1}".Translate(x.TranslateName, x.Type.ToString().Translate()) +
                              '\n' + x.Description);
                        }
                        for (int i = 0; i < MW.Core.Graph.GraphConfig.Works.Count; i++)
                        {
                            var x = MW.Core.Graph.GraphConfig.Works[i];
                            VPetChatProcess.AddKnowledgeDataBase("活动名称: {0}\n类型 {1}\n持续时间: {2}分钟".Translate(x.NameTrans, x.Type.ToString().Translate(), x.Time));
                        }
                        //VPetChatProcess.AddKnowledgeDataBase(MW.SchedulePackage.Select(x =>
                        //"自动{0}套餐:\n名称: {1}\n描述: {2}\n持续时间 {3}(天)".Translate(x.WorkType.ToString().Translate(), x.NameTrans, x.DescribeTrans
                        //, x.Duration, x.LevelInNeed)).ToArray());
                        //添加自带的知识库
                        VPetChatProcess.AddKnowledgeDataBase(Properties.Resources.VpetKnowledgeDataBase.Replace("\r", "").Split('\n').Select(x => x.Translate()).ToArray());
                        //当前状态
                        VPetChatProcess.KnowledgeDataBases.Add(new dynKnowDB.DynKDBStatic(MW, VPetChatProcess.Localization));
                        VPetChatProcess.KnowledgeDataBases.Add(new dynKnowDB.DynKDBTime(MW, VPetChatProcess.Localization));
                        VPetChatProcess.KnowledgeDataBases.Add(new dynKnowDB.DynKDBMonitor(MW, VPetChatProcess.Localization));
                    }
                    //添加设置的知识库                  
                    VPetChatProcess.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                    if (needinittool)
                    {
                        VPetChatProcess.Tools.Add(new Tool("dance", "让桌宠跳舞(舞)".Translate(), ToolDance, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("stopactive", "让桌宠停止(中断)当前进行的活动,例如(结束,别去)工作(干活)/玩耍(玩)/学习(学)".Translate(), ToolStopWork, [], VPetChatProcess.Localization, 2, 0.05));
                        VPetChatProcess.Tools.Add(new Tool("idle", "让桌宠发呆(呆)".Translate(), ToolIdel, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("sleep", "让桌宠睡觉(睡眠,就寝)".Translate(), ToolSleep, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("wakeup", "让桌宠起床(别睡了)".Translate(), ToolWakeup, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("touchhead", "摸桌宠的头(脑袋)".Translate(), ToolTouchHead, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("touchbody", "摸桌宠的身体(肚子)".Translate(), ToolTouchBody, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new Tool("move", "让桌宠自由移动(走动,走路)".Translate(), ToolMove, [], VPetChatProcess.Localization));
                        //吃东西和工作直接加到工具中
                        for (int i = 0; i < MW.Foods.Count; i++)
                        {
                            var x = MW.Foods[i];
                            VPetChatProcess.Tools.Add(new Tool("take" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.TranslateName), (x) => ToolTakeItem(i), [], VPetChatProcess.Localization));
                        }
                        for (int i = 0; i < MW.Core.Graph.GraphConfig.Works.Count; i++)
                        {
                            var x = MW.Core.Graph.GraphConfig.Works[i];
                            VPetChatProcess.Tools.Add(new Tool("do" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.NameTrans), (x) => ToolDoWork(i), [], VPetChatProcess.Localization));
                        }

                        foreach (ISub sub in MW.Set["diy"])
                            VPetChatProcess.Tools.Add(new Tool(sub.Name, sub.Name, (x) => { RunDIY(sub.Info); return null; }, [], VPetChatProcess.Localization));

                    }
                    //然后历史消息从设置中加载
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"))
                    {
                        var d = JsonConvert.DeserializeObject<List<Dialogue>>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"));
                        if (d != null)
                            VPetChatProcess.Dialogues = d;
                    }
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatGPTSetting.json"))
                    {
                        CGPTClient = ChatGPTClient.Load(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatGPTSetting.json"));
                        if (CGPTClient != null && CGPTClient.Completions.TryGetValue("vpet", out var vpet))
                            VPetChatProcess.SystemDescription = vpet.messages[0].content;
                    }

                    if (AzureVoiceEnable)
                    {
                        SpeechConfig speechConfig = SpeechConfig.FromSubscription(AzureKey, AzureRegion);
                        speechConfig.SpeechRecognitionLanguage = AzureRecognitionLanguage;
                        var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                        Recognizer = new SpeechRecognizer(speechConfig, audioConfig);
                    }

                    VPetChatProcess.W2VEngine = new W2VEngine(VPetChatProcess);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            });
        }
        public override void Save()
        {
            if (CGPTClient != null)
                File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatGPTSetting.json", CGPTClient.Save(), encoding: Encoding.UTF8);
            File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json", JsonConvert.SerializeObject(VPetChatProcess.Dialogues), Encoding.UTF8);
        }
        public override void Setting()
        {
            new winSetting(this).ShowDialog();
        }
        public override string PluginName => "ChatVPetProcess";

    }
}
