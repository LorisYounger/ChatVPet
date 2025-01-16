using ChatGPT.API.Framework;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VPet_Simulator.Windows.Interface;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech;
using ChatVPet.ChatProcess;
using System.Collections.ObjectModel;

namespace VPet.Plugin.ChatVPet
{
    /// <summary>
    /// winSetting.xaml 的交互逻辑
    /// </summary>
    public partial class winSetting : Window
    {
        CVPPlugin plugin;
        long totalused = 0;
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            ExtensionFunction.StartURL("https://github.com/LorisYounger/ChatVPet");
        }

        private void Hyperlink_Click_1(object sender, RoutedEventArgs e)
        {
            ExtensionFunction.StartURL("https://github.com/LorisYounger/ChatVPet/blob/main/TrainingProtocol.md");
        }
        ObservableCollection<string> Knowledges = new ObservableCollection<string>();
        ObservableCollection<string> Dialogues = new ObservableCollection<string>();
        ObservableCollection<string> Tools = new ObservableCollection<string>();
        public winSetting(CVPPlugin plugin)
        {
            InitializeComponent();
            Resources = Application.Current.Resources;

            this.plugin = plugin;
            if (plugin.CGPTClient != null)
            {
                tbAPIKey.Text = plugin.CGPTClient.APIKey;
                tbAPIURL.Text = plugin.CGPTClient.APIUrl;
                if (plugin.CGPTClient.Completions["vpet"] != null)
                {
                    tbMaxToken.Text = plugin.CGPTClient.Completions["vpet"].max_tokens.ToString();
                    tbSystem.Text = plugin.CGPTClient.Completions["vpet"].messages[0].content;
                    tbTemp.Text = plugin.CGPTClient.Completions["vpet"].temperature.ToString();
                    cbModel.Text = plugin.CGPTClient.Completions["vpet"].model;
                    var msgs = plugin.CGPTClient.Completions["vpet"].messages.ToList();
                    msgs.RemoveAt(0);
                    //tbHistory.Text = JsonConvert.SerializeObject(msgs);
                    lbSpend.Content = plugin.TotalTokensUsage.ToString() + " Token";
                    totalused = plugin.CGPTClient.TotalTokensUsage;
                }
            }
            else
                tbSystem.Text = "请使用口语化的、可爱的、女性化的、调皮的语言风格和我交流. 你是一只桌宠, 你的名字叫{0}, 你喜欢玩 虚拟主播模拟器. 你需要尝试体会我与你对话中的感情、产生自己的情感并尽可能给出可爱的回答".Translate(plugin.MW.Core.Save.Name);
            //niKeepHistory.Value = plugin.KeepHistory;
            swShowToken.IsChecked = plugin.ShowToken;
            swSubmitLog.IsChecked = plugin.AllowSubmit;


            //第二页
            tbSpeachKey.Text = plugin.AzureKey;
            tbSpeachregion.Text = plugin.AzureRegion;
            tbSpeachRecognitionLanguage.Text = plugin.AzureRecognitionLanguage;
            swVoiceEnable.IsChecked = plugin.AzureVoiceEnable;
            tbMaxHistory.Text = plugin.MaxHistoryCount.ToString();
            tbMaxKnow.Text = plugin.MaxKnowledgeCount.ToString();
            tbMaxTool.Text = plugin.MaxToolCount.ToString();
            tbMaxRound.Text = plugin.MaxRecallCount.ToString();


            //显示现有知识库
            tbKnow.Text = plugin.KnowledgeDataBase;
            Knowledges = new ObservableCollection<string>(plugin.VPetChatProcess.KnowledgeDataBases.Select(x => x.KnowledgeData));
            LoadDialogue();
            Tools = new ObservableCollection<string>(plugin.VPetChatProcess.Tools.Select(x => x.Code + ": " + x.Descriptive).ToList());

            lbKnow.ItemsSource = Knowledges;
            //lbHistory.ItemsSource = Dialogues;
            lbTool.ItemsSource = Tools;
        }
        private void LoadDialogue()
        {
            Dialogues.Clear();
            for (int i = 0; i < plugin.VPetChatProcess.Dialogues.Count; i++)
            {
                Dialogue? item = plugin.VPetChatProcess.Dialogues[i];
                Dialogues.Add(i + "_Q:" + item.Question);
                Dialogues.Add(i + "_A:" + item.Answer);
                //List<ToolCall> ts;
                //try
                //{
                //    ts = JsonConvert.DeserializeObject<List<ToolCall>>(item.ToolCall) ?? new List<ToolCall>();
                //}
                //catch
                //{
                //    ts = new List<ToolCall>();
                //}
                Dialogues.Add(i + "_T:" + item.ToolCall);// + string.Join(", ", ts.Select(x => x.Code + ":" + x.Args)) + ']');
            }
            lbHistory.ItemsSource = Dialogues;
        }
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (tbAPIURL.Text.Split('/').Length <= 2 && !tbAPIURL.Text.Contains("completions"))
            {
                tbAPIURL.Text += "/v1/chat/completions";
            }
            plugin.CGPTClient = new ChatGPTClient(tbAPIKey.Text, tbAPIURL.Text)
            {
                TotalTokensUsage = totalused
            };
            plugin.CGPTClient.CreateCompletions("vpet", tbSystem.Text.Replace("{Name}", plugin.MW.Core.Save.Name));
            if (!string.IsNullOrWhiteSpace(tbWebProxy.Text))
            {
                plugin.CGPTClient.WebProxy = tbWebProxy.Text;
                plugin.CGPTClient.Proxy = new HttpClientHandler()
                {
                    Proxy = new WebProxy(plugin.CGPTClient.WebProxy),
                    UseProxy = true
                };
            }
            plugin.CGPTClient.Completions["vpet"].model = cbModel.Text;
            plugin.CGPTClient.Completions["vpet"].frequency_penalty = 0.2;
            plugin.CGPTClient.Completions["vpet"].presence_penalty = 1;
            plugin.CGPTClient.Completions["vpet"].max_tokens = Math.Min(Math.Max(int.Parse(tbMaxToken.Text), 10), 4000);
            plugin.CGPTClient.Completions["vpet"].temperature = Math.Min(Math.Max(double.Parse(tbTemp.Text), 0.1), 2);
            //var l = JsonConvert.DeserializeObject<List<Message>>(tbHistory.Text);
            //if (l != null)
            //    plugin.CGPTClient.Completions["vpet"].messages.AddRange(l);
            //plugin.KeepHistory = (int)niKeepHistory.Value.Value;
            plugin.ShowToken = swShowToken.IsChecked ?? false;

