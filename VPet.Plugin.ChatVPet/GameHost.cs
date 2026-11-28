#nullable enable
using ChatVPet.ChatProcess;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;


using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.ChatVPet;

internal sealed class GameHost(IMainWindow MW, VPetChatProcess VPetChatProcess, string receiptSection)
{
    public GameSave_v2 Save { get; } = MW.GameSavesData;
    public bool IsCurrentSave => ReferenceEquals(Save, MW.GameSavesData);

    public void RegisterTools()
    {
        VPetChatProcess.RegisterGameTools();
        var tools = VPetChatProcess.Tools;
        tools.First(x => x.Code == "read_state").Execute = (_, args) => ReadState(args);
        tools.First(x => x.Code == "list_items").Execute = (_, args) =>
            {
                string search = args.GetValueOrDefault("search", "").Trim();
                var items = string.IsNullOrEmpty(search)
                    ? MW.Foods.ToList()
                    : MW.Foods.Where(x =>
                        x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.TranslateName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.Type.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                // 关键词字面未命中时交由服务端 Embedding + Rerank 做语义筛选
                if (items.Count == 0 && !string.IsNullOrEmpty(search))
                    items = MW.Foods.ToList();
                return new HostReceipt { Status = "observed", Facts = items.Count == 0 ? "未找到商品。" : string.Join("; ", items.Select((x, i) => $"{i + 1}. {x.TranslateName} (id:{x.Name}), 现价:{x.Price:F2}")), DisplayedItems = items.Select(x => x.Name).ToList() };
            };
        tools.First(x => x.Code == "buy_item").Execute = BuyItem;
        tools.First(x => x.Code == "eat_item").Execute = EatItem;
        tools.First(x => x.Code == "stop_activity").Execute = (_, _) =>
            {
                if (MW.Main.State == Main.WorkingState.Nomal) return new HostReceipt { Status = "skipped", Facts = "当前没有正在进行的活动。" };
                MW.Dispatcher.Invoke(() => MW.Main.WorkTimer.Stop(reason: WorkTimer.FinishWorkInfo.StopReason.MenualStop));
                return new HostReceipt { Status = MW.Main.State == Main.WorkingState.Nomal ? "applied" : "unknown", Facts = "停止后当前活动: " + MW.Main.State };
            };
        tools.First(x => x.Code == "list_activities").Execute = (_, args) =>
            {
                string search = args.GetValueOrDefault("search", "").Trim();
                var allWorks = MW.Core.Graph!.GraphConfig.Works;
                int level = MW.Core.Save!.Level;
                bool enableFunction = MW.Core.Controller?.EnableFunction ?? MW.Set.EnableFunction;
                static string WorkTypeAlias(GraphHelper.Work.WorkType t) => t switch
                {
                    GraphHelper.Work.WorkType.Work => "工作 打工 work job",
                    GraphHelper.Work.WorkType.Study => "学习 上课 study",
                    GraphHelper.Work.WorkType.Play => "玩耍 娱乐 play",
                    _ => t.ToString()
                };
                var matched = string.IsNullOrEmpty(search)
                    ? allWorks.ToList()
                    : allWorks.Where(x =>
                        x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.NameTrans.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || WorkTypeAlias(x.Type).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count == 0 && !string.IsNullOrEmpty(search))
                    matched = allWorks.ToList();
                return new HostReceipt
                {
                    Status = "observed",
                    Facts = matched.Count == 0
                        ? "未找到活动。"
                        : string.Join("; ", matched.Select(x =>
                        {
                            string lockNote = enableFunction && level < x.LevelLimit ? $"，需要{x.LevelLimit}级(未解锁)" : x.LevelLimit > 0 ? $"，需要{x.LevelLimit}级" : "";
                            return $"{x.NameTrans} (id:{x.Name}), {x.Type}, {x.Time}分钟{lockNote}";
                        }))
                };
            };
        tools.First(x => x.Code == "start_activity").Execute = (_, args) =>
            {
                string target = args.GetValueOrDefault("target", "");
                var work = MW.Core.Graph!.GraphConfig.Works.Find(x => x.Name == target);
                if (work == null) return new HostReceipt { Status = "skipped", Facts = "没有这项活动。" };
                var save = MW.Core.Save!;
                bool enableFunction = MW.Core.Controller?.EnableFunction ?? MW.Set.EnableFunction;
                if (enableFunction && save.Mode == IGameSave.ModeType.Ill)
                    return new HostReceipt { Status = "skipped", Facts = $"桌宠当前生病了，无法开始 {work.NameTrans}。" };
                // 与 VPet 的 Work.GetMaxMultiplier 保持一致：未解锁返回 0，否则按当前等级限制最高倍率，避免触发等级不足弹窗
                int maxMultiplier = enableFunction && save.Level < work.LevelLimit
                    ? 0
                    : (int)Math.Max(1, Math.Min(4000, save.Level) / (Math.Max(0L, work.LevelLimit) + 10));
                if (maxMultiplier == 0)
                    return new HostReceipt { Status = "skipped", Facts = $"当前等级不足（当前 {save.Level} 级，需要 {work.LevelLimit} 级），无法开始 {work.NameTrans}。" };
                if (MW.Main.State == Main.WorkingState.Work && MW.Main.NowWork?.Name == work.Name)
                    return new HostReceipt { Status = "skipped", Facts = $"当前已经在做 {work.NameTrans} 了。" };

                int multiplier = Math.Clamp(MW.Set["workmenu"].GetInt("double_" + work.Name, 1), 1, maxMultiplier);
                var actualWork = work.Double(multiplier);
                if (ReferenceEquals(actualWork, work))
                    actualWork = (GraphHelper.Work)work.Clone();
                if (!MW.Set["gameconfig"].GetBool("noAutoCal") && actualWork.IsOverLoad())
                    actualWork.FixOverLoad();
                if (enableFunction && save.Level < actualWork.LevelLimit)
                    return new HostReceipt { Status = "skipped", Facts = $"当前等级不足（当前 {save.Level} 级，需要 {actualWork.LevelLimit} 级），无法开始 {work.NameTrans}。" };
                if (MW.HashCheck && actualWork.IsOverLoad())
                    return new HostReceipt { Status = "skipped", Facts = $"{work.NameTrans} 数据超模，当前游戏规则不允许开始。" };

                bool started = MW.Dispatcher.Invoke(() => MW.Main.StartWork(actualWork));
                return new HostReceipt { Status = started ? "applied" : "skipped", Facts = started ? "已开始 " + work.NameTrans : "宿主当前不允许开始 " + work.NameTrans };
            };
        tools.First(x => x.Code == "adjust_state").Execute = AdjustState;
        tools.First(x => x.Code == "search_animations").Execute = (_, args) =>
            {
                string search = args.GetValueOrDefault("search", "").Trim();
                var allKeys = MW.Core.Graph!.GraphsList.Keys;
                var matched = string.IsNullOrEmpty(search)
                    ? allKeys.ToList()
                    : allKeys.Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count == 0 && !string.IsNullOrEmpty(search))
                    matched = allKeys.ToList();
                return new HostReceipt { Status = "observed", Facts = matched.Count == 0 ? "未找到动画。" : string.Join(", ", matched) };
            };
        tools.First(x => x.Code == "play_animation").Execute = (turn, args) =>
            {
                if (turn.Receipts.Any(x => x.Tool == "play_animation" && x.Status == "applied")) return new HostReceipt { Status = "skipped", Facts = "本轮已经播放过动作。" };
                string name = args.GetValueOrDefault("name", "");
                var graph = MW.Core.Graph!.FindGraph(name, AnimatType.Single, MW.Core.Save!.Mode)
                    ?? MW.Core.Graph!.FindGraph(name, AnimatType.A_Start, MW.Core.Save!.Mode);
                if (graph == null) return new HostReceipt { Status = "skipped", Facts = "当前没有可播放的匹配动作。" };
                MW.Main.Display(graph, MW.Main.DisplayNomal);
                return new HostReceipt { Status = "applied", Facts = "已播放动作 " + name };
            };
        foreach (var tool in tools.Where(x => !x.ReadOnly))
        {
            var execute = tool.Execute!;
            tool.Execute = (turn, args) =>
            {
                if (!IsCurrentSave) return new HostReceipt { Status = "skipped", Facts = "已切换存档，未执行。" };
                HostReceipt result;
                try { result = execute(turn, args); }
                catch
                { result = new HostReceipt { Status = "unknown", Facts = "宿主操作中断，实际结果需要核对。" }; }
                if (!IsCurrentSave)
                    return new HostReceipt { Status = "unknown", Facts = result.Facts + " 操作期间存档已切换，保存结果需要核对。" };
                var pending = turn.Receipts.Last();
                result.TurnId = turn.TurnId; result.StepId = pending.StepId;
                result.Tool = tool.Code; result.Arguments = args;
                // The latest committed step is enough: Process checkpoints each result before starting another step.
                Save.Data[receiptSection][(gstr)"last_receipt"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                try { MW.Save(); }
                catch
                {
                    result.Status = "unknown";
                    result.Facts += " 宿主保存未确认；不要重复执行，重新加载后核对。";
                    Save.Data[receiptSection][(gstr)"last_receipt"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                }
                return result;
            };
        }
    }

    public bool IsToolAllowed(ChatTurn turn, ToolUse tool)
    {
        if (!IsCurrentSave) return false;
        return global::ChatVPet.ChatProcess.VPetChatProcess.GameToolAllowed(turn, tool);
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
        if (!RequestedAction.TryParse(args, out var request, out var parseError))
            return new HostReceipt { Status = "skipped", Facts = parseError };
        string targetError = request.CheckTarget(request.Target, turn.OriginalDisplayedItems);
        if (targetError.Length > 0) return new HostReceipt { Status = "skipped", Facts = targetError };
        if (turn.Receipts.Take(turn.Receipts.Count - 1).Any(x => x.Tool == "buy_item" && x.Status == "unknown"))
            return new HostReceipt { Status = "skipped", Facts = "之前的购买尚未确认，先核对结果再决定其他购买。" };
        var item = MW.Foods.Find(x => x.Name == request.Target);
        if (item == null) return new HostReceipt { Status = "skipped", Facts = "商品已不在当前目录中。" };
        int quantity = request.Quantity;
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
        if (!RequestedAction.TryParse(args, out var request, out var parseError))
            return new HostReceipt { Status = "skipped", Facts = parseError };
        string targetError = request.CheckTarget(request.Target, turn.OriginalDisplayedItems);
        if (targetError.Length > 0) return new HostReceipt { Status = "skipped", Facts = targetError };
        int quantity = request.Quantity;
        var item = MW.Items.OfType<Food>().FirstOrDefault(x => x.Name == request.Target);
        if (item == null || quantity > item.Count)
            return new HostReceipt { Status = "skipped", Facts = "背包中没有足够的指定物品。" };
        if (!MW.Set.EnableFunction || MW.HashCheck && item.IsOverLoad()) return new HostReceipt { Status = "skipped", Facts = "当前游戏规则不允许食用。" };
        int applied = 0;
        try
        {
            for (int i = 0; i < quantity; i++)
            {
                MW.TakeItem(item);
                item.Consume(MW);
                applied++;
            }
        }
        catch
        {
            return new HostReceipt { Status = "unknown", Facts = $"已完成食用 {item.TranslateName} {applied} 个；当前一个的效果尚未确认；保存状态需要核对，其余没有继续执行。" };
        }
        string presentation = "";
        try { MW.Dispatcher.Invoke(() => { MW.TakeItemHandle(item, quantity, "chatvpet"); MW.DisplayFoodAnimation(item.GetGraph(), item.ImageSource); }); }
        catch { presentation = "展示动画失败，食用效果已完成。"; }
        return new HostReceipt { Status = "applied", Facts = $"已食用 {item.TranslateName} {quantity} 个。" + presentation };
    }

    private HostReceipt AdjustState(ChatTurn turn, Dictionary<string, string> args)
    {
        if (!MW.Set.EnableFunction) return new HostReceipt { Status = "skipped", Facts = "未开启养成。" };
        var save = MW.Core.Save!;
        var facts = new List<string>();
        if (turn.Initial.Has("affinity") && args.TryGetValue("affinity_strength", out var text))
        {
            if (!int.TryParse(text, out int strength) || strength < -100 || strength > 100 || save.LikabilityMax <= 0)
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

}
