# ChatVPet

[English](README_en.md)

## 桌宠的生命只有两分钟，我要给她完整的一生

**ChatVPet** 是我们为《虚拟桌宠模拟器》开发的桌宠语言模型及配套聊天框架。早期桌宠 AI 聊天测试中，未经专门训练的模型常常反应慢、脱离设定或乱说话，因此我们开始收集玩家授权的聊天数据，训练专属的中文对话模型，并同步开发配套的聊天处理库与桌宠插件。

正式版模型 **ChatVPet-qw35-9B-v1** 已发布至 Hugging Face，提供完整 BF16 权重与面向本地部署的 GGUF 量化版。配套的 `ChatVPet.ChatProcess` 处理库和 `VPet.Plugin.ChatVPet` 插件已完成重写，支持对白与游戏操作分离、知识与记忆检索、日记及累计前情提要、宿主回执核对与崩溃恢复。

## 模型下载（ChatVPet-qw35-9B-v1）

**ChatVPet-qw35-9B-v1** 基于 `Qwen/Qwen3.5-9B` 进行 LoRA 微调，并针对游戏操作、回执和日记检索补训与权重编辑，用于《虚拟桌宠模拟器》中萝莉斯的简体中文对白。

| 仓库 | 格式与用途 | 推荐起步 |
|---|---|---|
| [LorisYounger/ChatVPet-qw35-9B-v1](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1) | 合并后的 BF16 权重（约 19.3 GB，4 个 Safetensors 分片），适用于 Transformers 加载或继续二次开发 | 完整目录下载 |
| [LorisYounger/ChatVPet-qw35-9B-v1-GGUF](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1-GGUF) | 面向 llama.cpp / LM Studio 的量化版，提供 Q5_K_M、Q6_K、Q4_K_M 与可选 F16 视觉投影（mmproj） | **Q5_K_M**（6.19 GiB，纯文字版） |

桌宠默认使用纯文字版即可：下载 GGUF 主模型和 `system_prompt.txt`，用 LM Studio 或 `llama-server` 启动本地 OpenAI 兼容接口：

```sh
llama-server -m ChatVPet-qw35-9B-v1-Q5_K_M.gguf \
  --alias chatvpet --host 127.0.0.1 --port 8080 \
  -ngl 99 -np 1 -c 8192 --jinja --reasoning off
```

在插件设置页接入时，勾选“使用 ChatVPet 专用提示词”，插件会自动使用与模型包内 `system_prompt.txt` 一致的专用模板（[system-chatvpet-template.txt](training/chatvpet/system-chatvpet-template.txt)）。详细评测指标、量化档对比、SHA-256 校验与看图参数见 [Release.md](Release.md) 及两份模型卡：[BF16 模型卡](docs/models/ChatVPet-qw35-9B-v1/README.md) · [GGUF 模型卡](docs/models/ChatVPet-qw35-9B-v1-GGUF/README.md)。

> 受样本数量限制，当前模型仅支持简体中文，其他语言留待后续。

## 对白与操作分工

为了让桌宠聊得自然，同时保证查价、购买、喂食和活动等游戏操作可靠，聊天与操作采用分工架构：

- **ChatVPet** 负责角色对白、说话表情和意图抬头。普通聊天直接回复；需要查信息、操作桌宠、翻旧账、看屏幕、播动画或退出游戏时，在首行附上 `[delegate]`、`[recall]`、`[screenshot]`、`[animation]`、`[quit]` 等标签。
- **ChatVPetProcess** 负责按存档管理会话、检索相关知识与记忆、整理日记和累计前情提要、拼装协议消息，并追踪操作回执。
- **ToolCallAI**（另配支持工具调用与视觉的通用模型，例如 DeepSeek 或 OpenAI 模型）负责根据玩家原话、桌宠初答和现场状态选择工具、识别截图以及生成日记摘要。
- **桌宠插件** 负责在执行前核对预算、单价、库存和当前状态，执行后向宿主保存回执，再由 **ChatVPet** 把实际结果告诉玩家。重新生成回答不会重复消费。

在模拟商店与桌宠联调中，Q5_K_M 负责对白并搭配独立操作模型通过了 21/21 项联调；由 Q5_K_M 兼任操作模型仅通过 9/21 项，因此部署时建议保持对白与操作分离。

## 仓库内容

| 项目 / 文档 | 说明 |
|---|---|
| [ChatVPet.ChatProcess](ChatVPet.ChatProcess) | .NET 8 聊天处理库，负责知识与历史检索、日记和累计提要、协议解析、模型调用与回执管理 |
| [VPet.Plugin.ChatVPet](VPet.Plugin.ChatVPet) | .NET 8 Windows/WPF 桌宠插件，接入《虚拟桌宠模拟器》，提供状态读取、物品和活动工具、气泡与语音 |
| [ChatVPetProcess.md](ChatVPetProcess.md) | 处理库的安装方式、接口配置、对话协议与回执规范 |
| [VPet.Plugin.ChatVPet/README.md](VPet.Plugin.ChatVPet/README.md) | 插件存档格式、API 配置、编译及离线验证说明 |
| [Release.md](Release.md) | 模型各版本发布记录与评测摘要 |

开发时直接打开 [ChatVPet.sln](ChatVPet.sln) 即可。

## 项目进度

- **数据征集**：通过 [ChatGPT for 创意工坊作者](https://steamcommunity.com/sharedfiles/filedetails/?id=3157090829) 在玩家同意后持续收集简体中文对话数据。
- **初代验证（2024-05-15）**：基于 ChatGLM-6B 训练 `ChatVPet_v0.11`，验证早期流程。
- **框架重写（2026-11）**：完成 `ChatVPetProcess` 与桌宠插件重写，实现分离式协议、记忆检索、日记与累计前情提要、宿主回执与重启恢复。
- **v1 模型发布（2026-11-24，当前阶段）**：发布基于 Qwen3.5-9B 的 `ChatVPet-qw35-9B-v1`（BF16 与 GGUF 量化版）。
- **后续方向**：继续收集真实对话与反馈，改善长对话前情提要读取、连续改名记忆以及正文格式约束，并逐步探索多语言支持。

## 训练数据与许可

- **训练数据**：均在玩家同意后收集，不提供公开下载，详见 [训练协议](TrainingProtocol.md)。
- **代码许可**：本仓库源码采用 [GPL-3.0](LICENSE)。
- **模型许可**：`ChatVPet-qw35-9B-v1` 与 GGUF 量化权重随模型仓库采用 Apache 2.0。
