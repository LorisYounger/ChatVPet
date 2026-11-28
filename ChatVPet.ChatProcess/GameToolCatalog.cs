namespace ChatVPet.ChatProcess;

public partial class VPetChatProcess
{
    public static bool GameToolAllowed(ChatTurn turn, ToolUse tool)
    {
        if (tool.Code is "read_file" or "write_file") return turn.Initial.Has("delegate");
        if (tool.Code == "adjust_state") return turn.Initial.Has("mood") || turn.Initial.Has("affinity");
        if (tool.Code == "play_animation") return turn.Initial.Has("animation");
        if (tool.Code == "search_animations") return turn.Initial.Has("animation") || turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
        return turn.Initial.Has("delegate") || turn.Initial.Unknown.Count > 0;
    }

    private string PickLang(string hans, string hant, string en) => ChatProtocol.NormalizeLanguage(Language) switch
    {
        ChatProtocol.TraditionalChinese => hant,
        ChatProtocol.English => en,
        _ => hans
    };

    private string[] MultilingualEmbeddings(string hans, string hant, string en) => ChatProtocol.NormalizeLanguage(Language) switch
    {
        ChatProtocol.TraditionalChinese => [hans, en],
        ChatProtocol.English => [hans, hant],
        _ => [hant, en]
    };

