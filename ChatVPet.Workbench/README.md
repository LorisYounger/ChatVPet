# ChatVPet Lab

独立 WPF 测试工作台。左侧管理会话和测试，中间显示对白与宿主展示，右侧查看实际模型请求、检索候选、摘要和插件回执。界面与本机 HTTP 接口调用同一个服务。

## 启动

在仓库根目录执行：

```powershell
dotnet run --project ChatVPet.Workbench -c Release
```

也可以直接运行 `ChatVPet.Workbench/bin/Release/net8.0-windows/ChatVPet.Workbench.exe`。需要 .NET 8 Desktop Runtime。程序向上查找 `ChatVPet.sln`，发布到其他目录时传入 `--root "仓库路径"`。

默认接口为 `http://127.0.0.1:39421`，仅监听本机。`--port 39422` 可更换端口；`--headless` 只运行接口，仍保留 WPF Dispatcher 以初始化插件。不要同时让两个实例操作同一数据目录。

## 窗口里怎么测试

- **全局设置 · API 与桌宠数据** 可以选择已有 API 配置，或展开编辑区填写各模型的地址、名称和密钥。编辑后的通用 API 保存到 `Local/ChatVPetProcess.Workbench.Settings.json`。
- **新建测试会话** 会带入全局 API、MOD 目录和存档，可单独替换任意一项。清空 MOD 使用内置测试商品，清空存档使用测试员和 10 元余额。高级参数可设置检索阈值和字符预算。
- **发送消息**：Ctrl+Enter 发送；Enter 换行。可附上 PNG/JPEG，交给配置的操作/视觉 API 观察。模型不支持图片时能看到 HTTP 错误与插件返回的观察结果。
- **查看本轮** 打开该轮的测试结果。余额、背包和养成数值有变化时显示前后两格数值。执行记录列出查询、购买等操作，点开一行查看回执。测试预期由用例提供，留给测试员核对。
- **模型收发** 只列所选轮次的对话、操作、摘要和图片调用。选一次调用，在「模型输入」选择一条消息，默认选中末条；切到「模型返回」看同一次调用的回复或工具调用。离线演示单独标记。
- **更多** 可打开完整事件、上下文、对照与笔记、插件窗口和数据来源，也可导出完整记录。向量、重排、HTTP 状态和原始 JSON 可在完整事件中查询。
- **对照与笔记** 可从相同的存档建立基线和变体，比较知识与记忆检索、重排、历史摘要和提示词。
- **停止**：当前 HTTP 请求结束后停止后续步骤。不会终止服务端已经开始的生成。
- **续答重试**：调用 Process.RetryReply，已有购买不会再次执行。初答协议错误应重新发送消息。
- **人工分析**：保存预期、观察和修复建议。预设用例不会把关键词命中当成语义通过。

新建会话使用 API 配置的独立副本。插件设置窗口只修改当前会话副本，不覆盖 `Local` 中的原配置。设置、图片和全部测试存档保存在 Git 忽略的 `Local/Workbench/<会话ID>/`。导出的诊断 JSON 不包含配置密钥和认证头，包含测试对话与模型返回。`checkpoint.messages` 按时间导出有效消息，每条包含 `turnId`、`role`、`source`、`content`。回调的 role 为 `user`，来源为“宿主”。旧协议会话支持只读查看、导出。

### 用本体数据测试

1. 打开左侧「全局设置」，在「MOD 目录」选择具体的 MOD，例如 VPet 工程里的 `VPet-Simulator.Windows/mod/0000_core`。工作台读取 `food/*.lps` 商品和 `pet/*.lps` 桌宠配置。一个 MOD 内有多个角色时优先选 vup，其次按文件名选首个角色。
2. 在「VPet 存档」选择游戏 `Saves` 或 `Saves_BKP` 目录中的 `Save*.lps`。新会话通过本体的 `GameSave_v2` 和 `Item.CreateItem` 读取称呼、养成数值及背包物品。
3. 新建普通会话，在「更多 → 插件与数据来源」核对源路径、商品数与活动数，在「更多 → 完整上下文」核对当前余额和背包。例如发送“面包现在多少钱？只查价”，再到「模型收发」查看对话模型、操作模型与宿主回传的消息。基础协议测试集会为其测试题重设面包价格、余额和背包。

导入时保存数据副本，测试中的购买和食用写入独立会话。重开和对照分支使用会话内的副本。要测试源文件的新版本，可重新建会话导入。图像、动画、声音和额外 MOD 插件仍由完整游戏宿主提供。

## 测试集

