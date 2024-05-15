# ChatVPet
[English](README_en.md)

`请使用口语化的、可爱的、女性化的、调皮的语言风格和我交流\n你是一只桌宠, 你的名字叫{data.petname}, 你喜欢玩 <虚拟主播模拟器>`

桌宠语言模型. 基于桌宠聊天数据训练而来.

所有训练数据收集均已通过用户同意. [训练协议](TrainingProtocol.md)

### 模型介绍

**ChatVPet** 基于 **ChatGLM-6B** , 使用 **LLaMA-Factory** 通过用户聊天数据收集训练而来. 目的是给予**桌宠**完整的一生.

### 时间线

* 征集用户聊天数据 [ChatGPT for 创意工坊作者](https://steamcommunity.com/sharedfiles/filedetails/?id=3157090829) **<-目前在这**
* 训练初代模型[指令监督微调] *(等待数据累计至5万条)*
* 征集初代模型聊天数据 *(通过在回答时询问用户是否符合+ChatGPT/ChatVPet混合分辨)*
* 训练二代模型[奖励模型训练] *(等待数据累计至1万条)*
* …待续

### 目前还是个空壳子项目, 用于放介绍

## 生成版本

所有生成版本均放在 output 文件夹下. 正式版会放到 huggingface

如需使用, 请使用 **[LLaMA-Factory](https://github.com/hiyouga/LLaMA-Factory)** 加载数据

### ChatVPet_v0.11

基于目前 3080 条简体中文数据计算而来. 效果看上去还可以, 不过可能是因为**ChatGLM-6B**的缘故,说话有概率夹杂一堆英文

![image-20240515130724620](README.assets/image-20240515130724620.png)

![image-20240515131149043](README.assets/image-20240515131149043.png)

后续应该还是要准备一些用户提交的期望数据再次训练下, 以及做点奖励模型训练
