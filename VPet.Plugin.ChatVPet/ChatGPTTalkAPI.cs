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

namespace VPet.Plugin.ChatVPet
{
    public class ChatGPTTalkAPI : TalkBox
    {
        public ChatGPTTalkAPI(ChatVPetPlugin mainPlugin) : base(mainPlugin)
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
                Padding = new Thickness(5,0,0,0),
            };
            ButtonHelper.SetCornerRadius(btnvoice, new CornerRadius(4));
            Grid.SetColumn(btnvoice, 3);
            mg.Children.Add(btnvoice);
        }
        protected ChatVPetPlugin Plugin;
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
                //if (Plugin.CGPTClient.Completions.TryGetValue("vpet", out var vpetapi))
                //{
                //    while (vpetapi.messages.Count > Plugin.KeepHistory + 1)
                //    {
                //        vpetapi.messages.RemoveAt(1);
                //    }
                //    var last = vpetapi.messages.LastOrDefault();
                //    if (last != null)
                //    {
                //        if (last.role == ChatGPT.API.Framework.Message.RoleType.user)
                //        {
                //            vpetapi.messages.Remove(last);
                //        }
                //    }
                //}
                //content = "[当前状态: {0}, 好感度:{1}({2})]".Translate(Plugin.MW.Core.Save.Mode.ToString().Translate(), like_str[like_ts((int)Plugin.MW.Core.Save.Likability)].Translate(), (int)Plugin.MW.Core.Save.Likability) + content;
                //var resp = Plugin.CGPTClient.Ask("vpet", content);
                //var reply = resp.GetMessageContent();
                //if (resp.choices[0].finish_reason == "length")
                //{
                //    reply += " ...";
                //}
                //var showtxt = Plugin.ShowToken ? null : "当前Token使用".Translate() + ": " + resp.usage.total_tokens;
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
            Dispatcher.Invoke(() => this.IsEnabled = true);
        }

        public override void Setting() => Plugin.Setting();
    }
}
