using LinePutScript.Localization.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using Panuon.WPF.UI;
using System.Windows.Input;
using ChatVPet.ChatProcess;
using static VPet_Simulator.Core.GraphInfo;
using System.Threading;
using Microsoft.CognitiveServices.Speech;
using LinePutScript;

namespace VPet.Plugin.ChatVPet
{
    public class CVPTTalkAPI : TalkBox
    {
        public Button btnvoice;
        private WrittenFileLinks? fileLinks;
        private readonly TextBlock displayedOptions = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5) };
        private readonly Button retryReply = new() { Content = "重试结果回复", Margin = new Thickness(5), IsEnabled = false };
        public CVPTTalkAPI(CVPPlugin mainPlugin) : base(mainPlugin)
        {
            Plugin = mainPlugin;

            MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });
            btnvoice = new Button()
            {
                Content = "\uEF50",
                FontFamily = (FontFamily)Application.Current.Resources["RemixIcon"],
                BorderThickness = new Thickness(2),
                BorderBrush = Function.ResourcesBrush(Function.BrushType.DARKPrimaryDarker),
                Background = Function.ResourcesBrush(Function.BrushType.SecondaryLight),
                ToolTip = "长按启用语音输入".Translate(),//TODO:未来做快捷键识别
                Cursor = Cursors.Hand,
                FontSize = 30,
                Margin = new Thickness(5, 0, 0, 0),
                Visibility = Plugin.AzureVoiceEnable ? Visibility.Visible : Visibility.Collapsed
            };

            btnvoice.PreviewMouseLeftButtonDown += async (_, _) => await VoiceRecognize();
            btnvoice.PreviewMouseLeftButtonUp += (_, _) => voiceHeld = false;
            btnvoice.LostMouseCapture += (_, _) => voiceHeld = false;
            ButtonHelper.SetCornerRadius(btnvoice, new CornerRadius(4));
            Grid.SetColumn(btnvoice, 3);
            MainGrid.Children.Add(btnvoice);
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var imageButton = new Button { Content = "图片", Margin = new Thickness(5), ToolTip = "附上图片一起发送" };
            Grid.SetColumn(imageButton, MainGrid.ColumnDefinitions.Count - 1);
            MainGrid.Children.Add(imageButton);
            imageButton.Click += async (_, _) =>
            {
                var picker = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg" };
                if (picker.ShowDialog() != true) return;
                string text = string.IsNullOrWhiteSpace(tbTalk.Text) ? "看看这张图片。" : tbTalk.Text;
                await Task.Run(() => RespondWithImage(text, System.IO.File.ReadAllBytes(picker.FileName)));
            };
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var options = new ScrollViewer { Content = displayedOptions, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(options, 1);
            Grid.SetColumnSpan(options, MainGrid.ColumnDefinitions.Count);
            MainGrid.Children.Add(options);
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(retryReply, 2);
            Grid.SetColumnSpan(retryReply, MainGrid.ColumnDefinitions.Count);
            MainGrid.Children.Add(retryReply);
            retryReply.Click += async (_, _) =>
            {
                var turn = Plugin.VPetChatProcess.Dialogues.Select(x => x.Turn)
                    .LastOrDefault(x => x?.PendingReply == true);
                if (turn != null) await RetryTurn(turn.TurnId);
            };
            IsVisibleChanged += async (_, _) =>
            {
                if (!IsVisible || !IsEnabled) return;
                try
                {
                    await Plugin.Ready;
                    await Task.Run(Plugin.EnsureSave);
                    await RefreshSavedConversation();
                }
                catch (Exception ex) { Plugin.MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message); }
            };
        }

        public async Task RetryTurn(string turnId)
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            retryReply.IsEnabled = false;
            try
            {
                await Plugin.Ready;
                await Task.Run(() =>
                {
                    Plugin.EnsureSave();
                    var process = Plugin.VPetChatProcess;
                    if (!process.Dialogues.Any(x => x.Turn?.TurnId == turnId)) return;
                    process.RetryReply(turnId, response => Present(process, response));
                });
            }
            catch (Exception ex) { Plugin.MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message); }
            finally { IsEnabled = true; await RefreshSavedConversation(); }
        }

        private async Task RefreshSavedConversation()
        {
            var process = Plugin.VPetChatProcess;
            var snapshot = await Task.Run(process.ReadMemories);
            if (!Plugin.IsCurrentProcess(process)) return;
            var turns = snapshot.History.Select(x => x.Turn).Where(x => x != null).ToList();
            displayedOptions.Text = turns.SelectMany(x => x!.Receipts).LastOrDefault(x => x.DisplayedItems.Count > 0)?.Facts ?? "";
            retryReply.IsEnabled = turns.Any(x => x!.PendingReply);
        }

        private bool voiceHeld;
        private bool voiceWorking;
        private async Task VoiceRecognize()
        {
            if (!Plugin.AzureVoiceEnable) return;
            voiceHeld = true;
            if (voiceWorking) return;
            voiceWorking = true;
            try
            {
                var key = Plugin.AzureKey;
                var region = Plugin.AzureRegion;
                var language = Plugin.AzureRecognitionLanguage;
                using var recognizer = await Task.Run(() =>
                {
                    var config = SpeechConfig.FromSubscription(key, region);
                    config.SpeechRecognitionLanguage = language;
                    return new SpeechRecognizer(config);
                });
                while (voiceHeld && Plugin.AzureVoiceEnable)
                {
                    var result = await recognizer.RecognizeOnceAsync();
                    if (!Plugin.AzureVoiceEnable) break;
                    if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellation = CancellationDetails.FromResult(result);
                        if (cancellation.Reason == CancellationReason.Error)
                            Plugin.MW.Main.LabelDisplayShow("语音输入失败：" + cancellation.ErrorCode);
                        break;
                    }
                    if (!string.IsNullOrEmpty(result.Text)) tbTalk.AppendText(result.Text);
                }
            }
            catch (Exception ex) { Plugin.MW.Main.LabelDisplayShow("语音输入失败：" + ex.Message); }
            finally { voiceWorking = false; }
        }
        protected CVPPlugin Plugin;
        public override string APIName => "ChatVPetProcess";
        private Task<bool> lastSpeech = Task.FromResult(true);
        public bool LastSpeechCompleted => lastSpeech.IsCompletedSuccessfully && lastSpeech.Result;
        public override void Responded(string content) => RespondWithImage(content, null);

        public void RespondWithImage(string content, byte[]? image)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            Dispatcher.Invoke(() => IsEnabled = false);
            try
            {
                Plugin.Ready.GetAwaiter().GetResult();
                var previousProcess = Plugin.VPetChatProcess;
                Plugin.EnsureSave();
                var process = Plugin.VPetChatProcess;
                if (!ReferenceEquals(previousProcess, process)) Dispatcher.Invoke(() => displayedOptions.Text = "");
                Dispatcher.Invoke(() => retryReply.IsEnabled = false);
                process.HostState = Plugin.SampleState();
                process.SystemDescription = ChatProtocol.RenderSystem(Plugin.MW.Core.Save!.Name, Plugin.MW.Core.Save!.HostName, Plugin.OpenAIConfig!.UseChatVPetPrompt);
                string observation = "";
                if (image != null)
                    observation = Plugin.ObserveAttachedImage(content, image);
                process.Ask(content, observation, response => Present(process, response), new ProcessControl());
                _ = Task.Run(() =>
                {
                    try { process.CompressHistory(); }
                    catch (Exception) { }
                });
            }
            catch (Exception ex)
            {
                Plugin.MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message);
                var turn = Plugin.VPetChatProcess.Dialogues.LastOrDefault()?.Turn;
                if (turn?.PendingReply == true)
                    Dispatcher.Invoke(() => retryReply.IsEnabled = true);
            }
            finally { Dispatcher.Invoke(() => IsEnabled = true); }
        }

        private void Present(VPetChatProcess process, ProcessResponse response)
        {
            if (!Plugin.IsCurrentProcess(process)) return;
            if (response.DisplayedList != null) Dispatcher.Invoke(() => displayedOptions.Text = response.DisplayedList);
            if (string.IsNullOrEmpty(response.Reply))
            {
                if (response.WrittenFiles.Length > 0) Dispatcher.Invoke(() =>
                {
                    if (fileLinks?.Tag as string == response.TurnId)
                        foreach (string path in response.WrittenFiles) fileLinks.AddFile(path);
                });
                return;
            }
            // Operations can run while the initial speech is playing; subsequent replies remain ordered.
            lastSpeech.GetAwaiter().GetResult();
            if (!Plugin.IsCurrentProcess(process)) return;
            var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool shown = false;
            var bar = Plugin.MW.Main.MsgBar;
            void OnEnd() { if (shown) ended.TrySetResult(true); }
            void OnLoaded(object sender, RoutedEventArgs e) => shown = true;
            void OnUnloaded(object sender, RoutedEventArgs e)
            {
                if (shown && bar.Visibility == Visibility.Visible) ended.TrySetResult(false); // Replaced by another host message.
            }
            var marker = Dispatcher.Invoke(() =>
            {
                string cache = Plugin.HasChatCacheUsage ? $", 缓存 {Plugin.ChatCachedTokens}/{Plugin.ChatPromptTokens} 输入Token" : ", 缓存未报告";
                var text = new TextBlock { Text = Plugin.ShowToken ? $"聊天 {Plugin.ChatTokens}, 操作 {Plugin.ToolTokens}, 日记 {Plugin.SummaryTokens} Token" + cache : "", FontSize = 20 };
                var panel = new StackPanel();
                fileLinks = new WrittenFileLinks { Tag = response.TurnId };
                foreach (string path in response.WrittenFiles) fileLinks.AddFile(path);
                panel.Children.Add(fileLinks);
                panel.Children.Add(text);
                panel.Loaded += OnLoaded; panel.Unloaded += OnUnloaded;
                return panel;
            });
            bar.EndAction += OnEnd;
            string? graph = null;
            if (!response.SuppressSayAnimation && response.Expression != null)
            {
                graph = Plugin.MW.Core.Graph!.FindGraphs(response.Expression, AnimatType.A_Start, Plugin.MW.Core.Save!.Mode)
                    ?.FirstOrDefault(g => g.GraphInfo.Type == GraphType.Say && string.Equals(g.GraphInfo.Name, response.Expression, StringComparison.OrdinalIgnoreCase))?.GraphInfo.Name;
            }
            Plugin.MW.Main.Say(response.Reply, marker, graph);
            lastSpeech = FinishSpeech();
            if (response.QuitAfterSpeech) lastSpeech.GetAwaiter().GetResult();

            async Task<bool> FinishSpeech()
            {
                try
                {
                    if (!await ended.Task.WaitAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(false)) return false;
                    var until = DateTime.UtcNow.AddMinutes(2);
                    while (Plugin.MW.Main.PlayingVoice && DateTime.UtcNow < until) await Task.Delay(100).ConfigureAwait(false);
                    return !Plugin.MW.Main.PlayingVoice;
                }
                catch (TimeoutException) { return false; }
                finally
                {
                    bar.EndAction -= OnEnd;
                    if (!Dispatcher.HasShutdownStarted)
                        await Dispatcher.InvokeAsync(() => { marker.Loaded -= OnLoaded; marker.Unloaded -= OnUnloaded; });
                }
            }
        }

        public override void Setting() => Plugin.Setting();
    }
}
