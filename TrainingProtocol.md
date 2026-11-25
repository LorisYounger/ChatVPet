# ChatVPet 训练协议

[English](#chatvpet-training-protocol)

只有开启设置中的「参与训练数据收集」开关并同意本协议，LBGAME 才会将相关对话与反馈用于模型训练。关闭该开关不影响正常聊天。

插件升级时沿用您原有的训练选择。更新通知会介绍新版功能，您可随时在设置中查看或更改授权。

## 参与方式

开启「参与训练数据收集」后，您可以通过以下方式帮助改进桌宠：

- **点赞或点踩**：一键评价桌宠的回复。
- **修改建议**：用一句话指出改进方向，例如「查一下价格就好」。
- **详细改进**：主动展开详细编辑界面，具体修改桌宠应该做什么、做到哪一步，以及怎样回答。

以上三种入口均可在设置中单独选择是否显示。只有参与训练的玩家可以对已授权的回复发起纠错，每条原回复最多免费换答 5 次。关闭「参与训练数据收集」时，反馈和纠错入口均隐藏，您仍可正常发送新消息或恢复中断的回复。

## 收集与使用范围

### 用于训练的数据

仅当开启收集，且您对某条回复主动给出评价、修改建议或改写时，该轮相关的以下内容可能纳入训练数据：

- 相关聊天原文
- 桌宠回答及候选版本
- 理解该轮所需的上下文，如桌宠名字与当前状态（例如 `[当前状态: 开心, 好感度:陌生(2)]`）
- 该回复用到的 TOOLCALL AI 返回结果（TOOLCALL AI 负责替桌宠查看信息或执行操作；例如您问「下载完成了吗？」，工具返回「进度为 80%」，训练样本可包含这句结果及桌宠据此作出的回答）
- 您实际给出的评价、修改建议或改写内容

### 不纳入训练的数据

- 关闭「参与训练数据收集」期间的聊天、桌宠回答、工具结果和反馈。日后重新开启，也不会将关闭期间的记录补入训练集。
- TOOLCALL AI 为执行任务读取的原始资料，包括桌面截图、当时打开的软件或窗口清单，以及工具读取的原始窗口内容。

### 常规服务记录

无论是否开启「参与训练数据收集」，LBGAME 都会保存您的聊天正文和桌宠回答，用于显示聊天历史、建立索引和检索先前的消息。这些消息不会与任何无关三方分享或在未允许的情况下进入训练集.

# ChatVPet Training Protocol

[简体中文](#chatvpet-训练协议)

LBGAME uses your conversations and feedback for model training only after you agree to this protocol and enable the "Participate in training data collection" setting. You can use chat normally with this setting turned off.

Updating the plugin preserves your existing training preference. The update notice introduces the new features, and you can review or change your choice in Settings at any time.

## How to Participate

When "Participate in training data collection" is enabled, you can help improve the pet in three ways:

- **Thumbs-up or thumbs-down**: Rate a reply with a single click.
- **Short suggestion**: Write a brief note on what to change, such as "just check the price."
- **Detailed edit**: Open the detailed editor to specify what the pet should do, where it should stop, and how it should reply.

Settings let you choose which of these three controls to show. Only players who participate in training can request corrections to eligible replies, with up to five free alternatives per original reply. Turning collection off hides feedback and correction controls. You can still send new messages or resume an interrupted reply.

## Data Scope

### Data Used for Training

Only when collection is enabled and you actively rate, suggest changes to, or edit a reply may the following data from that turn be included in training data:

- The relevant chat messages
- The pet's replies and alternative replies
- The context needed to understand the turn, such as the pet's name and current status (e.g. `[Current status: Happy, Affection: Stranger(2)]`)
- The TOOLCALL AI result used for that reply (TOOLCALL AI looks up information or performs actions for the pet; for example, if you ask whether a download is finished and the tool returns "80% complete," the training sample may include that result and the pet's reply)
- The rating, suggestion, or edit you provided

### Data Excluded from Training

- Messages, replies, tool results, and feedback from periods when "Participate in training data collection" is off. Turning the setting on later does not add earlier conversations to the training set.
- Raw material read by TOOLCALL AI to complete a task, including desktop screenshots, lists of open applications or windows, and raw window contents. Even if a tool copies a full window list into its result, those raw details are excluded.

### Standard Service Records

Regardless of the training setting, LBGAME stores your chat messages and the pet's replies to display chat history and to index and retrieve earlier messages. These messages will not be shared with any third parties or entered into the training set without permission.
