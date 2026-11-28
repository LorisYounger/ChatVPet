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
            new ToolUse("read_state",
                "查询桌宠当前状态：余额、金币、体力、心情、饱腹度、口渴度、等级、当前正在进行的活动或背包物品。",
                ["钱", "余额", "金币", "体力", "能量", "背包", "心情", "饱腹", "口渴", "等级", "当前活动", "状态", "money", "balance", "strength", "feeling", "mood", "food", "drink", "level", "activity", "inventory", "status"],
                ["Check pet status: money, balance, strength, energy, mood, feeling, hunger, thirst, level, current activity, inventory."],
                [new ToolUse.Arg { Name = "field", Description = "money/strength/feeling/food/drink/level/activity/inventory", IsRequired = true }],
                Localization) { ReadOnly = true },

            new ToolUse("list_items",
                "查询商店商品目录：查找食物、零食、饮料、牛排、面包、蛋糕、咖啡、药品、礼品的当前售价与列表。",
                ["商品", "商店", "食物", "零食", "饮料", "面包", "牛排", "咖啡", "药品", "礼物", "价格", "多少钱", "够买", "买", "吃", "喝", "shop", "item", "food", "drink", "snack", "price", "catalog", "list"],
                ["List shop catalog: search food, snacks, drinks, meals, medicine, gifts and current prices."],
                [new ToolUse.Arg { Name = "search", Description = "商品名、分类或描述关键词；空字符串按相关性返回推荐商品" }],
                Localization) { ReadOnly = true },

            new ToolUse("buy_item",
                "购买商品：按玩家本轮明确要求买食物、零食、饮料、牛排、面包或礼物放入背包（不自动食用）；执行前宿主复核数量、现价、余额与全部预算条件。",
                ["买", "购买", "下单", "购物", "食物", "零食", "饮料", "面包", "牛排", "礼物", "预算", "buy", "purchase", "order", "shop", "item", "food"],
                ["Buy item: purchase food, drinks, snacks, meals, or gifts from the shop into inventory."],
                [new() { Name = "target", Description = "目录中的准确商品 id", IsRequired = true },
                 new() { Name = "quantity", Type = "integer", Description = "购买数量，未指定时为 1", IsRequired = true },
                 new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个（从 1 开始）；无序号时省略" },
                 new() { Name = "max_total", Type = "number", Description = "本轮所有购买合计预算；无限制时省略" },
                 new() { Name = "max_unit", Type = "number", Description = "允许的最高单价；无限制时省略" },
                 new() { Name = "min_remaining", Type = "number", Description = "购买后至少保留的余额；无限制时省略" },
                 new() { Name = "exclusive_total", Type = "boolean", Description = "合计须严格低于 max_total 时传 true" },
                 new() { Name = "exclusive_unit", Type = "boolean", Description = "单价须严格低于 max_unit 时传 true" }],
                Localization),

            new ToolUse("eat_item",
                "吃东西、喝饮料：食用或饮用背包中已有的食物、零食或饮料。",
                ["吃", "喝", "食用", "饮用", "面包", "零食", "饮料", "背包", "eat", "drink", "consume", "feed", "food"],
                ["Eat or drink item: consume food or drinks already in inventory."],
                [new() { Name = "target", Description = "背包中的准确商品 id", IsRequired = true },
                 new() { Name = "quantity", Type = "integer", Description = "食用数量，未指定时为 1", IsRequired = true },
                 new() { Name = "display_index", Type = "integer", Description = "玩家指原展示列表第几个（从 1 开始）；无序号时省略" }],
                Localization),

            new ToolUse("stop_activity",
                "停止活动：停止或结束桌宠正在进行的工作、打工、学习、玩耍，停下来休息。",
                ["停止", "结束", "取消", "工作", "打工", "学习", "玩耍", "别干", "休息", "stop", "cancel", "end", "work", "study", "play", "rest"],
                ["Stop activity: stop or cancel ongoing work, job, study, or play to take a rest."],
                [],
                Localization),

            new ToolUse("list_activities",
                "查询活动目录：查看可以做的工作、打工、学习、课程、玩耍、娱乐等活动列表（含 MOD 工作、学习与玩耍）。",
                ["工作", "打工", "赚钱", "学习", "上课", "课程", "活动", "玩耍", "娱乐", "文案", "work", "job", "study", "play", "activity", "task", "list"],
                ["List activities: view available work, jobs, study, courses, and play activities."],
                [new ToolUse.Arg { Name = "search", Description = "活动名或类型关键词（如 工作/学习/玩耍）；空字符串返回匹配活动" }],
                Localization) { ReadOnly = true },

            new ToolUse("start_activity",
                "开始活动：让桌宠去工作、打工、赚钱、学习、上课、玩耍或开始执行宿主目录中的某项活动任务。",
                ["工作", "打工", "赚钱", "学习", "上课", "玩耍", "开始", "开工", "活动", "任务", "去", "做", "start", "begin", "work", "job", "study", "play", "activity"],
                ["Start activity: go to work, start a job, study, play, or begin an activity."],
                [new ToolUse.Arg { Name = "target", Description = "活动目录中的准确 id", IsRequired = true }],
                Localization),

            new ToolUse("adjust_state",
                "调整心情或好感度：按本轮 mood/affinity 抬头调整一次桌宠的心情值或好感等级；无变化时可不调整。",
                ["心情", "好感", "好感度", "情绪", "mood", "affinity", "feeling", "likability"],
                ["Adjust state: change pet mood/feeling or affinity/likability."],
                [new ToolUse.Arg { Name = "feeling", Type = "number", Description = "心情改变量，-20 到 20" },
                 new ToolUse.Arg { Name = "affinity_strength", Type = "integer", Description = "好感强度，-100 到 100" }],
                Localization),

            new ToolUse("search_animations",
                "搜索动画列表：查询已注册的桌宠动作、动画、表演、跳舞、表情名称（含皮肤和 MOD）。",
                ["动画", "动作", "表演", "跳舞", "卖萌", "表情", "animation", "action", "dance", "emote", "search"],
                ["Search animations: list available pet animations, actions, emotes, and dances."],
                [new ToolUse.Arg { Name = "search", Description = "动画名关键词" }],
                Localization) { ReadOnly = true },

            new ToolUse("play_animation",
                "播放动画动作：让桌宠播放一个当前可用的动画、动作、表演、跳舞或卖萌。",
                ["动画", "动作", "表演", "跳舞", "卖萌", "挥手", "转圈", "play", "animation", "action", "dance", "emote"],
                ["Play animation: perform a pet animation, action, dance, or emote."],
                [new ToolUse.Arg { Name = "name", Description = "动画名称", IsRequired = true }],
                Localization),

            new ToolUse("web_search",
                "联网搜索：上网查询现实世界的实时新闻、天气、百科、攻略、最新资讯与公开资料。",
                ["搜索", "联网", "上网", "查一下", "天气", "新闻", "百科", "最新", "资料", "search", "web", "internet", "weather", "news", "online"],
                ["Web search: search the internet for real-time news, weather, facts, and latest information."],
                [new ToolUse.Arg { Name = "query", Description = "简洁准确的搜索关键词", IsRequired = true },
                 new ToolUse.Arg { Name = "count", Type = "integer", Description = "返回条数，默认 5，范围 1 到 10" }],
                Localization)
            {
                ReadOnly = true,
                Execute = RunWebSearch
            }
        ];
    }
}
