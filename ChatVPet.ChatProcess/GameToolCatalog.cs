namespace ChatVPet.ChatProcess;

public partial class VPetChatProcess
{
    public static bool GameToolAllowed(ChatTurn turn, ToolUse tool)
    {
        if (tool.Code == "adjust_state") return turn.Initial.Has("mood") || turn.Initial.Has("affinity");
        if (tool.Code == "play_animation") return turn.Initial.Has("animation");
        if (tool.Code == "search_animations") return turn.Initial.Has("animation") || turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
        return turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
    }

    public void RegisterGameTools()
    {
        Tools = [
            new ToolUse("read_state", "查询余额、体力、心情、饱腹、口渴、等级、当前活动或背包。", ["钱", "余额", "体力", "背包", "心情", "当前活动"], [],
            [new ToolUse.Arg { Name = "field", Description = "money/strength/feeling/food/drink/level/activity/inventory", IsRequired = true }], Localization) { ReadOnly = true },
            new ToolUse("list_items", "查询商品目录及当前售价，按返回顺序展示。", ["商品", "面包", "价格", "够买", "买", "吃", "喝"], [],
            [new ToolUse.Arg { Name = "search", Description = "商品名或描述关键词；空字符串查看全部" }], Localization) { ReadOnly = true },
            new ToolUse("buy_item", "按玩家本轮明确要求立即购买商品入包（不自动食用）；执行前宿主复核数量、现价、余额与全部预算条件。", ["买", "购买", "面包", "预算"], [],
            [new() { Name = "target", Description = "目录中的准确商品 id", IsRequired = true },
             new() { Name = "quantity", Type = "integer", Description = "购买数量，未指定时为 1", IsRequired = true },
             new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个（从 1 开始）；无序号时省略" },
             new() { Name = "max_total", Type = "number", Description = "本轮所有购买合计预算；无限制时省略" },
             new() { Name = "max_unit", Type = "number", Description = "允许的最高单价；无限制时省略" },
             new() { Name = "min_remaining", Type = "number", Description = "购买后至少保留的余额；无限制时省略" },
             new() { Name = "exclusive_total", Type = "boolean", Description = "合计须严格低于 max_total 时传 true" },
             new() { Name = "exclusive_unit", Type = "boolean", Description = "单价须严格低于 max_unit 时传 true" }], Localization),
            new ToolUse("eat_item", "食用背包中已有的指定商品。", ["吃", "喝", "食用", "面包"], [],
            [new() { Name = "target", Description = "背包中的准确商品 id", IsRequired = true },
             new() { Name = "quantity", Type = "integer", Description = "食用数量，未指定时为 1", IsRequired = true },
             new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个（从 1 开始）；无序号时省略" }], Localization),
            new ToolUse("stop_activity", "停止正在进行的工作、学习或玩耍。", ["停止", "结束", "工作", "别干", "休息"], ["停一下工作"], [], Localization),
            new ToolUse("list_activities", "查询宿主活动目录（含 MOD 工作、学习与玩耍）。", ["工作", "学习", "活动", "玩耍", "文案"], [],
            [new ToolUse.Arg { Name = "search" }], Localization) { ReadOnly = true },
            new ToolUse("start_activity", "开始宿主目录中存在且当前允许的活动。", ["工作", "学习", "开始", "活动", "去", "做"], [],
            [new ToolUse.Arg { Name = "target", Description = "活动目录中的准确 id", IsRequired = true }], Localization),
            new ToolUse("adjust_state", "按本轮 mood/affinity 抬头调整一次心情或好感；无变化时可不调整。", ["心情", "好感", "mood", "affinity"], [],
            [new ToolUse.Arg { Name = "feeling", Type = "number", Description = "心情改变量，-20 到 20" }, new ToolUse.Arg { Name = "affinity_strength", Type = "integer", Description = "好感强度，-100 到 100" }], Localization),
            new ToolUse("search_animations", "查询已注册的宿主动画名（含皮肤和 MOD）。", ["动画", "动作", "表演", "跳舞"], [],
            [new ToolUse.Arg { Name = "search" }], Localization) { ReadOnly = true },
            new ToolUse("play_animation", "从宿主动画目录播放一个当前可用动作。", ["动画", "动作", "表演", "跳舞"], [],
            [new ToolUse.Arg { Name = "name", IsRequired = true }], Localization),
            new ToolUse("web_search", "联网搜索实时资讯、天气、百科或最新公开信息。", ["搜索", "联网", "查一下", "天气", "新闻", "最新", "资料"], ["帮我查一下今天的新闻", "联网搜索一下"],
            [new ToolUse.Arg { Name = "query", Description = "简洁准确的搜索关键词", IsRequired = true },
             new ToolUse.Arg { Name = "count", Type = "integer", Description = "返回条数，默认 5，范围 1 到 10" }], Localization)
            {
                ReadOnly = true,
                Execute = RunWebSearch
            }
        ];
    }
}