    public void RegisterGameTools()
    {
        const string readStateHans = "查询桌宠当前状态：余额、金币、体力、心情、饱腹度、口渴度、等级、当前正在进行的活动或背包物品。";
        const string readStateHant = "查詢桌寵目前狀態：餘額、金幣、體力、心情、飽腹度、口渴度、等級、目前進行中的活動或背包物品。";
        const string readStateEn = "Check pet status: money, balance, coins, strength, stamina, energy, mood, feeling, hunger, thirst, level, current activity, or inventory items.";

        const string listItemsHans = "查询商店商品目录：查找食物、零食、饮料、牛排、面包、蛋糕、咖啡、药品、礼品的当前售价与列表。";
        const string listItemsHant = "查詢商店商品目錄：尋找食物、零食、飲料、牛排、麵包、蛋糕、咖啡、藥品、禮品的目前售價與清單。";
        const string listItemsEn = "List shop catalog: search food, snacks, drinks, meals, medicine, gifts, items, and current prices.";

        const string buyItemHans = "购买商品：按玩家本轮明确要求买食物、零食、饮料、牛排、面包或礼物放入背包（不自动食用）；执行前宿主复核数量、现价、余额与全部预算条件。";
        const string buyItemHant = "購買商品：依玩家本輪明確要求買食物、零食、飲料、牛排、麵包或禮物放入背包（不自動食用）；執行前宿主複核數量、現價、餘額與全部預算條件。";
        const string buyItemEn = "Buy item: purchase food, drinks, snacks, meals, medicine, or gifts from the shop into inventory within budget and quantity limits (does not eat automatically).";

        const string eatItemHans = "吃东西、喝饮料：食用或饮用背包中已有的食物、零食或饮料。";
        const string eatItemHant = "吃東西、喝飲料：食用或飲用背包中已有的食物、零食或飲料。";
        const string eatItemEn = "Eat or drink item: consume food, snacks, or drinks already in inventory.";

        const string stopActHans = "停止活动：停止或结束桌宠正在进行的工作、打工、学习、玩耍，停下来休息。";
        const string stopActHant = "停止活動：停止或結束桌寵正在進行的工作、打工、學習、玩耍，停下來休息。";
        const string stopActEn = "Stop activity: stop or cancel ongoing work, job, study, or play to take a rest.";

        const string listActHans = "查询活动目录：查看可以做的工作、打工、学习、课程、玩耍、娱乐等活动列表（含 MOD 工作、学习与玩耍）。";
        const string listActHant = "查詢活動目錄：查看可以做的工作、打工、學習、課程、玩耍、娛樂等活動清單（含 MOD 工作、學習與玩耍）。";
        const string listActEn = "List activities: view available work, jobs, study, courses, and play activities (including MOD activities).";

        const string startActHans = "开始活动：让桌宠去工作、打工、赚钱、学习、上课、玩耍或开始执行宿主目录中的某项活动任务。";
        const string startActHant = "開始活動：讓桌寵去工作、打工、賺錢、學習、上課、玩耍或開始執行宿主目錄中的某項活動任務。";
        const string startActEn = "Start activity: send the pet to work, earn money, study, take a class, play, or begin a host activity.";

        const string adjustStateHans = "调整心情或好感度：按本轮 mood/affinity 抬头调整一次桌宠的心情值或好感等级；无变化时可不调整。";
        const string adjustStateHant = "調整心情或好感度：依本輪 mood/affinity 抬頭調整一次桌寵的心情值或好感等級；無變化時可不調整。";
        const string adjustStateEn = "Adjust state: update the pet's mood/feeling or affinity/likability once for this turn when feelings change.";

        const string searchAnimHans = "搜索动画列表：查询已注册的桌宠动作、动画、表演、跳舞、表情名称（含皮肤和 MOD）。";
        const string searchAnimHant = "搜尋動畫清單：查詢已註冊的桌寵動作、動畫、表演、跳舞、表情名稱（含外觀和 MOD）。";
        const string searchAnimEn = "Search animations: list available pet animations, actions, emotes, gestures, and dances (including skins and MODs).";

        const string playAnimHans = "播放动画动作：让桌宠播放一个当前可用的动画、动作、表演、跳舞或卖萌。";
        const string playAnimHant = "播放動畫動作：讓桌寵播放一個目前可用的動畫、動作、表演、跳舞或賣萌。";
        const string playAnimEn = "Play animation: perform an available pet animation, action, dance, wave, or emote.";

        const string readFileHans = "读取文本文件，默认限于玩家设置的目录。玩家开启全位置读取后可读取当前用户有权访问的任意文本文件。省略 path 可查询当前读取范围、相对路径起点和写入目录。长文件按回执中的 offset 继续读。";
        const string readFileHant = "讀取文字檔案，預設限於玩家設定的目錄。玩家開啟全位置讀取後可讀取目前使用者有權存取的任意文字檔案。省略 path 可查詢目前讀取範圍、相對路徑起點和寫入目錄。長檔案依回執中的 offset 繼續讀。";
        const string readFileEn = "Read text files in the configured folders, or any accessible text file if read-anywhere is enabled. Omit path to inspect current permissions and default folder; use offset to continue long files.";

        const string writeFileHans = "按玩家要求在允许写入的目录中创建或覆盖文本文件。局部修改或追加前先读完原文，content 要包含完整的新正文，保留其余内容。";
        const string writeFileHant = "依玩家要求在允許寫入的目錄中建立或覆寫文字檔案。局部修改或追加前先讀完原文，content 要包含完整的新正文，保留其餘內容。";
        const string writeFileEn = "Create or update a text file (notes, code, web page, document) inside the player's allowed folders. Read existing content first before editing, and pass the complete new content.";

        const string webSearchHans = "联网搜索：上网查询现实世界的实时新闻、天气、百科、攻略、最新资讯与公开资料。";
        const string webSearchHant = "聯網搜尋：上網查詢現實世界的即時新聞、天氣、百科、攻略、最新資訊與公開資料。";
        const string webSearchEn = "Web search: search the internet for real-time news, weather, encyclopedia facts, game guides, and up-to-date public information.";

        Tools = [
            new ToolUse("read_state",
                PickLang(readStateHans, readStateHant, readStateEn),
                ["钱", "余额", "金币", "体力", "能量", "背包", "心情", "饱腹", "口渴", "等级", "当前活动", "状态",
                 "錢", "餘額", "金幣", "體力", "飽腹", "等級", "目前活動", "狀態",
                 "money", "balance", "coins", "strength", "stamina", "energy", "mood", "feeling", "food", "hunger", "drink", "thirst", "level", "activity", "inventory", "status"],
                MultilingualEmbeddings(readStateHans, readStateHant, readStateEn),
                [new ToolUse.Arg { Name = "field", Description = "money/strength/feeling/food/drink/level/activity/inventory", IsRequired = true }],
                Localization) { ReadOnly = true },

            new ToolUse("list_items",
                PickLang(listItemsHans, listItemsHant, listItemsEn),
                ["商品", "商店", "食物", "零食", "饮料", "面包", "牛排", "咖啡", "药品", "礼物", "价格", "多少钱", "够买", "买", "吃", "喝",
                 "飲料", "麵包", "藥品", "禮物", "價格", "多少錢", "夠買", "買",
                 "shop", "store", "catalog", "list", "item", "food", "drink", "snack", "cake", "coffee", "medicine", "gift", "price", "cost", "afford"],
                MultilingualEmbeddings(listItemsHans, listItemsHant, listItemsEn),
                [new ToolUse.Arg { Name = "search", Description = PickLang("商品名、分类或描述关键词；空字符串按相关性返回推荐商品", "商品名、分類或描述關鍵詞；空字串依相關性回傳推薦商品", "Item name, category, or description keyword; empty returns relevant items") }],
                Localization) { ReadOnly = true },

            new ToolUse("buy_item",
                PickLang(buyItemHans, buyItemHant, buyItemEn),
                ["买", "购买", "下单", "购物", "食物", "零食", "饮料", "面包", "牛排", "礼物", "预算",
                 "買", "購買", "下單", "購物", "飲料", "麵包", "禮物", "預算",
                 "buy", "purchase", "order", "shop", "store", "item", "food", "drink", "snack", "gift", "budget"],
                MultilingualEmbeddings(buyItemHans, buyItemHant, buyItemEn),
                [new() { Name = "target", Description = PickLang("目录中的准确商品 id", "目錄中的準確商品 id", "Exact item id from the catalog"), IsRequired = true },
                 new() { Name = "quantity", Type = "integer", Description = PickLang("购买数量，未指定时为 1", "購買數量，未指定時為 1", "Quantity to buy (default 1)"), IsRequired = true },
                 new() { Name = "display_index", Type = "integer", Description = PickLang("玩家指原展示列表第几个（从 1 开始）；无序号时省略", "玩家指原展示清單第幾個（從 1 開始）；無序號時省略", "1-based index in the previously shown list; omit if unspecified") },
                 new() { Name = "max_total", Type = "number", Description = PickLang("本轮所有购买合计预算；无限制时省略", "本輪所有購買合計預算；無限制時省略", "Maximum total budget across all purchases this turn; omit if none") },
                 new() { Name = "max_unit", Type = "number", Description = PickLang("允许的最高单价；无限制时省略", "允許的最高單價；無限制時省略", "Maximum unit price allowed; omit if none") },
                 new() { Name = "min_remaining", Type = "number", Description = PickLang("购买后至少保留的余额；无限制时省略", "購買後至少保留的餘額；無限制時省略", "Minimum balance to keep after purchase; omit if none") },
                 new() { Name = "exclusive_total", Type = "boolean", Description = PickLang("合计须严格低于 max_total 时传 true", "合計須嚴格低於 max_total 時傳 true", "Set true if total must be strictly less than max_total") },
                 new() { Name = "exclusive_unit", Type = "boolean", Description = PickLang("单价须严格低于 max_unit 时传 true", "單價須嚴格低於 max_unit 時傳 true", "Set true if unit price must be strictly less than max_unit") }],
                Localization),

            new ToolUse("eat_item",
                PickLang(eatItemHans, eatItemHant, eatItemEn),
                ["吃", "喝", "食用", "饮用", "面包", "零食", "饮料", "背包",
                 "飲用", "麵包", "飲料",
                 "eat", "drink", "consume", "feed", "food", "snack", "inventory", "hungry", "thirsty"],
                MultilingualEmbeddings(eatItemHans, eatItemHant, eatItemEn),
                [new() { Name = "target", Description = PickLang("背包中的准确商品 id", "背包中的準確商品 id", "Exact item id in inventory"), IsRequired = true },
                 new() { Name = "quantity", Type = "integer", Description = PickLang("食用数量，未指定时为 1", "食用數量，未指定時為 1", "Quantity to consume (default 1)"), IsRequired = true },
                 new() { Name = "display_index", Type = "integer", Description = PickLang("玩家指原展示列表第几个（从 1 开始）；无序号时省略", "玩家指原展示清單第幾個（從 1 開始）；無序號時省略", "1-based index in the previously shown list; omit if unspecified") }],
                Localization),

            new ToolUse("stop_activity",
                PickLang(stopActHans, stopActHant, stopActEn),
                ["停止", "结束", "取消", "工作", "打工", "学习", "玩耍", "别干", "休息",
                 "結束", "學習", "別幹",
                 "stop", "cancel", "end", "quit", "work", "job", "study", "play", "rest", "break"],
                MultilingualEmbeddings(stopActHans, stopActHant, stopActEn),
                [],
                Localization),

            new ToolUse("list_activities",
                PickLang(listActHans, listActHant, listActEn),
                ["工作", "打工", "赚钱", "学习", "上课", "课程", "活动", "玩耍", "娱乐", "文案",
                 "賺錢", "學習", "上課", "課程", "活動", "娛樂",
                 "work", "job", "study", "course", "class", "play", "activity", "activities", "task", "list"],
                MultilingualEmbeddings(listActHans, listActHant, listActEn),
                [new ToolUse.Arg { Name = "search", Description = PickLang("活动名或类型关键词（如 工作/学习/玩耍）；空字符串返回匹配活动", "活動名或類型關鍵詞（如 工作/學習/玩耍）；空字串回傳符合活動", "Activity name or type keyword (e.g. work/study/play); empty returns matching activities") }],
                Localization) { ReadOnly = true },

            new ToolUse("start_activity",
                PickLang(startActHans, startActHant, startActEn),
                ["工作", "打工", "赚钱", "学习", "上课", "玩耍", "开始", "开工", "活动", "任务", "去", "做",
                 "賺錢", "學習", "上課", "開始", "開工", "活動", "任務",
                 "start", "begin", "do", "work", "job", "study", "play", "activity", "task"],
                MultilingualEmbeddings(startActHans, startActHant, startActEn),
                [new ToolUse.Arg { Name = "target", Description = PickLang("活动目录中的准确 id", "活動目錄中的準確 id", "Exact activity id from the activity catalog"), IsRequired = true }],
                Localization),

            new ToolUse("adjust_state",
                PickLang(adjustStateHans, adjustStateHant, adjustStateEn),
                ["心情", "好感", "好感度", "情绪",
                 "情緒",
                 "mood", "affinity", "feeling", "likability", "affection", "emotion"],
                MultilingualEmbeddings(adjustStateHans, adjustStateHant, adjustStateEn),
                [new ToolUse.Arg { Name = "feeling", Type = "number", Description = PickLang("心情改变量，-20 到 20", "心情改變量，-20 到 20", "Mood delta from -20 to 20") },
                 new ToolUse.Arg { Name = "affinity_strength", Type = "integer", Description = PickLang("好感强度，-100 到 100", "好感強度，-100 到 100", "Affinity change strength from -100 to 100") }],
                Localization),

            new ToolUse("search_animations",
                PickLang(searchAnimHans, searchAnimHant, searchAnimEn),
                ["动画", "动作", "表演", "跳舞", "卖萌", "表情",
                 "動畫", "動作", "賣萌",
                 "animation", "animations", "action", "dance", "emote", "gesture", "perform", "search", "list"],
                MultilingualEmbeddings(searchAnimHans, searchAnimHant, searchAnimEn),
                [new ToolUse.Arg { Name = "search", Description = PickLang("动画名关键词", "動畫名關鍵詞", "Animation name keyword") }],
                Localization) { ReadOnly = true },

            new ToolUse("play_animation",
                PickLang(playAnimHans, playAnimHant, playAnimEn),
                ["动画", "动作", "表演", "跳舞", "卖萌", "挥手", "转圈",
                 "動畫", "動作", "賣萌", "揮手", "轉圈",
                 "play", "perform", "animation", "action", "dance", "emote", "wave", "spin"],
                MultilingualEmbeddings(playAnimHans, playAnimHant, playAnimEn),
                [new ToolUse.Arg { Name = "name", Description = PickLang("动画名称", "動畫名稱", "Animation name"), IsRequired = true }],
                Localization),

            new ToolUse("read_file",
                PickLang(readFileHans, readFileHant, readFileEn),
                ["文件", "读取", "笔记", "文档", "打开", "路径", "目录",
                 "檔案", "讀取", "筆記", "文件", "開啟", "路徑", "目錄",
                 "read", "open", "view", "file", "text", "note", "document", "path", "folder", "directory"],
                MultilingualEmbeddings(readFileHans, readFileHant, readFileEn),
                [new() { Name = "path", Description = PickLang("可读文件的绝对路径，或相对于默认目录的路径", "可讀檔案的絕對路徑，或相對於預設目錄的路徑", "Absolute file path, or path relative to the default folder") },
                 new() { Name = "offset", Type = "integer", Description = PickLang("起始字符位置，默认 0", "起始字元位置，預設 0", "Starting character offset (default 0)") }], Localization) { ReadOnly = true },

            new ToolUse("write_file",
                PickLang(writeFileHans, writeFileHant, writeFileEn),
                ["文件", "写入", "保存", "生成", "笔记", "网页", "文档", "覆盖",
                 "檔案", "寫入", "儲存", "筆記", "網頁", "覆寫",
                 "write", "save", "create", "edit", "update", "file", "note", "code", "document", "folder"],
                MultilingualEmbeddings(writeFileHans, writeFileHant, writeFileEn),
                [new() { Name = "path", Description = PickLang("绝对路径，或相对于首个允许目录的文件路径", "絕對路徑，或相對於首個允許目錄的檔案路徑", "Absolute path, or path relative to the first allowed folder"), IsRequired = true },
                 new() { Name = "content", Description = PickLang("完整文件内容", "完整檔案內容", "Complete file content"), IsRequired = true }], Localization),

            new ToolUse("web_search",
                PickLang(webSearchHans, webSearchHant, webSearchEn),
                ["搜索", "联网", "上网", "查一下", "天气", "新闻", "百科", "最新", "资料",
                 "搜尋", "聯網", "上網", "天氣", "新聞", "資料",
                 "search", "web", "internet", "online", "lookup", "weather", "news", "wiki", "guide", "latest", "info"],
                MultilingualEmbeddings(webSearchHans, webSearchHant, webSearchEn),
                [new ToolUse.Arg { Name = "query", Description = PickLang("简洁准确的搜索关键词", "簡潔準確的搜尋關鍵詞", "Concise, accurate search query"), IsRequired = true },
                 new ToolUse.Arg { Name = "count", Type = "integer", Description = PickLang("返回条数，默认 5，范围 1 到 10", "回傳筆數，預設 5，範圍 1 到 10", "Number of results (default 5, range 1-10)") }],
                Localization)
            {
                ReadOnly = true,
                Execute = RunWebSearch
            }
        ];
    }

