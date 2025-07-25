using ChatGPT.API.Framework;
using LinePutScript.Localization.WPF;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;
using static VPet_Simulator.Core.Main;
using static VPet_Simulator.Core.WorkTimer;

namespace VPet.Plugin.ChatVPet
{
    public partial class CVPPlugin
    {
        /// <summary>
        /// 让桌宠自己买东西吃
        /// </summary>
        public string? ToolTakeItem(Food item)
        {
            //看是什么模式
            if (MW.Set.EnableFunction)
            {//$10以内的食物允许赊账
                if (item.Price >= 10 && item.Price >= MW.Core.Save.Money)
                {//买不起                   
                    return "您没有足够金钱来购买 {0}\n您需要 {1:f2} 金钱来购买\n您当前 {2:f2} 拥有金钱"
                        .Translate(item.TranslateName, item.Price, MW.Core.Save.Money);
                }
                //看看是否超模
                if (MW.HashCheck && item.IsOverLoad())
                {
                    return null;
                }
                MW.TakeItem(item);
            }
            MW.DisplayFoodAnimation(item.GetGraph(), item.ImageSource);
            return null;
        }
        /// <summary>
        /// 让桌宠自己工作
        /// </summary>
        public string? ToolDoWork(GraphHelper.Work work)
        {
            MW.Dispatcher.Invoke(() => MW.Main.ToolBar.StartWork(work.Double(MW.Set["workmenu"].GetInt("double_" + work.Name, 1))));
            return null;
        }

        public string? ToolStopWork(Dictionary<string, string> args)
        {
            MW.Dispatcher.Invoke(() => MW.Main.WorkTimer.Stop(reason: FinishWorkInfo.StopReason.MenualStop));
            return null;
        }
        public string? ToolDance(Dictionary<string, string> args)
        {
            var ig = MW.Core.Graph.FindGraphs("music", AnimatType.A_Start, MW.Core.Save.Mode);
            if (ig != null && ig.Count != 0)
            {
                MW.Main.CountNomal = 0;
                MW.Main.Display(ig[Function.Rnd.Next(ig.Count)], () =>
                MW.Main.DisplayBLoopingToNomal("music", Function.Rnd.Next(5, 20)));
            }
            return null;
        }
        public string? ToolTouchHead(Dictionary<string, string> args)
        {
            MW.Main.DisplayTouchHead();
            return null;
        }
        public string? ToolTouchBody(Dictionary<string, string> args)
        {
            MW.Main.DisplayTouchBody();
            return null;
        }
        public string? ToolIdel(Dictionary<string, string> args)
        {
            if (Function.Rnd.Next(2) == 0)
                MW.Main.DisplayIdel();
            else
                MW.Main.DisplayIdel_StateONE();
            return null;
        }
        public string? ToolSleep(Dictionary<string, string> args)
        {
            var m = MW.Main;
            MW.Dispatcher.Invoke(() => 
            {
                if (m.State == Main.WorkingState.Nomal)
                {
                    // 正常状态直接睡觉
                    m.DisplaySleep(true);
                }
                else if (m.State != Main.WorkingState.Sleep)
                {
                    // 如果正在工作，先停止工作再睡觉
                    m.WorkTimer.Stop(() => m.DisplaySleep(true), WorkTimer.FinishWorkInfo.StopReason.MenualStop);
                }
                // 如果已经是睡眠状态，什么都不做
            });
            return null;
        }
        public string? ToolWakeup(Dictionary<string, string> args)
        {
            var m = MW.Main;
            if (m.State == Main.WorkingState.Sleep)
            {
                if (m.Core.Save.Mode == IGameSave.ModeType.Ill)
                    return null;
                m.State = WorkingState.Nomal;
                m.Display(GraphType.Sleep, AnimatType.C_End, m.DisplayNomal);
            }
            return null;
        }
        public string? ToolMove(Dictionary<string, string> args)
        {
            MW.Main.DisplayMove();
            return null;
        }
        public int temptoken = 0;
        /// <summary>
        /// 调用GPT的方法
        /// </summary>
        public string GPTAsk(string system, List<string[]> historys, string message)
        {
            Completions completions = new Completions();
            if (CGPTClient == null)
                throw new Exception("请先前往设置中设置 GPT API".Translate());
            completions.max_tokens = CGPTClient.Completions["vpet"].max_tokens;
            completions.temperature = CGPTClient.Completions["vpet"].temperature;
            completions.model = CGPTClient.Completions["vpet"].model;
            completions.frequency_penalty = CGPTClient.Completions["vpet"].frequency_penalty;
            completions.presence_penalty = CGPTClient.Completions["vpet"].presence_penalty;

            completions.messages.Add(new Message() { role = Message.RoleType.system, content = system });
            foreach (var h in historys)
            {
                completions.messages.Add(new Message() { role = Message.RoleType.user, content = h[0] });
                completions.messages.Add(new Message() { role = Message.RoleType.system, content = h[1] });
            }
            completions.messages.Add(new Message() { role = Message.RoleType.user, content = message });
            var resp = completions.GetResponse(CGPTClient.APIUrl, CGPTClient.APIKey, CGPTClient.Proxy);
            var reply = resp!.GetMessageContent();
            if (resp.choices.Length == 0)
            {
                throw new Exception("请检查API token设置".Translate());
            }
            else if (resp.choices[0].finish_reason == "length")
            {
                reply += " ...";
            }
            temptoken = resp.usage.total_tokens;
            TotalTokensUsage += temptoken;
            TokenCount = temptoken;
            if (AllowSubmit)
            {
                upsysmessage = system;
                upquestion = message;
                uphistory = historys;
            }
            return reply!;
        }
        public void RunDIY(string content)
        {
            if (content.Contains(@":\"))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = content;
                    startInfo.UseShellExecute = false;
                    Process.Start(startInfo);
                }
                catch
                {
                    try
                    {
                        try
                        {
                            Process.Start(content);
                        }
                        catch
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = content,
                                UseShellExecute = true
                            };
                            Process.Start(psi);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                    }
                }
            }
            else if (content.Contains("://"))
            {
                try
                {
                    ExtensionFunction.StartURL(content);
                }
                catch (Exception e)
                {
                    MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                }
            }
            else
            {
                try
                {
                    System.Windows.Forms.SendKeys.SendWait(content);
                }
                catch (Exception e)
                {
                    MessageBoxX.Show("快捷键运行失败:无法运行指定内容".Translate() + '\n' + e.Message);
                }
            }
        }

