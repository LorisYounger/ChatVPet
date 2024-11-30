## ChatVPetProcess

### 简介
ChatVPetProcess 是一个用于处理和模拟自然语言交互的聊天系统。它通过整合 GPT 模型、知识库和工具，实现了对用户输入的自然语言的理解和处理。

ChatVPet 和 后续训练均会使用该处理模式和结构

### 功能介绍
- **知识库管理**：
  - 允许添加、编辑和管理多个知识库，以支持丰富的问答系统。
- **自定义工具**：
  - 可以集成各种自定义工具，以扩展插件的功能和行为。
- **本地化支持**：
  - 内置多种语言支持，根据不同区域设置提供适当的翻译和资源

### 安装
- 通过Parckage Manager

     ```
     Install-Package ChatVPet.ChatProcess 
     ```

* 通过nuget.org

  [ChatVPet.ChatProcess](https://www.nuget.org/packages/ChatVPet.ChatProcess/)

### 快速入门
1. **准备聊天方法**：

   ```csharp
   // 可以用自己或者其他的AI聊天程序, 这里是使用 ChatGPT.API.Framework
   // https://github.com/LorisYounger/ChatGPT.API.Framework
   /// <summary>
   /// 调用GPT的方法
   /// </summary>
   public string GPTAsk(string system, List<string[]> historys, string message)
   {
       Completions completions = new Completions();
       completions.max_tokens = 8000;//最大token限制
       completions.model = "gpt-4o-mini";
       
       completions.messages.Add(new Message() { role = Message.RoleType.system, content = system });
       foreach (var h in historys)
       {
           completions.messages.Add(new Message() { role = Message.RoleType.user, content = h[0] });
           completions.messages.Add(new Message() { role = Message.RoleType.system, content = h[1] });
       }
       completions.messages.Add(new Message() { role = Message.RoleType.user, content = message });
       var resp = completions.GetResponse("APIUrl", "APIKey");
       var reply = resp.GetMessageContent();
       if (resp.choices.Length == 0)
       {
           return "请检查API token设置".Translate();
       }
       else if (resp.choices[0].finish_reason == "length")
       {
           reply += " ...";
       }
       temptoken = resp.usage.total_tokens;
       TokenCount = temptoken;
       return reply;
   }
   ```

2.  **实例化 VPetChatProcess**

   ```C#
   public VPetChatProcess VPetChatProcess = new VPetChatProcess(new ILocalization.LChineseSimple(),GPTAsk);
   ```

3. **加载知识和工具**：

   ```c#
   // 加载知识库
   VPetChatProcess.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
   // 加载工具
   VPetChatProcess.Tools.Add(new Tool("takeitem", "购买并使用有物品id的物品,例如(吃)食物/正餐/零食/(喝)饮料/功能性/药品/(送)礼品".Translate(), ToolTakeItem, new List<Tool.Arg>()
   {
       new Tool.Arg(){ Name = "itemID", Description = "(int)物品id, 没有物品ID的物品没法使用".Translate() }
   }, VPetChatProcess.Localization));
   ```

4. **发送聊天请求**：
   ```csharp
   // 聊天控制器
   var pc = new ProcessControl();
   VPetChatProcess.Ask(content, (pr) =>
   {
       if (pr.IsError)
       {// 处理错误情况
          	MessageBox.Show("VCP报错: " + pr.Reply);
       }
       else if (!string.IsNullOrWhiteSpace(pr.Reply))
       {// 处理正常回复
           MessageBox.Show(pr.Reply);
       }
       if (pr.IsEnd || pr.IsError)
       {
           //结束前的处理
       }
       else if (pr.ListPosition >= 5)
       {
           MessageBox.Show("轮询次数超过5,已停止下一轮轮询");
           pc.StopBeforeNext = true;
       }
   }, pc);
   ```
