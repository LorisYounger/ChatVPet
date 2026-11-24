---
language:
- zh
license: apache-2.0
library_name: transformers
pipeline_tag: image-text-to-text
base_model: Qwen/Qwen3.5-9B
base_model_relation: finetune
tags:
- conversational
- vpet
- qwen3_5
---

# ChatVPet-qw35-9B-v1

ChatVPet 是我们给《虚拟桌宠模拟器》的萝莉斯做的中文对话模型。她会接着玩家前面说过的话往下聊；需要翻日记、看屏幕或问游戏里的价格时，她会在对白前加上对应标签，等桌宠把结果带回来再继续回答。

本仓库提供合并后的 BF16 模型。四个 Safetensors 分片合计约 19.3 GB，下载完整目录就能用 Transformers 加载。想在自己的电脑上用 llama.cpp 或 LM Studio 聊几句，可前往 [GGUF 版](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1-GGUF)，从 6.19 GiB 的 Q5_K_M 开始。

## 和萝莉斯聊一句

在下载好的模型目录中运行下面的 Python 示例。我们在 RTX 5090 D 上使用 Transformers 5.3.0、PyTorch 2.7.0 和 Accelerate 验证过这段代码。

```python
from pathlib import Path

import torch
from transformers import AutoModelForImageTextToText, AutoTokenizer

model_dir = Path(".")
tokenizer = AutoTokenizer.from_pretrained(model_dir)
model = AutoModelForImageTextToText.from_pretrained(
    model_dir,
    dtype=torch.bfloat16,
    device_map={"": "cuda:0"},
    attn_implementation="sdpa",
).eval()

system = (model_dir / "system_prompt.txt").read_text(encoding="utf-8").strip()
messages = [
    {"role": "system", "content": system},
    {"role": "user", "content": "上回没想好。现在给染布定名灰翎。我刚定了什么名？"},
]
inputs = tokenizer.apply_chat_template(
    messages, add_generation_prompt=True, tokenize=True,
    return_dict=True, return_tensors="pt",
).to(model.device)
with torch.inference_mode():
    output = model.generate(**inputs)
reply = output[0, inputs.input_ids.shape[1]:]
print(tokenizer.decode(reply, skip_special_tokens=True))
```

这句在实测中得到的回复是：

```text
[expression:conscientious]
灰翎。
```

桌宠读第一行选表情，第二行才显示给玩家。多轮聊天时，将之前的玩家消息与萝莉斯带标签的完整回答一并传回。系统消息使用仓库内的 [角色提示词](./system_prompt.txt)；包内默认贪心解码，最多生成 150 个 token，遇到 `<|im_end|>`（248046）结束。

## 接到桌宠里

玩家问“面包多少钱？只查价格，不要买。”时，萝莉斯可以用 `[delegate]` 请游戏询价；模拟商店报回 8 块后，她回答“查到啦，面包8块。没买，只是看了价格。”玩家翻旧账时，`[recall]` 请求查以前的聊天或日记；`[screenshot]` 则用于请求宿主提供屏幕观察结果。

接入时将 ChatVPet 用作对白模型，另配支持工具调用的操作模型处理查价与购买。该分工在 Q5_K_M 的模拟桌宠联调中通过 21/21 项；由 ChatVPet 兼任操作模型时通过 9/21 项，多次缺少执行所需的工具回执。具体实现见 [聊天处理库和插件](https://github.com/LorisYounger/ChatVPet)。

## 这版怎么选的

我们先用 LLaMA-Factory 在 Qwen3.5-9B 上做 rank 128 的 LoRA 微调。早期中文训练集 `release-v21` 包含 60,543 条不同样本，加权后为 61,029 条。后续针对游戏操作、回执和日记检索补训，选取修复轮的第 24 步，再编辑一层 MLP 的 8 列权重，以减少当轮已经给出答案时多余的查日记请求。正式 v1 采用这份编辑后的 P2 权重（详见 [训练数据使用约定](https://github.com/LorisYounger/ChatVPet/blob/main/TrainingProtocol.md)）。

选版时，编辑前后在同一组 48 道盲题上分别通过 29 道和 33 道，真正需要查日记的 18 道题两版均全部通过。对 18 道当轮给名题逐字复核，能明确给出正确名字且没有追问或自我否定的回答由 5 条增至 7 条。连续改名仍可能出错，例如花铲改叫“柚白”后，模型有时会说记不清了。完整数据见 [评测记录](./evaluation.json)。

另在早期版本留下的 128 条固定验证对话上（保留各题原有的系统消息），对 42,603 个桌宠回复 token 计算平均 loss：正式 BF16 为 1.79824，早期 BF16 为 1.74657（详见 [完整 loss 结果](./loss-report.json)）。这项指标对照的是早期权重，前面的 48 道盲题则对照第 24 步在权重编辑前后的对话表现。

下载后可在模型目录运行 `sha256sum -c SHA256SUMS` 核对文件。基础模型说明与权重许可随包提供：[Qwen3.5-9B 模型卡](./README.base.md) · [Apache 2.0](./LICENSE)。

在 ChatVPet 插件中接入时，勾选“使用 ChatVPet 专用提示词”。项目内的[专用模板](../../../training/chatvpet/system-chatvpet-template.txt)与正式 v1 的 `system_prompt.txt` 使用相同内容，仅将姓名与主人称呼参数化；默认身份渲染后文本一致。通用聊天模型另用[完整兼容模板](../../../training/chatvpet/system-process-template.txt)。
