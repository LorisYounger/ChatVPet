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

namespace VPet.Plugin.ChatVPet
{
    public class CVPTTalkAPI : TalkBox
    {
        public CVPTTalkAPI(CVPPlugin mainPlugin) : base(mainPlugin)
        {
            Plugin = mainPlugin;
            Grid mg = ((Grid)((Border)Content).Child);
            mg.ColumnDefinitions.Add(new ColumnDefinition() { Width = new System.Windows.GridLength(0.5, System.Windows.GridUnitType.Star) });
            var btnvoice = new Button()
            {
                Content = "\uEF50",
                FontFamily = (FontFamily)Application.Current.Resources["RemixIcon"],
                BorderThickness = new Thickness(2),
                BorderBrush = Function.ResourcesBrush(Function.BrushType.DARKPrimaryDarker),
                Background = Function.ResourcesBrush(Function.BrushType.SecondaryLight),
                ToolTip = "长按启用语音输入".Translate(),//TODO:未来做快捷键识别
                Cursor = Cursors.Hand,
                FontSize = 30,
                Padding = new Thickness(5, 0, 0, 0),
            };
            ButtonHelper.SetCornerRadius(btnvoice, new CornerRadius(4));
            Grid.SetColumn(btnvoice, 3);
            mg.Children.Add(btnvoice);
        }
        protected CVPPlugin Plugin;
        public override string APIName => "ChatVPetProcess";
        public static string[] like_str = new string[] { "陌生", "普通", "喜欢", "爱" };
        public static int like_ts(int like)
        {
            if (like > 50)
            {
                if (like < 100)
                    return 1;
                else if (like < 200)
                    return 2;
                else
                    return 3;
            }
            return 0;
        }
        public override void Responded(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            DisplayThink();
            if (Plugin.CGPTClient == null)
            {
                DisplayThinkToSayRnd("请先前往设置中设置 GPT API".Translate());
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
                        Plugin.MW.Main.LabelDisplayShow("VCP报错: ".Translate() + pr.Reply);
                    }
                    else if (!string.IsNullOrWhiteSpace(pr.Reply))
                    {
                        var showtxt = Plugin.ShowToken ? null : "当前Token使用".Translate() + ": " + Plugin.temptoken;
                        DisplayThinkToSayRndAutoNoForce(pr.Reply, showtxt);
                    }
                    if (pr.IsEnd || pr.IsError)
                    {//结束前的处理
                        Dispatcher.Invoke(() => this.IsEnabled = true);
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
                DisplayThinkToSayRnd("API调用失败".Translate() + $",{str}\n{e}");//, GraphCore.Helper.SayType.Serious);
            }
        }
        /// <summary>
        /// 显示思考结束并说话 (不强制切动画,智能化处理)
        /// </summary>
        public void DisplayThinkToSayRndAutoNoForce(string text, string? desc = null)
        {
            var think = MainPlugin.MW.Core.Graph.FindGraphs("think", AnimatType.C_End, MainPlugin.MW.Core.Save.Mode);
            if (Plugin.MW.Main.DisplayType.Name == "think")
            {
                Action Next = () => { MainPlugin.MW.Main.SayRnd(text, true, desc); };
                if (think.Count > 0)
                {
                    MainPlugin.MW.Main.Display(think[Function.Rnd.Next(think.Count)], Next);
                }
                else
                {
                    Next();
                }
            }
        }
        public override void Setting() => Plugin.Setting();
    }
}
