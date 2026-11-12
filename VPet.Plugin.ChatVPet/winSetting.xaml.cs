using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class winSetting : Window
    {
        CVPPlugin plugin;
        private readonly VPetChatProcess process;
        private Dialogue[] history = [];
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
            process = plugin.VPetChatProcess;
            if (plugin.OpenAIConfig != null)
            {
                tbAPIKey.Text = plugin.OpenAIConfig.ApiKey;
                tbAPIURL.Text = plugin.OpenAIConfig.ApiUrl;
                tbWebProxy.Text = plugin.OpenAIConfig.WebProxy;
                tbMaxToken.Text = plugin.OpenAIConfig.MaxTokens.ToString();
                tbSystem.Text = ChatProtocol.RenderSystem(plugin.MW.Core.Save!.Name, plugin.MW.Core.Save!.HostName);
                tbTemp.Text = plugin.OpenAIConfig.Temperature.ToString();
                cbModel.Text = plugin.OpenAIConfig.Model;
                tbEmbeddingAPIURL.Text = plugin.OpenAIConfig.EmbeddingApiUrl;
                tbEmbeddingAPIKey.Text = plugin.OpenAIConfig.EmbeddingApiKey;
                tbToolUrl.Text = plugin.OpenAIConfig.ToolApiUrl;
                tbToolKey.Text = plugin.OpenAIConfig.ToolApiKey;
                tbToolModel.Text = plugin.OpenAIConfig.ToolModel;
                tbRerankUrl.Text = plugin.OpenAIConfig.RerankApiUrl;
                tbRerankKey.Text = plugin.OpenAIConfig.RerankApiKey;
                tbRerankModel.Text = plugin.OpenAIConfig.RerankModel;
                cbRerank.IsChecked = plugin.OpenAIConfig.RerankEnabled;
                tbRetrievalTimeout.Text = plugin.OpenAIConfig.RetrievalTimeoutSeconds.ToString();
                tbEmbeddingVersion.Text = plugin.OpenAIConfig.EmbeddingVersion;
                tbRerankVersion.Text = plugin.OpenAIConfig.RerankVersion;
                tbEmbeddingInstruction.Text = plugin.OpenAIConfig.EmbeddingInstruction;
                cbScreenshot.IsChecked = plugin.OpenAIConfig.ScreenshotEnabled;
                cbScreenScope.SelectedIndex = plugin.OpenAIConfig.ScreenshotScope == "screen" ? 1 : 0;
                cbQuit.IsChecked = plugin.OpenAIConfig.AllowQuit;
                tbEmbeddingModel.Text = plugin.OpenAIConfig.EmbeddingModel;
                lbSpend.Content = plugin.TotalTokensUsage.ToString() + " Token";
                totalused = plugin.TotalTokensUsage;
            }
            else
                tbSystem.Text = ChatProtocol.RenderSystem(plugin.MW.Core.Save!.Name, plugin.MW.Core.Save!.HostName);
            swShowToken.IsChecked = plugin.ShowToken;


            tbSpeachKey.Text = plugin.AzureKey;
            tbSpeachregion.Text = plugin.AzureRegion;
            tbSpeachRecognitionLanguage.Text = plugin.AzureRecognitionLanguage;
            swVoiceEnable.IsChecked = plugin.AzureVoiceEnable;
            tbMaxHistory.Text = plugin.MaxHistoryCount.ToString();
            tbMaxKnow.Text = plugin.MaxKnowledgeCount.ToString();
            tbMaxTool.Text = plugin.MaxToolCount.ToString();
            tbMaxRound.Text = plugin.MaxRecallCount.ToString();

            tbMaxHistoryBeforeCompress.Text = plugin.MaxHistoryBeforeCompress.ToString();
            tbDiaryKeepRecentCount.Text = plugin.DiaryKeepRecentCount.ToString();
            tbDiaryDecayRate.Text = plugin.DiaryDecayRate.ToString(System.Globalization.CultureInfo.InvariantCulture);


            tbKnow.Text = plugin.KnowledgeDataBase;
            Knowledges = new ObservableCollection<string>(process.KnowledgeDataBases.Select(x => x.KnowledgeData));
            Loaded += async (_, _) => await LoadDialogue();
            Tools = new ObservableCollection<string>(process.Tools.Select(x => x.Code + ": " + x.Descriptive).ToList());

            lbKnow.ItemsSource = Knowledges;
            lbTool.ItemsSource = Tools;
        }
        private async Task LoadDialogue()
        {
            history = (await Task.Run(process.ReadMemories)).History;
            Dialogues.Clear();
            for (int i = 0; i < history.Length; i++)
            {
                Dialogue item = history[i];
                Dialogues.Add(i + "_Q:" + item.Question);
                Dialogues.Add(i + "_A:" + item.Answer);
                Dialogues.Add(i + "_T:" + string.Join("; ", item.Turn?.Receipts.Select(x => x.ToContext()) ?? []));// + string.Join(", ", ts.Select(x => x.Code + ":" + x.Args)) + ']');
            }
            lbHistory.ItemsSource = Dialogues;
        }
        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {           
            string embeddingBaseUrl = tbAPIURL.Text
                .Replace("/chat/completions", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');
            if (plugin.TalkAPI?.IsEnabled == false)
            {
                MessageBox.Show("请在当前对话结束后保存 API 设置。");
                return;
            }
            if (!int.TryParse(tbMaxToken.Text, out int maxTokens) || maxTokens <= 0
                || !double.TryParse(tbTemp.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temperature) || !double.IsFinite(temperature)
                || !int.TryParse(tbRetrievalTimeout.Text, out int timeout) || timeout <= 0
                || !int.TryParse(tbMaxHistory.Text, out int maxHistory) || maxHistory < 0
                || !int.TryParse(tbMaxKnow.Text, out int maxKnowledge) || maxKnowledge < 0
                || !int.TryParse(tbMaxTool.Text, out int maxTools) || maxTools < 0
                || !int.TryParse(tbMaxRound.Text, out int maxRounds) || maxRounds <= 0
                || !int.TryParse(tbMaxHistoryBeforeCompress.Text, out int compressAt) || compressAt < 0
                || !int.TryParse(tbDiaryKeepRecentCount.Text, out int keepRecent) || keepRecent < 0
                || !float.TryParse(tbDiaryDecayRate.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float decay) || !float.IsFinite(decay) || decay < 0 || decay > 1)
            {
                MessageBox.Show("请检查数量、温度、超时和衰减率的格式。超时、输出 Token 和操作回合数需大于0，衰减率为0到1。");
                return;
            }
            plugin.OpenAIConfig = new OpenAIChatConfig
            {
                ApiKey = tbAPIKey.Text,
                ApiUrl = embeddingBaseUrl,
                WebProxy = tbWebProxy.Text,
                Model = cbModel.Text,
                MaxTokens = maxTokens,
                Temperature = temperature,
                EmbeddingApiUrl = tbEmbeddingAPIURL.Text.TrimEnd('/'),
                EmbeddingApiKey = tbEmbeddingAPIKey.Text,
                EmbeddingModel = string.IsNullOrWhiteSpace(tbEmbeddingModel.Text) ? "Qwen/Qwen3-Embedding-0.6B" : tbEmbeddingModel.Text,
                ToolApiUrl = tbToolUrl.Text.TrimEnd('/'), ToolApiKey = tbToolKey.Text, ToolModel = tbToolModel.Text,
                RerankApiUrl = tbRerankUrl.Text, RerankApiKey = tbRerankKey.Text,
                RerankModel = string.IsNullOrWhiteSpace(tbRerankModel.Text) ? "Qwen/Qwen3-Reranker-0.6B" : tbRerankModel.Text,
                RerankEnabled = cbRerank.IsChecked == true, RetrievalTimeoutSeconds = timeout,
                EmbeddingVersion = tbEmbeddingVersion.Text, RerankVersion = tbRerankVersion.Text,
                EmbeddingInstruction = tbEmbeddingInstruction.Text,
                ScreenshotEnabled = cbScreenshot.IsChecked == true, ScreenshotScope = cbScreenScope.SelectedIndex == 1 ? "screen" : "pet",
                AllowQuit = cbQuit.IsChecked == true,
            };

            plugin.ShowToken = swShowToken.IsChecked ?? false;

            plugin.AzureKey = tbSpeachKey.Text;
            plugin.AzureRegion = tbSpeachregion.Text;
            plugin.AzureRecognitionLanguage = tbSpeachRecognitionLanguage.Text;
            plugin.AzureVoiceEnable = swVoiceEnable.IsChecked ?? false;
            plugin.TalkAPI!.btnvoice.Visibility = swVoiceEnable.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;
            plugin.MaxHistoryCount = maxHistory;
            plugin.MaxKnowledgeCount = maxKnowledge;
            plugin.MaxToolCount = maxTools;
            plugin.MaxRecallCount = maxRounds;

            plugin.MaxHistoryBeforeCompress = compressAt;
            plugin.DiaryKeepRecentCount = keepRecent;
            plugin.DiaryDecayRate = decay;

            plugin.KnowledgeDataBase = tbKnow.Text;
            IsEnabled = false;
            plugin.TalkAPI!.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    plugin.ReloadKnowledge();
                    plugin.ConfigureModels();
                    plugin.ConfigureVoice();
                    plugin.MW.Save();
                });
                Close();
            }
            catch (Exception ex) { MessageBox.Show("设置未能全部应用: " + ex.Message); }
            finally { IsEnabled = true; plugin.TalkAPI.IsEnabled = true; }
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

        private async void RetryHistory_Click(object sender, RoutedEventArgs e)
        {
            if (lbHistory.SelectedItem is string msg && int.TryParse(msg.Split('_')[0], out int id)
                && history.ElementAtOrDefault(id)?.Turn is ChatTurn turn && plugin.TalkAPI != null)
            {
                await plugin.TalkAPI.RetryTurn(turn.TurnId);
                await LoadDialogue();
            }
        }

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (lbHistory.SelectedItem is string msg)
            {
                if (int.TryParse(msg.Split('_')[0], out int id))
                {
                    var entry = history[id];
                    await Task.Run(() => process.DeleteMemory(entry));
                    await LoadDialogue();
                    MessageBox.Show("删除成功".Translate());
                }
            }
        }

        private async void btn_ClearVectorCache(object sender, RoutedEventArgs e)
        {
            await Task.Run(process.ClearRetrievalCache);
            MessageBox.Show("向量缓存已清除".Translate());
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

        private async void btn_SearchDB_vec(object sender, RoutedEventArgs e)
        {
            string search = tbSeachDB.Text;
            if (string.IsNullOrEmpty(search))
            {
                lbKnow.ItemsSource = Knowledges;
                lbHistory.ItemsSource = Dialogues;
                lbTool.ItemsSource = Tools;
                return;
            }

            var result = await Task.Run(() => process.QuerySimilar(search));

            lbKnow.ItemsSource = new ObservableCollection<string>(
                result.Knowledges.Select(x => $"[{x.Score:F3}] {x.Item.KnowledgeData}"));

            ObservableCollection<string> di = new ObservableCollection<string>();
            for (int i = 0; i < result.Dialogues.Count; i++)
            {
                var (item, score) = result.Dialogues[i];
                int dialogueIndex = Array.IndexOf(history, item);
                if (dialogueIndex < 0)
                    continue;

                di.Add($"{dialogueIndex}_[{score:F3}]_Q:{item.Question}");
                di.Add($"{dialogueIndex}_[{score:F3}]_A:{item.Answer}");
                di.Add($"{dialogueIndex}_[{score:F3}]_T:{string.Join("; ", item.Turn?.Receipts.Select(x => x.ToContext()) ?? [])}");
            }
            lbHistory.ItemsSource = di;

            lbTool.ItemsSource = new ObservableCollection<string>(
                result.Tools.Select(x => $"[{x.Score:F3}] {x.Item.Code}: {x.Item.Descriptive}"));
        }

        private async void TestRetrieval_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            try { tbApiTestResult.Text = await Task.Run(plugin.TestRetrievalApis); }
            catch (Exception ex) { tbApiTestResult.Text = ex.Message; }
            finally { button.IsEnabled = true; }
        }

        private void TextBlock_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (LocalizeCore.CurrentCulture.StartsWith("zh"))
                ExtensionFunction.StartURL("https://wiki.exlb.net/vpet/mod/chatvpetprocess");
            else
                ExtensionFunction.StartURL("https://wiki.exlb.net/en/vpet/mod/chatvpetprocess");
        }
    }
}