`Scenarios.json` 包含：

| 测试集 | 内容 |
|---|---|
| baseline | 7 个基本场景：问候、只查价、超预算、购买入包、未来条件、引用指令、现场变价 |
| compression | 每组 36 条：24 轮事实与闲聊、8 个回忆问题、保存重开、3 个 RAG 恢复问题 |
| 插件购买自检 | 7 个确定性检查：目录、余额、登记不执行、购买入包、不自动吃、重复执行拦截、预算拦截 |

压缩对照沿用之前的少压缩 20/10 和多压缩 6/2；摘要交给 DeepSeek 操作 API，输出预算 8192。事实回忆阶段关闭知识/记忆 RAG，最后三题再打开。工具候选仍参与配置的检索与重排。完整请求按 UTF-8 字节加输出预算保守估算 40960 token 上限，这不是模型 tokenizer 的精确计数。

每个任务开始时记录全部用例。遇到模型连续两次协议错误或程序异常会停止该组，保留已完成步骤与失败请求，不把未执行用例计为通过。当前工作台使用真实插件的知识库、工具目录和测试员称呼，与旧控制台的简化宿主不同，不能直接把两者分数当成同条件模型比较。

## 插件测试边界

直接项目引用 `VPet.Plugin.ChatVPet`，运行 `EnsureSave`、`ConfigureModels`、`RegisterTools`、`PlanAction`、购买/食用/状态核验及回执代码，并打开其原始 WPF 设置与日记窗口。

测试宿主实现 `IMainWindow` 的所需部分，提供独立 GameSave、商品目录、背包与本地保存。食用只模拟饱腹变化；演出请求记录为未播放。当前没有安装完整游戏 TalkBox、动画资源、TTS、麦克风和真正的游戏窗口生命周期。因此这里验证的是插件逻辑与窗口，不等于整个 VPet 宿主联调通过。未实现的宿主调用会报告异常，不伪造成功。

## 供 Codex 和脚本调用

Python 客户端只依赖标准库，程序需先启动：

```powershell
python ChatVPet.Workbench/workbench.py info
python ChatVPet.Workbench/workbench.py new --title "购买排查"
python ChatVPet.Workbench/workbench.py new --mod "D:/VPet/mod/0000_core" --save "D:/VPet/Saves/Save_1.lps"
python ChatVPet.Workbench/workbench.py send <ID> "帮我买一个面包，最多花8块，不要吃。"
python ChatVPet.Workbench/workbench.py wait <ID> --seconds 30
python ChatVPet.Workbench/workbench.py events <ID> --after 0
python ChatVPet.Workbench/workbench.py compare <ID> "面包现在多少钱？" --variant no-rerank
python ChatVPet.Workbench/workbench.py suite compression
python ChatVPet.Workbench/workbench.py plugin-check <ID>
python ChatVPet.Workbench/workbench.py export <ID> Local/diagnosis.json
```

`batch <ID> <文件.json>` 接收 TestStep 数组，每项支持 `Text`、`Expected`、`Observation`、`Rag`、`Money`、`Price`、`Inventory`、`Reload`。现场参数在该条消息前应用。图片通过 `send --image` 或 HTTP 的 `ImageBase64` 提供。

| 方法与路径 | 用途 |
|---|---|
| GET /api/info | 配置文件名、测试集与能力 |
| GET/POST /api/sessions | 会话列表 / 创建，POST 接收 SessionOptions |
| GET /api/sessions/{id} | 完整诊断快照 |
| GET /api/sessions/{id}/events?after=N | 增量事件及当前状态 |
| POST /api/sessions/{id}/steps | 单条 TestStep 或 `{"steps":[...]}`，立即返回 202 |
| POST /api/sessions/{id}/stop | 停止后续步骤 |
| POST /api/sessions/{id}/retry | 重试上一轮结果回复 |
| POST /api/sessions/{id}/compare | `{"text":"问题","variants":["baseline","no-rag"]}` |
| POST /api/sessions/{id}/notes | `{"text":"分析"}` |
| POST /api/sessions/{id}/plugin-check | 插件确定性自检，结束后恢复测试余额和背包 |
| GET /api/sessions/{id}/export | 不含配置密钥的完整 JSON |
| POST /api/suites | `{"name":"compression","options":{...}}` |

POST 使用 `application/json`。不开放网页跨域调用，不提供 shell 或任意文件读取接口。

运行 `python ChatVPet.Workbench/check_workbench.py` 可验证接口、插件自检、存档重开、分支隔离、增量记录与停止流程，全程不调用远程模型。
