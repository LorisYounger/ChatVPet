using ChatVPet.ChatProcess;
using LinePutScript;
using LinePutScript.Localization.WPF;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VPet_Simulator.Windows.Interface;
using static ChatVPet.ChatProcess.ToolUse;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
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

            var diaryMenuItem = new MenuItem()
            {
                Header = "桌宠日记".Translate(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            diaryMenuItem.Click += (s, e) => { new winDiary(this).ShowDialog(); };
            MW.Main.ToolBar.MenuMODConfig.Items.Add(diaryMenuItem);
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
                    VPetChatProcess.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => IText.ConverText(x, MW.Main)).ToArray());

                    if (needinittool)
                    {
                        VPetChatProcess.Tools.AddRange(VPetChatProcess.GetBuiltinDiaryTools());
                        VPetChatProcess.Tools.Add(new ToolUse("dance", "让桌宠跳舞".Translate(), "跳舞 舞".Translate().Split(' '), ["你能跳个舞吗".Translate(), "给我跳舞看看".Translate(), "我想看你跳舞".Translate(), "来一段舞蹈吧".Translate(), "跳个舞给我看".Translate()], ToolDance, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("stopactive", "让桌宠停止当前进行的活动,例如工作/玩耍/学习".Translate(), "停止 活动 结束 别去 工作 干活 玩耍 玩 学习 学".Translate().Split(' '), ["别工作了,休息一下吧".Translate(), "停下来歇一歇".Translate(), "不要再干活了".Translate(), "先停下来吧".Translate(), "放下手头的事".Translate(), "不要再玩了".Translate(), "别学习了".Translate()], ToolStopWork, [], VPetChatProcess.Localization, 2, 0.05));
                        VPetChatProcess.Tools.Add(new ToolUse("idle", "让桌宠发呆".Translate(), "发呆 呆".Translate().Split(' '), ["发会儿呆吧".Translate(), "你就这样发呆好了".Translate(), "放空一下吧".Translate(), "什么都不用做,发呆就好".Translate(), "就这样待着吧".Translate()], ToolIdel, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("sleep", "让桌宠睡觉".Translate(), "睡觉 睡眠 就寝".Translate().Split(' '), ["快去睡觉吧".Translate(), "该休息了,去睡觉".Translate(), "你看起来很累,去睡吧".Translate(), "早点睡觉哦".Translate(), "去好好睡一觉".Translate()], ToolSleep, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("wakeup", "让桌宠起床".Translate(), "起床 别睡了 睡".Translate().Split(' '), ["快起床了".Translate(), "不要再睡了,起来吧".Translate(), "天都亮了,该起床了".Translate(), "起来起来,别睡了".Translate(), "该醒了".Translate()], ToolWakeup, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("touchhead", "摸桌宠的头".Translate(), "摸头 摸脑袋 摸".Translate().Split(' '), ["我想摸摸你的头".Translate(), "让我摸摸你的脑袋".Translate(), "摸摸头".Translate(), "给你一个摸头杀".Translate(), "头好软,摸一摸".Translate()], ToolTouchHead, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("touchbody", "摸桌宠的身体".Translate(), "摸身体 摸肚子".Translate().Split(' '), ["让我摸摸你的肚子".Translate(), "摸摸你的身体".Translate(), "肚子摸摸".Translate(), "摸一下肚子".Translate(), "揉揉肚子".Translate()], ToolTouchBody, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("move", "让桌宠自由移动".Translate(), "移动 走动 走路".Translate().Split(' '), ["去到处走走吧".Translate(), "到处移动一下".Translate(), "你可以自由走动".Translate(), "随便走走吧".Translate(), "动一动".Translate()], ToolMove, [], VPetChatProcess.Localization));
                        VPetChatProcess.Tools.Add(new ToolUse("modifystate", "根据对话情绪和上下文适当调整桌宠的状态(例如：聊天高兴时增加心情或好感度，获得奖励时增加经验值或金钱)".Translate(),
                            "调整状态 高兴 心情 好感度 奖励 经验值 金钱".Translate().Split(' '), ["好好学习了吗".Translate(), "给我点钱".Translate(), "开心一点".Translate(), "喜欢我吗".Translate()], ToolModifyState, [
                            new Arg(){  Name = "exp",Type = "double" , Description="增加经验值(负数为扣除)[±1000]".Translate() },
                            new Arg(){  Name = "money",Type = "double" ,Description="增加金钱(负数为扣除)[±1000]".Translate() },
                            new Arg(){  Name = "feeling",Type = "double" ,Description="增加心情值(负数为扣除)[±20]".Translate() },
                            new Arg(){  Name = "likability",Type = "double" ,Description="增加好感度(负数为扣除)[±5.0]".Translate() }
                            ], VPetChatProcess.Localization, 3, 0.5));
                        //吃东西和工作直接加到工具中
                        for (int i = 0; i < MW.Foods.Count; i++)
                        {
                            var x = MW.Foods[i];
                            VPetChatProcess.Tools.Add(new ToolUse("take" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.TranslateName),
                                ["吃".Translate(), "喝".Translate(), (x.Type.ToString() + "ing").Translate(), x.Name, x.TranslateName, x.Description],
                                [$"{(x.Type.ToString() + "ing").Translate()} {x.TranslateName}"], (_) => ToolTakeItem(x), [], VPetChatProcess.Localization));
                        }
                        for (int i = 0; i < MW.Core.Graph.GraphConfig.Works.Count; i++)
                        {
                            var x = MW.Core.Graph.GraphConfig.Works[i];
                            VPetChatProcess.Tools.Add(new ToolUse("do" + x.Name, "让桌宠 {0} {1}".Translate((x.Type.ToString() + "ing").Translate(), x.NameTrans),
                                ["干活".Translate(), "工作".Translate(), "玩".Translate(), "学".Translate(), (x.Type.ToString() + "ing").Translate(), x.Name, x.NameTrans],
                                [$"{(x.Type.ToString() + "ing").Translate()} {x.NameTrans}"],
                                (_) => ToolDoWork(x), [], VPetChatProcess.Localization));
                        }

                        foreach (ISub sub in MW.Set["diy"])
                            VPetChatProcess.Tools.Add(new ToolUse(sub.Name, sub.Name, [], [], (x) => { RunDIY(sub.Info); return null; }, [], VPetChatProcess.Localization));

                    }
                    //然后历史消息从设置中加载
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"))
                    {
                        var d = JsonConvert.DeserializeObject<List<Dialogue>>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessHistory.json"));
                        if (d != null)
                            VPetChatProcess.Dialogues = d;
                    }
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatVPetProcessDiary.json"))
                    {
                        var diary = JsonConvert.DeserializeObject<List<DiaryEntry>>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessDiary.json"));
                        if (diary != null)
                        {
                            VPetChatProcess.DiaryEntries = diary;
                            if (diary.Count > 0)
                                DiaryEntry.EnsureCounterAbove(diary.Max(e => e.Id));
                        }
                    }
                    if (File.Exists(ExtensionValue.BaseDirectory + @"\OpenAISetting.json"))
                    {
                        var oas = JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(ExtensionValue.BaseDirectory + @"\OpenAISetting.json"));
                        if (oas != null)
                        {
                            OpenAIConfig = oas;
                            VPetChatProcess.SystemDescription = OpenAIConfig.SystemPrompt;
                        }
                    }

                    // Apply persisted settings to VPetChatProcess
                    VPetChatProcess.MaxHistoryCount = MaxHistoryCount;
                    VPetChatProcess.MaxKnowledgeCount = MaxKnowledgeCount;
                    VPetChatProcess.MaxToolCount = MaxToolCount;
                    VPetChatProcess.MaxToolRecallCount = MaxRecallCount;
                    VPetChatProcess.MaxHistoryBeforeCompress = MaxHistoryBeforeCompress;
                    VPetChatProcess.DiaryKeepRecentCount = DiaryKeepRecentCount;
                    VPetChatProcess.DiaryDecayRate = DiaryDecayRate;
                    VPetChatProcess.MaxDiaryInContext = MaxDiaryInContext;


                    VPetChatProcess.W2VEngine = new W2VEngine(VPetChatProcess);
                    if (!string.IsNullOrWhiteSpace(OpenAIConfig?.EmbeddingApiKey))
                    {//只有写过设置的才预先生成EMBedding向量，避免没有设置API就加载插件时因为调用API而报错
                        VPetChatProcess.W2VEngine.GetQueryVector(VPetChatProcess.KnowledgeDataBases);
                        VPetChatProcess.W2VEngine.GetQueryVector(VPetChatProcess.Tools);
                        VPetChatProcess.W2VEngine.GetQueryVector(VPetChatProcess.Dialogues);
                        VPetChatProcess.W2VEngine.GetQueryVector(VPetChatProcess.DiaryEntries);
                    }

                    //最后初始化语音识别, 这玩意最容易出bug，所以放最后，前面都成功了就算这个失败了也不至于影响使用
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
            File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatVPetProcessDiary.json", JsonConvert.SerializeObject(VPetChatProcess.DiaryEntries), Encoding.UTF8);
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
