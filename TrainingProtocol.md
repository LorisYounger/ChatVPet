# ChatVPet 训练协议

[English](#chatvpet-training-protocol)

只有开启设置中的「参与训练数据收集」开关并同意本协议，LBGAME 才会将相关对话与反馈用于模型训练。关闭该开关不影响正常聊天。

## 参与方式

开启「参与训练数据收集」后，您可以通过以下方式帮助改进桌宠：

- **点赞或点踩**：一键评价桌宠的回复。
- **修改建议**：指出改进方向，例如「查一下价格就好」,「不要防御性回复」。
- **详细改进**：主动展开详细编辑界面，具体修改桌宠应该做什么、做到哪一步，以及怎样回答。

以上三种入口均可在设置中单独选择是否显示。只有参与训练的玩家可以对已授权的回复发起纠错。

## 收集与使用范围

### 用于训练的数据

开启「参与训练数据收集」期间产生的已授权对话，以及您对已授权回复主动给出的评价、修改建议或改写，其以下内容可纳入训练数据：

- 已授权回合的聊天原文（若前文混有未授权期间的消息，导出时会自动剔除未授权历史，仅保留已授权部分）
- 桌宠回答及候选版本
- 理解该轮所需的上下文，如桌宠名字、当前状态，以及包含已授权对话的免费自动摘要与日记
- 该回复用到的 TOOLCALL AI 与视觉观察返回的文字结果（例如图片观察描述或联网搜索结果；原始图片本身不纳入训练）
- 您实际给出的评价、修改建议或改写内容

### 不纳入训练的数据

- 关闭「参与训练数据收集」期间新产生的聊天原文、桌宠回答与纯未授权摘要。日后重新开启，也不会将关闭期间的原话补入训练集。
- 原始图片文件/桌面截图、当时打开的软件或窗口清单、原始窗口内容，以及密钥或登录凭据。

### 常规服务记录

无论是否开启「参与训练数据收集」，LBGAME 都会保存您的聊天正文和桌宠回答，用于显示聊天历史、建立索引和检索先前的消息。这些消息不会与任何无关三方分享或在未允许的情况下进入训练集.



# ChatVPet Training Protocol

LBGAME will use relevant conversations and feedback for model training only if you turn on the "Participate in Training Data Collection" toggle in Settings and agree to this agreement. Turning off the toggle does not affect normal chat.

## How to Participate

With "Participate in Training Data Collection" enabled, you can help improve your VPet in the following ways:

- **Like or Dislike**: Rate your VPet's replies with one click.
- **Suggest Improvements**: Point out what should be improved, for example, "Just check the price" or "Don't give defensive replies."
- **Detailed Edits**: Open the detailed editing interface yourself to specify exactly what your VPet should do, how far it should go, and how it should reply.

You can choose individually in Settings whether to show each of these three options. Only players participating in training can submit corrections to authorized replies.

## Scope of Collection and Use

### Data Used for Training

Authorized conversations generated while "Participate in Training Data Collection" is enabled, as well as any ratings, suggestions for improvement, or rewrites you actively provide for authorized replies, may be included in the training data as follows:

- Original chat text from authorized rounds (if the preceding text contains messages from unauthorized periods, the export will automatically filter out the unauthorized history and retain only the authorized portions)
- VPet responses and candidate versions
- Context needed to understand the round, such as the VPet's name, current status, and the free automatic summaries and diaries that contain authorized conversations
- Text results returned by the TOOLCALL AI and visual observations used in this reply (e.g., image observation descriptions or online search results; the original images themselves are not included in training)
- Your actual ratings, suggested improvements, or rewrites

### Data Not Included in Training

- New chat transcripts, VPet responses, and purely unauthorized summaries generated while "Participate in Training Data Collection" is off. Even if you turn it back on later, the original text from the off period will not be added to the training set.
- Original image files/desktop screenshots, lists of the software or windows open at the time, original window content, and keys or login credentials.

### General Service Records

Regardless of whether "Participate in Training Data Collection" is on, LBGAME will save your chat messages and VPet responses to display chat history, build indexes, and retrieve previous messages. These messages will not be shared with any unrelated third parties or included in the training set without your permission.