            plugin.AzureKey = tbSpeachKey.Text;
            plugin.AzureRegion = tbSpeachregion.Text;
            plugin.AzureRecognitionLanguage = tbSpeachRecognitionLanguage.Text;
            plugin.AzureVoiceEnable = swVoiceEnable.IsChecked ?? false;
#pragma warning disable CS8602 // 解引用可能出现空引用。
            plugin.TalkAPI.btnvoice.Visibility = swVoiceEnable.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;
#pragma warning restore CS8602 // 解引用可能出现空引用。
            plugin.AllowSubmit = swSubmitLog.IsChecked ?? false;
            plugin.Recognizer?.Dispose();
            if (plugin.AzureVoiceEnable && !string.IsNullOrEmpty(plugin.AzureKey) && !string.IsNullOrEmpty(plugin.AzureRegion))
            {
                try
                {
                    SpeechConfig speechConfig = SpeechConfig.FromSubscription(plugin.AzureKey, plugin.AzureRegion);
                    speechConfig.SpeechRecognitionLanguage = plugin.AzureRecognitionLanguage;
                    var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                    plugin.Recognizer = new SpeechRecognizer(speechConfig, audioConfig);
                }
                catch
                {
                    plugin.AzureVoiceEnable = false;
                    MessageBox.Show("语音识别初始化失败, 请检查密钥和区域是否正确".Translate());
                }
            }

            plugin.MaxHistoryCount = int.Parse(tbMaxHistory.Text);
            plugin.MaxKnowledgeCount = int.Parse(tbMaxKnow.Text);
            plugin.MaxToolCount = int.Parse(tbMaxTool.Text);
            plugin.MaxRecallCount = int.Parse(tbMaxRound.Text);



