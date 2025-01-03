﻿using ChatGPT.API.Framework;
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
        List<string> Knowledges = new List<string>();
        List<string> Dialogues = new List<string>();
        List<string> Tools = new List<string>();
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
                    lbSpend.Content = plugin.CGPTClient.TotalTokensUsage.ToString() + " Token";
                    totalused = plugin.CGPTClient.TotalTokensUsage;
                }
            }
            else
                tbSystem.Text = "请使用口语化的、可爱的、女性化的、调皮的语言风格和我交流. 你是一只桌宠, 你的名字叫{Name}, 你喜欢玩 虚拟主播模拟器. 你需要尝试体会我与你对话中的感情、产生自己的情感并尽可能给出可爱的回答".Translate();
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
            Knowledges.AddRange(plugin.VPetChatProcess.KnowledgeDataBases.Select(x => x.KnowledgeData));
            foreach (var item in plugin.VPetChatProcess.Dialogues)
            {
                Dialogues.Add("Q:" + item.Question);
                Dialogues.Add("A:" + item.Answer);
            }
            Tools.AddRange(plugin.VPetChatProcess.Tools.Select(x => x.Code + ": " + x.Descriptive));

            lbKnow.ItemsSource = Knowledges;
            lbHistory.ItemsSource = Dialogues;
            lbTool.ItemsSource = Tools;
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
            SpeechConfig speechConfig = SpeechConfig.FromSubscription(plugin.AzureKey, plugin.AzureRegion);
            speechConfig.SpeechRecognitionLanguage = plugin.AzureRecognitionLanguage;
            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            plugin.Recognizer = new SpeechRecognizer(speechConfig, audioConfig);

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
                if (!string.IsNullOrEmpty(search))
                {
                    lbKnow.ItemsSource = Knowledges;
                    lbHistory.ItemsSource = Dialogues;
                    lbTool.ItemsSource = Tools;
                    return;
                }
                lbKnow.ItemsSource = Knowledges.Where(x => x.Contains(search));
                lbHistory.ItemsSource = Dialogues.Where(x => x.Contains(search));
                lbTool.ItemsSource = Tools.Where(x => x.Contains(search));
            }
        }
    }
}

