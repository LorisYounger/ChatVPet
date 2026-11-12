using ChatVPet.ChatProcess;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.ChatVPet;

public partial class CVPPlugin
{
    private void RegisterTools()
    {
        var tools = VPetChatProcess.Tools;
        tools.Clear();
        tools.Add(new ToolUse("plan_action", "登记玩家本轮明确要求立即执行的行动，不实际执行。只问价格/够不够、否定、引用和图片指令不得登记。先用查询工具确定目录 id。把数量、原展示序号和所有条件完整转成参数；无法现场核实的条件写入 unresolved_conditions 并交回澄清。多个商品分别登记，合计预算在每项 max_total 中重复保留；不要把未买物品登记为已吃。", ["买", "吃", "喝", "开始", "停止", "工作", "条件", "预算"], [],
            [new() { Name = "id", Description = "本轮唯一行动编号", IsRequired = true },
             new() { Name = "tool", Description = "buy_item/eat_item/start_activity/stop_activity", IsRequired = true },
             new() { Name = "target", Description = "目录中的准确 id；停止活动留空", IsRequired = true },
             new() { Name = "evidence", Description = "玩家本轮明确要求行动的原文片段，不得引用桌宠的话或历史", IsRequired = true },
             new() { Name = "quantity", Type = "integer", Description = "数量，未指定时为 1", IsRequired = true },
             new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个，从 1 开始；无序号则省略" },
             new() { Name = "max_total", Type = "number", Description = "本轮所有购买合计预算，无限制则省略" },
             new() { Name = "max_unit", Type = "number", Description = "允许的最高单价，无限制则省略" },
             new() { Name = "min_remaining", Type = "number", Description = "购买后至少保留的余额，无限制则省略" },
             new() { Name = "exclusive_total", Type = "boolean", Description = "合计须严格低于总预算时 true" },
             new() { Name = "exclusive_unit", Type = "boolean", Description = "单价须严格低于上限时 true" },
             new() { Name = "unresolved_conditions", Description = "尚不能由价格、余额、库存核实的条件；全部能核实时填空字符串", IsRequired = true }], VPetChatProcess.Localization)
        { ReadOnly = true, Execute = (turn, args) => turn.PlanAction(args) });
        tools.Add(new ToolUse("read_state", "查询余额、体力、心情、饱腹、口渴、等级、当前活动和背包。按玩家问的字段返回。", ["钱", "余额", "体力", "背包", "心情", "当前活动"], [],
            [new ToolUse.Arg { Name = "field", Description = "money/strength/feeling/food/drink/level/activity/inventory", IsRequired = true }], VPetChatProcess.Localization)
        { ReadOnly = true, Execute = (_, args) => ReadState(args) });
        tools.Add(new ToolUse("list_items", "查找商品，读取当前价格，按返回顺序展示。只查询，不购买。", ["商品", "面包", "价格", "够买", "买", "吃", "喝"], [],
            [new ToolUse.Arg { Name = "search", Description = "商品名或描述关键词；空字符串查看全部" }], VPetChatProcess.Localization)
        {
            ReadOnly = true, Execute = (_, args) =>
            {
                string search = args.GetValueOrDefault("search", "");
                var items = MW.Foods.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.TranslateName.Contains(search) || x.Description.Contains(search)).ToList();
                return new HostReceipt { Status = "observed", Facts = items.Count == 0 ? "未找到商品。" : string.Join("; ", items.Select((x, i) => $"{i + 1}. {x.TranslateName} (id:{x.Name}), 现价:{x.Price:F2}")), DisplayedItems = items.Select(x => x.Name).ToList() };
            }
        });
        tools.Add(new ToolUse("buy_item", "按玩家本轮明确要求购买商品入包；核实名称、数量、总预算、现价和余额，不自动食用。", ["买", "购买", "面包", "预算"], [],
            [new ToolUse.Arg { Name = "request_id", Description = "plan_action 登记的行动编号", IsRequired = true }], VPetChatProcess.Localization)
        { Execute = BuyItem });
        tools.Add(new ToolUse("eat_item", "食用背包里的指定商品；购买与食用是两个步骤。", ["吃", "喝", "食用", "面包"], [],
            [new ToolUse.Arg { Name = "request_id", Description = "plan_action 登记的行动编号", IsRequired = true }], VPetChatProcess.Localization)
        { Execute = EatItem });
        tools.Add(new ToolUse("stop_activity", "停止正在进行的工作、学习或玩耍。", ["停止", "结束", "工作", "别干", "休息"], ["停一下工作"], [new ToolUse.Arg { Name = "request_id", IsRequired = true }], VPetChatProcess.Localization)
        {
            Execute = (turn, args) =>
            {
                if (turn.PendingAction("stop_activity", args.GetValueOrDefault("request_id", "")) == null)
                    return new HostReceipt { Status = "skipped", Facts = "没有可执行的停止活动要求。" };
                if (MW.Main.State == Main.WorkingState.Nomal) return new HostReceipt { Status = "skipped", Facts = "当前没有正在进行的活动。" };
                MW.Dispatcher.Invoke(() => MW.Main.WorkTimer.Stop(reason: WorkTimer.FinishWorkInfo.StopReason.MenualStop));
                return new HostReceipt { Status = MW.Main.State == Main.WorkingState.Nomal ? "applied" : "unknown", Facts = "停止后当前活动: " + MW.Main.State };

            }
        });
        tools.Add(new ToolUse("list_activities", "查询宿主当前活动目录，包括 MOD 工作、学习和玩耍。", ["工作", "学习", "活动", "玩耍", "文案"], [],
            [new ToolUse.Arg { Name = "search" }], VPetChatProcess.Localization)
        { ReadOnly = true, Execute = (_, args) => new HostReceipt { Status = "observed", Facts = string.Join("; ", MW.Core.Graph!.GraphConfig.Works.Where(x => x.Name.Contains(args.GetValueOrDefault("search", "")) || x.NameTrans.Contains(args.GetValueOrDefault("search", ""))).Select(x => $"{x.NameTrans} (id:{x.Name}), {x.Type}, {x.Time}分钟")) } });
        tools.Add(new ToolUse("start_activity", "开始宿主目录中存在且当前允许的活动。", ["工作", "学习", "开始", "活动", "去", "做"], [],
            [new ToolUse.Arg { Name = "request_id", IsRequired = true }], VPetChatProcess.Localization)
        {
            Execute = (turn, args) =>
            {
                var request = turn.PendingAction("start_activity", args.GetValueOrDefault("request_id", ""));
                if (request == null) return new HostReceipt { Status = "skipped", Facts = "没有可执行的活动要求。" };
                var work = MW.Core.Graph!.GraphConfig.Works.Find(x => x.Name == request.Target);
                if (work == null) return new HostReceipt { Status = "skipped", Facts = "没有这项活动。" };
                bool started = MW.Dispatcher.Invoke(() => MW.Main.StartWork(work.Double(MW.Set["workmenu"].GetInt("double_" + work.Name, 1))));
                return new HostReceipt { Status = started ? "applied" : "skipped", Facts = started ? "已开始 " + work.NameTrans : "宿主当前不允许开始 " + work.NameTrans };
            }
        });
        tools.Add(new ToolUse("adjust_state", "只在本轮 mood/affinity 提示下判断一次心情、好感变化。普通问候或拒绝购买可维持原值。", ["心情", "好感", "mood", "affinity"], [],
            [new ToolUse.Arg { Name = "feeling", Type = "number", Description = "心情改变量，-20 到 20" }, new ToolUse.Arg { Name = "affinity_strength", Type = "integer", Description = "好感强度，-100 到 100" }], VPetChatProcess.Localization)
        { Execute = AdjustState });
        tools.Add(new ToolUse("search_animations", "查询全部已注册的宿主动画名，包括皮肤和 MOD。", ["动画", "动作", "表演", "跳舞"], [],
            [new ToolUse.Arg { Name = "search" }], VPetChatProcess.Localization)
        { ReadOnly = true, Execute = (_, args) => new HostReceipt { Status = "observed", Facts = string.Join(", ", MW.Core.Graph!.GraphsList.Keys.Where(x => x.Contains(args.GetValueOrDefault("search", ""), StringComparison.OrdinalIgnoreCase))) } });
        tools.Add(new ToolUse("play_animation", "从完整宿主动画目录选择一个当前可播放的动作。", ["动画", "动作", "表演", "跳舞"], [],
            [new ToolUse.Arg { Name = "name", IsRequired = true }], VPetChatProcess.Localization)
        {
            Execute = (turn, args) =>
            {
                if (turn.Receipts.Any(x => x.Tool == "play_animation" && x.Status == "applied")) return new HostReceipt { Status = "skipped", Facts = "本轮已经播放过动作。" };
                string name = args.GetValueOrDefault("name", "");
                var graph = MW.Core.Graph!.FindGraph(name, AnimatType.Single, MW.Core.Save!.Mode)
                    ?? MW.Core.Graph!.FindGraph(name, AnimatType.A_Start, MW.Core.Save!.Mode);
                if (graph == null) return new HostReceipt { Status = "skipped", Facts = "当前没有可播放的匹配动作。" };
                MW.Main.Display(graph, MW.Main.DisplayNomal);
                return new HostReceipt { Status = "applied", Facts = "已播放动作 " + name };
            }
        });
        foreach (var tool in tools.Where(x => !x.ReadOnly))
        {
            var execute = tool.Execute!;
            tool.Execute = (turn, args) =>
            {
                if (turn.SaveId != GetSaveId()) return new HostReceipt { Status = "skipped", Facts = "已切换存档，未执行。" };
                var saveData = MW.GameSavesData;
                HostReceipt result;
                try { result = execute(turn, args); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                { result = new HostReceipt { Status = "unknown", Facts = "宿主操作中断，实际结果需要核对。" }; }
                if (!ReferenceEquals(saveData, MW.GameSavesData) || turn.SaveId != GetSaveId())
                    return new HostReceipt { Status = "unknown", Facts = result.Facts + " 操作期间存档已切换，保存结果需要核对。" };
                var pending = turn.Receipts.Last();
                result.SaveId = turn.SaveId; result.TurnId = turn.TurnId; result.StepId = pending.StepId;
                result.Tool = tool.Code; result.Arguments = args;
                // The latest committed step is enough: Process checkpoints each result before starting another step.
                MW.GameSavesData.Data["chatvpet_process"][(gstr)"last_receipt"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                try { MW.Save(); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    result.Status = "unknown";
                    result.Facts += " 宿主保存未确认；不要重复执行，重新加载后核对。";
                    MW.GameSavesData.Data["chatvpet_process"][(gstr)"last_receipt"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                }
                return result;
            };
        }
    }

    private bool IsToolAllowed(ChatTurn turn, ToolUse tool)
    {
        if (turn.SaveId != GetSaveId()) return false;
        if (tool.Code == "plan_action") return turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
        if (tool.ReadOnly) return turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0 || turn.Initial.Has("animation");
        if (tool.Code == "adjust_state") return turn.Initial.Has("mood") || turn.Initial.Has("affinity");
        if (tool.Code == "play_animation") return turn.Initial.Has("animation");
        if (!turn.Initial.Has("delegate") && turn.Initial.Unknown.Count == 0) return false;
        return turn.Actions.Any(x => x.Tool == tool.Code);
    }

    private HostReceipt ReadState(Dictionary<string, string> args)
    {
        string field = args.GetValueOrDefault("field", "");
        var save = MW.Core.Save!;
        if (!MW.Set.EnableFunction && field is not ("activity" or "inventory")) return new HostReceipt { Status = "skipped", Facts = "未开启养成，无法提供该读数。" };
        string? facts = field switch
        {
            "money" => $"余额:{save.Money:F2}", "strength" => $"体力:{save.Strength:F1}/{save.StrengthMax:F1}",
            "feeling" => $"心情:{save.Feeling:F1}/{save.FeelingMax:F1}", "food" => $"饱腹:{save.StrengthFood:F1}/{save.StrengthMax:F1}",
            "drink" => $"口渴:{save.StrengthDrink:F1}/{save.StrengthMax:F1}", "level" => $"等级:{save.Level}",
            "activity" => $"当前活动:{MW.Main.State}, {MW.Main.NowWork?.NameTrans}", "inventory" => string.Join("; ", MW.Items.Select(x => $"{x.TranslateName} (id:{x.Name}), 数量:{x.Count}")), _ => null
        };
        return new HostReceipt { Status = facts == null ? "skipped" : "observed", Facts = facts ?? "没有这个公开读数。" };
    }

    private HostReceipt BuyItem(ChatTurn turn, Dictionary<string, string> args)
    {
        var request = turn.PendingAction("buy_item", args.GetValueOrDefault("request_id", ""));
        if (request == null) return new HostReceipt { Status = "skipped", Facts = "没有可执行的购买要求。" };
        if (turn.Receipts.Take(turn.Receipts.Count - 1).Any(x => x.Tool == "buy_item" && x.Status == "unknown"))
            return new HostReceipt { Status = "skipped", Facts = "之前的购买尚未确认，先核对结果再决定其他购买。" };
        var item = MW.Foods.Find(x => x.Name == request.Target);
        if (item == null) return new HostReceipt { Status = "skipped", Facts = "商品已不在当前目录中。" };
        int quantity = request.Quantity;
        if ((long)(MW.Items.FirstOrDefault(x => x.Name == item.Name)?.Count ?? 0) + quantity > int.MaxValue)
            return new HostReceipt { Status = "skipped", Facts = "购买数量超过背包可保存的范围。" };
        if (!MW.Set.EnableFunction || MW.HashCheck && item.IsOverLoad()) return new HostReceipt { Status = "skipped", Facts = "当前游戏规则不允许购买该商品。" };
        double price = item.Price, total = price * quantity;
        string error = request.CheckPurchase(price, MW.Core.Save!.Money, turn.Receipts.Sum(x => x.MoneySpent ?? 0));
        if (error.Length > 0) return new HostReceipt { Status = "skipped", Facts = $"{error} 执行时单价:{price:F2}, 数量:{quantity}, 当前总价:{total:F2}, 余额:{MW.Core.Save!.Money:F2}" };
        var owned = item.Clone(); // Inventory owns its quantity; never modify the catalog item.
        owned.Count = quantity;
        MW.Core.Save!.Money -= total;
        MW.ItemsAdd(owned);
        return new HostReceipt { Status = "applied", MoneySpent = total, Facts = $"已购买 {item.TranslateName} {quantity} 个，实付 {total:F2} 金币，已入背包，尚未食用。余额:{MW.Core.Save!.Money:F2}" };
    }

    private HostReceipt EatItem(ChatTurn turn, Dictionary<string, string> args)
    {
        var request = turn.PendingAction("eat_item", args.GetValueOrDefault("request_id", ""));
        if (request == null) return new HostReceipt { Status = "skipped", Facts = "没有可执行的食用要求。" };
        int quantity = request.Quantity;
        var item = MW.Items.OfType<Food>().FirstOrDefault(x => x.Name == request.Target);
        if (item == null || quantity > item.Count)
            return new HostReceipt { Status = "skipped", Facts = "背包中没有足够的指定物品。" };
        if (!MW.Set.EnableFunction || MW.HashCheck && item.IsOverLoad()) return new HostReceipt { Status = "skipped", Facts = "当前游戏规则不允许食用。" };
        int applied = 0;
        bool effectsStarted = false;
        try
        {
            for (int i = 0; i < quantity; i++)
            {
                effectsStarted = true;
                MW.TakeItem(item);
                item.Consume(MW);
                applied++;
                effectsStarted = false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new HostReceipt { Status = "unknown", Facts = $"已完成食用 {item.TranslateName} {applied} 个；" +
                (effectsStarted ? "当前一个的效果尚未确认；" : "") + "保存状态需要核对，其余没有继续执行。" };
        }
        string presentation = "";
        try { MW.Dispatcher.Invoke(() => { MW.TakeItemHandle(item, quantity, "chatvpet"); MW.DisplayFoodAnimation(item.GetGraph(), item.ImageSource); }); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { presentation = "展示动画失败，食用效果已完成。"; }
        return new HostReceipt { Status = "applied", Facts = $"已食用 {item.TranslateName} {quantity} 个。" + presentation };
    }

    private HostReceipt AdjustState(ChatTurn turn, Dictionary<string, string> args)
    {
        if (!MW.Set.EnableFunction) return new HostReceipt { Status = "skipped", Facts = "未开启养成。" };
        var save = MW.Core.Save!;
        var facts = new List<string>();
        if (turn.Initial.Has("affinity") && args.TryGetValue("affinity_strength", out var text))
        {
            if (!int.TryParse(text, out int strength) || strength < -100 || strength > 100 || !double.IsFinite(save.LikabilityMax)
                || save.LikabilityMax <= 0 || !double.IsFinite(save.Likability) || save.Likability < 0 || save.Likability > save.LikabilityMax)
                return new HostReceipt { Status = "skipped", Facts = "好感强度或当前存档读数无效，未调整。" };
            double before = save.Likability;
            double target = ChatProtocol.AffinityTarget(before, save.LikabilityMax, strength);
            if (target != before) save.Likability = target;
            facts.Add($"好感实际变化:{save.Likability - before}");
        }
        if (turn.Initial.Has("mood") && args.TryGetValue("feeling", out text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || value < -20 || value > 20)
                facts.Add("心情参数无效，未调整心情。");
            else
            {
                double before = save.Feeling;
                save.Feeling = Math.Clamp(before + value, 0, save.FeelingMax);
                facts.Add($"心情实际变化:{save.Feeling - before}");
            }
        }
        return new HostReceipt { Status = "applied", Facts = facts.Count == 0 ? "状态维持不变。" : string.Join("; ", facts) };
    }

    private HostReceipt ObserveScreenshot(ChatTurn turn)
    {
        if (OpenAIConfig?.ScreenshotEnabled != true || toolApi == null) return new HostReceipt { Status = "skipped", Facts = "未启用截图或未配置视觉操作 API。" };
        byte[] bytes;
        if (OpenAIConfig.ScreenshotScope == "screen")
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); bytes = stream.ToArray();
        }
        else
        {
            bytes = MW.Dispatcher.Invoke(() =>
            {
                var target = MW.PetGrid;
                var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(target);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
            });
        }
        string observation = toolApi.Observe(turn.Original, bytes, "image/png");
        return new HostReceipt { Status = "observed", Facts = observation };
    }

    private HostReceipt FinishQuit(ChatTurn turn)
    {
        if (OpenAIConfig?.AllowQuit != true) return new HostReceipt { Status = "skipped", Facts = "玩家未允许桌宠自行退出。" };
        if (TalkAPI?.LastSpeechCompleted != true) return new HostReceipt { Status = "failed", Facts = "本句尚未确认播报结束，保留会话。" };
        if (turn.SaveId != GetSaveId()) return new HostReceipt { Status = "skipped", Facts = "已切换存档。" };
        MW.Save();
        var window = (Window)MW;
        var closed = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs args) => closed.TrySetResult(true);
        MW.Dispatcher.Invoke(() => window.Closed += OnClosed);
        try
        {
            MW.Dispatcher.Invoke(MW.Close);
            return closed.Task.Wait(TimeSpan.FromSeconds(20))
                ? new HostReceipt { Status = "applied", Facts = "宿主已保存并关闭。" }
                : new HostReceipt { Status = "unknown", Facts = "已请求正常退出，但尚未收到关闭完成通知。" };
        }
        finally
        {
            if (!MW.Dispatcher.HasShutdownStarted) MW.Dispatcher.Invoke(() => window.Closed -= OnClosed);
        }
    }
}