        public string? ToolModifyState(Dictionary<string, string> args)
        {
            if (args.TryGetValue("exp", out string? exp))
            {
                if (int.TryParse(exp, out int expi))
                {
                    var max = MW.GameSavesData.GameSave.LevelUpNeed() * 0.1;
                    SideMessage += "\n" + "经验值".Translate() + " " + ExtensionFunction.ValueToPlusPlus(expi, 1 / 4, 6);
                    MW.Core.Save.Exp += Math.Max(-max, Math.Min(expi, max));
                    return null;
                }
            }
            if (args.TryGetValue("money", out string? money))
            {
                if (double.TryParse(money, out double moneyi))
                {
                    SideMessage += "\n" + "金钱".Translate() + " " + ExtensionFunction.ValueToPlusPlus(moneyi, 1 / 100, 6);
                    MW.Core.Save.Money += Math.Max(-1000, Math.Min(moneyi, 1000));
                    return null;
                }
            }
            if (args.TryGetValue("feeling", out string? feeling))
            {
                if (double.TryParse(feeling, out double feelingi))
                {
                    SideMessage += "\n" + "心情".Translate() + " " + ExtensionFunction.ValueToPlusPlus(feelingi, 1 / 3, 6);
                    var max = MW.Core.Save.FeelingMax * 0.5;
                    MW.Core.Save.Feeling += Math.Max(-max, Math.Min(feelingi, max));
                    return null;
                }
            }
            if (args.TryGetValue("likability", out string? likability))
            {
                if (double.TryParse(likability, out double likabilityi))
                {
                    SideMessage += "\n" + "好感度".Translate() + " " + ExtensionFunction.ValueToPlusPlus(likabilityi, 1.5, 6);
                    MW.Core.Save.Likability += Math.Max(-10, Math.Min(likabilityi, 10));
                    return null;
                }
            }
            return null;
        }
    }
}