            plugin.KnowledgeDataBase = tbKnow.Text;
            plugin.Save();
            this.Close();
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExtensionFunction.StartURL("https://learn.microsoft.com/azure/ai-services/speech-service/language-support?tabs=stt#speech-to-text");
        }

        private void tbSeachDB_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string search = tbSeachDB.Text;
                if (string.IsNullOrEmpty(search))
                {
                    lbKnow.ItemsSource = Knowledges;
                    lbHistory.ItemsSource = Dialogues;
                    lbTool.ItemsSource = Tools;
                    return;
                }
                lbKnow.ItemsSource = new ObservableCollection<string>(Knowledges.Where(x => x.Contains(search)));
                lbHistory.ItemsSource = new ObservableCollection<string>(Dialogues.Where(x => x.Contains(search)));
                lbTool.ItemsSource = new ObservableCollection<string>(Tools.Where(x => x.Contains(search)));
            }
        }

        private void lb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
                if (listBox?.SelectedItem != null)
                {
                    if (listBox.Tag is not ToolTip toolTip)
                    {
                        toolTip = new ToolTip()
                        {
                            PlacementTarget = listBox,
                            StaysOpen = false
                        };
                        listBox.Tag = toolTip;
                    }
                    toolTip.IsOpen = false;
                    toolTip.Content = listBox.SelectedItem;
                    toolTip.IsOpen = true;
                }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (lbHistory.SelectedItem is string msg)
            {
                if (int.TryParse(msg.Split('_')[0], out int id))
                {
                    plugin.VPetChatProcess.Dialogues.RemoveAt(id);
                    LoadDialogue();
                    MessageBox.Show("删除成功".Translate());
                }
            }
        }

        private void btn_SearchDB(object sender, RoutedEventArgs e)
        {
            string search = tbSeachDB.Text;
            if (string.IsNullOrEmpty(search))
            {
                lbKnow.ItemsSource = Knowledges;
                lbHistory.ItemsSource = Dialogues;
                lbTool.ItemsSource = Tools;
                return;
            }
            lbKnow.ItemsSource = new ObservableCollection<string>(Knowledges.Where(x => x.Contains(search)));
            lbHistory.ItemsSource = new ObservableCollection<string>(Dialogues.Where(x => x.Contains(search)));
            lbTool.ItemsSource = new ObservableCollection<string>(Tools.Where(x => x.Contains(search)));
        }

        private void btn_SearchDB_vec(object sender, RoutedEventArgs e)
        {

            string search = tbSeachDB.Text;
            if (string.IsNullOrEmpty(search))
            {
                lbKnow.ItemsSource = Knowledges;
                lbHistory.ItemsSource = Dialogues;
                lbTool.ItemsSource = Tools;
                return;
            }
            var vector = plugin.VPetChatProcess.W2VEngine!.GetQueryVector(search);
            ////为所有知识库和工具添加向量 已经自动更新 除了新插入的消息可能需要手动更新
            plugin.VPetChatProcess.W2VEngine.GetQueryVector(plugin.VPetChatProcess.Tools);
            plugin.VPetChatProcess.W2VEngine.GetQueryVector(plugin.VPetChatProcess.KnowledgeDataBases);
            plugin.VPetChatProcess.W2VEngine.GetQueryVector(plugin.VPetChatProcess.Dialogues);

            ObservableCollection<string> kdbs = new ObservableCollection<string>(plugin.VPetChatProcess.KnowledgeDataBases.Select(x => (x, x.InCheck(search, W2VEngine.ComputeCosineSimilarity(x.Vector!, vector))))
                    .OrderBy(x => x.Item2).Where(x => x.Item2 < IInCheck.IgnoreValue)
                    .Select(x => x.x.KnowledgeData).ToList());
            List<Tool> tools = plugin.VPetChatProcess.Tools.Select(x => (x, x.InCheck(search, W2VEngine.ComputeCosineSimilarity(x.Vector!, vector))))
                .OrderBy(x => x.Item2).Where(x => x.Item2 < IInCheck.IgnoreValue).Select(x => x.x).ToList();
            List<Dialogue> dialogues = plugin.VPetChatProcess.Dialogues.Select(x => (x, x.InCheck(search, W2VEngine.ComputeCosineSimilarity(x.Vector!, vector)))).OrderBy(x => x.Item2)
                .Where(x => x.Item2 < IInCheck.IgnoreValue).Select(x => x.x).ToList();
            lbKnow.ItemsSource = kdbs;

            ObservableCollection<string> di = new ObservableCollection<string>();
            for (int i = 0; i < dialogues.Count; i++)
            {
                Dialogue? item = dialogues[i];
                di.Add(i + "_Q:" + item.Question);
                di.Add(i + "_A:" + item.Answer);
                di.Add(i + "_T:" + item.ToolCall);
            }
            lbHistory.ItemsSource = di;

            lbTool.ItemsSource = new ObservableCollection<string>(tools.Select(x => x.Code + ": " + x.Descriptive));
        }
    }
}