    public void RegisterReminderTool()
    {
        if (Tools.Any(tool => tool.Code == "set_reminder")) return;
        const string reminderHans = "设置定时提醒：填写提醒内容，并用延迟秒数或本地到期时间指定何时提醒玩家。";
        const string reminderHant = "設定定時提醒：填寫提醒內容，並用延遲秒數或本地到期時間指定何時提醒玩家。";
        const string reminderEn = "Schedule a reminder or alarm: specify the reminder text and either delay_seconds or local due_time to notify the user.";

        Tools.Add(new ToolUse("set_reminder",
            PickLang(reminderHans, reminderHant, reminderEn),
            ["提醒", "定时", "闹钟", "叫我", "倒计时",
             "定時", "鬧鐘", "倒數",
             "remind", "reminder", "timer", "alarm", "schedule", "notify", "countdown"],
            MultilingualEmbeddings(reminderHans, reminderHant, reminderEn),
            [new() { Name = "content", Description = PickLang("到点提醒玩家的事项", "到點提醒玩家的事項", "What to remind the user about"), IsRequired = true },
             new() { Name = "delay_seconds", Type = "integer", Description = PickLang("从现在到提醒的秒数，与 due_time 二选一", "從現在到提醒的秒數，與 due_time 二選一", "Seconds from now until reminder (either this or due_time)") },
             new() { Name = "due_time", Description = PickLang("本地到期时间，与 delay_seconds 二选一", "本地到期時間，與 delay_seconds 二選一", "Local due time (either this or delay_seconds)") }],
            Localization));
    }
}
