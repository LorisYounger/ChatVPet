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
using static ChatVPet.ChatProcess.ToolUse;
using System.Collections;
using System.Net;
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
            if (MW.DynamicResources.TryGetValue("CVPTools", out object? tbo) && tbo is List<ToolUse> tb)
            {
                VPetChatProcess.Tools = tb;
            }
            else if (MW.DynamicResources.TryGetValue("CVPTools", out tbo) && tbo is IEnumerable tools)
            {
                List<ToolUse> list = [];
                foreach (var item in tools)
                {
                    if (item is ToolUse toolUse)
                        list.Add(toolUse);
                }
                if (list.Count > 0)
                {
                    VPetChatProcess.Tools = list;
                    MW.DynamicResources["CVPTools"] = VPetChatProcess.Tools;
                }
                else
                {
                    MW.DynamicResources["CVPTools"] = VPetChatProcess.Tools;
                    needinittool = true;
                }
            }
            else
            {
                MW.DynamicResources["CVPTools"] = VPetChatProcess.Tools;
                needinittool = true;
            }
        }
        public OpenAIChatConfig? OpenAIConfig;
        public SpeechRecognizer? Recognizer;
        bool needinitknowledge = false;
        bool needinittool = false;
        public CVPTTalkAPI? TalkAPI;
        /// <summary>
        /// 侧边消息,随着输出输出
        /// </summary>
        public string SideMessage = "";
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
            VPetChatProcess.AIAPIAskFunction = OpenAIAsk;
            VPetChatProcess.AIAPIEmbeddingFunction = OpenAIEmbedding;
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
                        try
                        {
                            VPetChatProcess.KnowledgeDataBases.Add(new dynKnowDB.DynKDBMonitor(MW, VPetChatProcess.Localization));
                        }
                        catch
                        {

                        }
                    }
                    //添加设置的知识库                  
                    VPetChatProcess.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                    if (needinittool)
                    {
                        VPetChatProcess.Tools.Add(new ToolUse("dance", "让桌宠跳舞(舞)".Translate(), [], ToolDance, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("stopactive", "让桌宠停止(中断)当前进行的活动,例如(结束,别去)工作(干活)/玩耍(玩)/学习(学)".Translate(), [], ToolStopWork, [], VPetChatProcess.Localization, 2, 0.05));
                        VPetChatProcess.Tools.Add(new ToolUse("idle", "让桌宠发呆(呆)".Translate(), [], ToolIdel, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("sleep", "让桌宠睡觉(睡眠,就寝)".Translate(), [], ToolSleep, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("wakeup", "让桌宠起床(别睡了)".Translate(), [], ToolWakeup, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("touchhead", "摸桌宠的头(脑袋)".Translate(), [], ToolTouchHead, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("touchbody", "摸桌宠的身体(肚子)".Translate(), [], ToolTouchBody, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("move", "让桌宠自由移动(走动,走路)".Translate(), [], ToolMove, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("modifystate", "根据对话情绪和上下文适当调整桌宠的状态(例如：聊天高兴时增加心情或好感度，获得奖励时增加经验值或金钱)".Translate(), [], ToolModifyState, [
                            new Arg(){  Name = "exp",Description="[double?]增加经验值(负数为扣除)[±1000]".Translate() },
                            new Arg(){  Name = "money",Description="[double?]增加金钱(负数为扣除)[±1000]".Translate() },
                            new Arg(){  Name = "feeling",Description="[double?]增加心情值(负数为扣除)[±20]".Translate() },
                            new Arg(){  Name = "likability",Description="[double?]增加好感度(负数为扣除)[±5.0]".Translate() }
                            ], VPetChatProcess.Localization));
                        //吃东西和工作直接加到工具中
                        for (int i = 0; i < MW.Foods.Count; i++)
                        {
                            var x = MW.Foods[i];
                            VPetChatProcess.Tools.Add(new ToolUse("take" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.TranslateName), [], (_) => ToolTakeItem(x), [], VPetChatProcess.Localization));
                        }
                        for (int i = 0; i < MW.Core.Graph.GraphConfig.Works.Count; i++)
                        {
                            var x = MW.Core.Graph.GraphConfig.Works[i];
                            VPetChatProcess.Tools.Add(new ToolUse("do" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.NameTrans), [], (_) => ToolDoWork(x), [], VPetChatProcess.Localization));
                        }

                        foreach (ISub sub in MW.Set["diy"])
                            VPetChatProcess.Tools.Add(new ToolUse(sub.Name, sub.Name, [], (x) => { RunDIY(sub.Info); return null; }, [], VPetChatProcess.Localization));

                    }
                    //然后历史消息从设置中加载
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"))
                    {
                        var d = JsonConvert.DeserializeObject<List<Dialogue>>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"));
                        if (d != null)
                            VPetChatProcess.Dialogues = d;
                    }
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\OpenAISetting.json"))
                    {
                        OpenAIConfig = JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\OpenAISetting.json"));
                        if (OpenAIConfig != null)
                            VPetChatProcess.SystemDescription = OpenAIConfig.SystemPrompt;
                    }

                    if (AzureVoiceEnable)
                    {
                        try
                        {
                            SpeechConfig speechConfig = SpeechConfig.FromSubscription(AzureKey, AzureRegion);
                            speechConfig.SpeechRecognitionLanguage = AzureRecognitionLanguage;
                            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                            Recognizer = new SpeechRecognizer(speechConfig, audioConfig);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.ToString());
                        }
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
            if (OpenAIConfig != null)
                File.WriteAllText(ExtensionValue.BaseDirectory + @"\OpenAISetting.json", JsonConvert.SerializeObject(OpenAIConfig), encoding: Encoding.UTF8);
            File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json", JsonConvert.SerializeObject(VPetChatProcess.Dialogues), Encoding.UTF8);
        }
        public override void Setting()
        {
            new winSetting(this).ShowDialog();
        }
        public override string PluginName => "ChatVPetProcess";


        public class ProcessUpload
        {
            public string Question { get; set; } = string.Empty;
            public string Anser { get; set; } = string.Empty;
            public string SystemMessage { get; set; } = string.Empty;
            public long SteamID { get; set; }

            public List<string[]> History { get; set; } = new List<string[]>();

            public string Language { get; set; } = string.Empty;
        }

        public string upsysmessage = "";
        public List<string[]> uphistory = new List<string[]>();
        public string upresponse = "";
        public string upquestion = "";
        public async void UploadMessage()
        {
            if (!MW.IsSteamUser || string.IsNullOrWhiteSpace(upquestion) || string.IsNullOrWhiteSpace(upresponse) || string.IsNullOrWhiteSpace(upsysmessage))
            {
                return;
            }

            try
            {
                ProcessUpload pu = new ProcessUpload()
                {
                    SteamID = (long)MW.SteamID,
                    Anser = upresponse,
                    Question = upquestion,
                    SystemMessage = upsysmessage,
                    History = uphistory,
                    Language = LocalizeCore.CurrentCulture
                };
                var content = JsonConvert.SerializeObject(pu);

                string _url = "https://aiopen.exlb.net:5810/SubMitProcess";
                // 参数
                var request = (HttpWebRequest)WebRequest.Create(_url);
                request.Method = "POST";
                request.ContentType = "application/json"; // ContentType
                byte[] byteData = Encoding.UTF8.GetBytes(content);
                int length = byteData.Length;
                request.ContentLength = length;
                using (Stream writer = request.GetRequestStream())
                {
                    writer.Write(byteData, 0, length);
                    writer.Close();
                    writer.Dispose();
                }
                string responseString;
                using (var response = await request.GetResponseAsync())
                {
                    responseString = new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();
                    response.Dispose();
                }
            }
            catch (Exception e)
            {
#if DEBUG
                MessageBox.Show(e.ToString());
#endif
            }
        }

    }
}
