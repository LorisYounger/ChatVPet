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

namespace VPet.Plugin.ChatVPet
{
    public partial class CVPPlugin : MainPlugin
    {
        public VPetChatProcess VPetChatProcess = new VPetChatProcess();
        public CVPPlugin(IMainWindow mainwin) : base(mainwin)
        {
            //TODO: 在MW里添加个公共变量的位置,方便插件之间的交互
            //if (MW.Core.Graph.CommConfig.TryGetValue("CVPKnowledgeDataBase", out object? kdbo) && kdbo is List<KnowledgeDataBase> kdb)
            //{
            //    VPetChatProcess.KnowledgeDataBases = kdb;
            //}
            //else
            //{
            //    MW.Core.Graph.CommConfig["CVPKnowledgeDataBase"] = VPetChatProcess.KnowledgeDataBases;
            //    needinitknowledge = true;
            //}
            //if (MW.Core.Graph.CommConfig.TryGetValue("CVPTools", out object? tbo) && tbo is List<Tool> tb)
            //{
            //    VPetChatProcess.Tools = tb;
            //}
            //else
            //{
            //    MW.Core.Graph.CommConfig["CVPTools"] = VPetChatProcess.Tools;
            //    needinittool = true;
            //}
        }
        public ChatGPTClient? CGPTClient;
        bool needinitknowledge = false;
        bool needinittool = false;
        //TODO: 2轮加载Plugin
        public override void LoadPlugin()
        {
            if (MW.Core.Graph.CommConfig.TryGetValue("CVPKnowledgeDataBase", out object? kdbo) && kdbo is List<KnowledgeDataBase> kdb)
            {
                VPetChatProcess.KnowledgeDataBases = kdb;
            }
            else
            {
                MW.Core.Graph.CommConfig["CVPKnowledgeDataBase"] = VPetChatProcess.KnowledgeDataBases;
                needinitknowledge = true;
            }
            if (MW.Core.Graph.CommConfig.TryGetValue("CVPTools", out object? tbo) && tbo is List<Tool> tb)
            {
                VPetChatProcess.Tools = tb;
            }
            else
            {
                MW.Core.Graph.CommConfig["CVPTools"] = VPetChatProcess.Tools;
                needinittool = true;
            }

            MW.TalkAPI.Add(new CVPTTalkAPI(this));
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
                if (MW.Core.Graph.CommConfig.TryGetValue("CVPLC", out object? lc) && lc is ILocalization il)
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
                        VPetChatProcess.AddKnowledgeDataBase("itemID: " + i.ToString() + '\n' +
                            x.Description + $"\n{"类型".Translate()}: {x.Type.ToString().Translate()}");
                    }
                    for (int i = 0; i < MW.Core.Graph.GraphConfig.Works.Count; i++)
                    {
                        var x = MW.Core.Graph.GraphConfig.Works[i];
                        VPetChatProcess.AddKnowledgeDataBase("activeID: " + i.ToString() + '\n' +
                            "活动名称: {0}\n类型 {1}\n持续时间: {2}分钟".Translate(x.NameTrans, x.Type.ToString().Translate(), x.Time));
                    }
                    VPetChatProcess.AddKnowledgeDataBase(MW.SchedulePackage.Select(x =>
                    "自动{0}套餐:\n名称: {1}\n描述: {2}\n持续时间 {3}(天)".Translate(x.WorkType.ToString().Translate(), x.NameTrans, x.DescribeTrans
                    , x.Duration, x.LevelInNeed)).ToArray());
                    //添加自带的知识库
                    VPetChatProcess.AddKnowledgeDataBase(Properties.Resources.VpetKnowledgeDataBase.Split('\n'));
                    //TODO 当前状态/统计动态知识库
                }
                //添加设置的知识库
                VPetChatProcess.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n'));


                if (needinittool)
                {
                    VPetChatProcess.Tools.Add(new Tool("takeitem", "使用有物品id的物品,例如吃食物/正餐/零食/喝饮料/功能性/药品/送礼品".Translate(), ToolTakeItem, new List<Tool.Arg>()
                        {
                            new Tool.Arg(){ Name = "itemID", Description = "(int)物品id, 没有物品ID的物品没法使用".Translate() }
                        }, VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("doactive", "进行有活动id的活动,例如工作/玩耍/学习".Translate(), ToolDoWork, new List<Tool.Arg>()
                    {
                          new Tool.Arg(){ Name = "activeID", Description = "(int)活动id, 没有活动ID的活动无法进行".Translate() }
                    }, VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("dance", "让桌宠跳舞".Translate(), ToolDance, [], VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("idle", "让桌宠发呆".Translate(), ToolIdel, [], VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("sleep", "让桌宠睡觉".Translate(), ToolSleep, [], VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("touchhead", "摸桌宠的头".Translate(), ToolTouchHead, [], VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("touchbody", "摸桌宠的身体".Translate(), ToolTouchBody, [], VPetChatProcess.Localization));
                    VPetChatProcess.Tools.Add(new Tool("move", "让桌宠移动位置".Translate(), ToolMove, [], VPetChatProcess.Localization));
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
            });
        }
        public override void Save()
        {
            if (CGPTClient != null)
                File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatGPTSetting.json", CGPTClient.Save());
        }
        public override void Setting()
        {
            new winSetting(this).ShowDialog();
        }
        public override string PluginName => "ChatVPetProcess";



    }
}
