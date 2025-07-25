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

namespace VPet.Plugin.ChatVPet
{
    public class CVPTTalkAPI : TalkBox
    {
        public Button btnvoice;
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

            btnvoice.PreviewMouseDown += (s, e) => Task.Run(VoiceRecognize);
            btnvoice.PreviewMouseUp += (s, e) => voicecontinue = false;
            ButtonHelper.SetCornerRadius(btnvoice, new CornerRadius(4));
            Grid.SetColumn(btnvoice, 3);
            MainGrid.Children.Add(btnvoice);
        }
        private bool voicecontinue = false;
        private bool voiceisworking = false;
        private void VoiceRecognize()
        {
            //如果没有语音识别或者正在进行语音识别则返回
            if (Plugin.Recognizer == null)
                return;
            voicecontinue = true;
            if (voiceisworking)
            {
                return;
            }
            voiceisworking = true;
            while (voicecontinue == true)
            {
                try
                {
                    var rr = Plugin.Recognizer.RecognizeOnceAsync().Result;
                    if (string.IsNullOrEmpty(rr.Text))
                    {
                        continue;
                    }
                    Dispatcher.Invoke(() => tbTalk.AppendText(rr.Text));
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            }
            voiceisworking = false;
        }
        protected CVPPlugin Plugin;
        public override string APIName => "ChatVPetProcess";
        //public static string[] like_str = ["完全陌生", "稍微了解", "普通朋友", "好朋友", "喜欢", "信任", "亲密", "爱慕"];
        //public static int like_ts(int like)
        //{
        //    return int.Max(7, (int)Math.Sqrt(like / 16) - 2);
        //}
        public override void Responded(string content) // 处理响应内容
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            DisplayThink(); // 显示思考动画
            if (Plugin.CGPTClient == null)
            {
                MessageBox.Show("请先前往设置中设置 GPT API".Translate());
                return;
            }
            Dispatcher.Invoke(() => this.IsEnabled = false);
            try
            {
                var pc = new ProcessControl();
                Plugin.VPetChatProcess.Ask(content, (pr) =>
                {
                    if (pr.IsError)
                    {
                        MessageBox.Show("VCP报错: ".Translate() + pr.Reply); // 显示错误消息弹窗Label
                    }
                    else if (!string.IsNullOrWhiteSpace(pr.Reply))
                    {
                        string? showtxt;
                        if (Plugin.ShowToken)
                        {
                            showtxt = "当前Token使用".Translate() + ": " + Plugin.temptoken + Plugin.SideMessage;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(Plugin.SideMessage))
                                showtxt = null;
                            else
                                showtxt = Plugin.SideMessage.Trim('\n');
                        }
                        Plugin.SideMessage = "";
                        DisplayThinkToSayRndAutoNoForce(pr.Reply, showtxt);
                    }
                    if (pr.IsEnd || pr.IsError)
                    {//结束前的处理
                        Dispatcher.Invoke(() => this.IsEnabled = true);
                        if (Plugin.AllowSubmit && !pr.IsError)
                        {
                            string? model = Plugin.CGPTClient.Completions["vpet"].model?.ToLower();
                            if (string.IsNullOrWhiteSpace(model) || !model.Contains("gpt-4") || !model.Contains("o1") || !model.Contains("pro") ||
                            !model.Contains("plus") || !model.Contains("max"))//确保提交质量 
                                return;

                            string[]? msg = Plugin.VPetChatProcess.Dialogues.LastOrDefault()?.ToMessages(Plugin.VPetChatProcess.Localization);
                            if (msg != null)
                            {
                                Plugin.upquestion = content;
                                Plugin.upresponse = msg[1];
                                Plugin.UploadMessage();
                            }
                        }
                    }
                    else if (pr.ListPosition >= Plugin.MaxRecallCount)
                    {
                        Plugin.MW.Main.LabelDisplayShow("轮询次数超过{0},已停止下一轮轮询".Translate(Plugin.MaxRecallCount));
                        pc.StopBeforeNext = true;
                    }
                }, pc);
                //DisplayThinkToSayRnd(reply, desc: showtxt);
            }
            catch (Exception exp)
            {
                var e = exp.ToString();
                string str = "请检查设置和网络连接".Translate();
                if (e.Contains("401"))
                {
                    str = "请检查API token设置".Translate();
                }
                DisplayThinkToSayRndAutoNoForce("API调用失败".Translate() + $",{str}\n{e}"); //, GraphCore.Helper.SayType.Serious);
                MessageBox.Show("API调用失败".Translate() + $",{str}\n{e}"); // 显示错误消息弹窗
                Dispatcher.Invoke(() => this.IsEnabled = true); // 恢复按钮可用状态
            }
        }
        bool istalksuccess = false;
        /// <summary>
        /// 显示思考结束并说话 (不强制切动画,智能化处理)
        /// </summary>
        public void DisplayThinkToSayRndAutoNoForce(string text, string? desc = null)
        {
            if (Plugin.MW.Main.DisplayType.Name == "think")
            {
                var think = MainPlugin.MW.Core.Graph.FindGraphs("think", AnimatType.C_End, MainPlugin.MW.Core.Save.Mode);
                istalksuccess = false;
                Action Next = () => { istalksuccess = true; MainPlugin.MW.Main.SayRnd(text, true, desc); };
                if (think.Count > 0)
                {
                    MainPlugin.MW.Main.Display(think[Function.Rnd.Next(think.Count)], Next);
                }
                else
                {
                    Next();
                }
                Task.Run(() =>
                {
                    Thread.Sleep(2000);
                    if (!istalksuccess)
                    {
                        MainPlugin.MW.Main.SayRnd(text, false, desc);
                    }
                });
            }
            else
            {
                MainPlugin.MW.Main.SayRnd(text, false, desc);
            }
        }
        public override void Setting() => Plugin.Setting();
    }
}
