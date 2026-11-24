---
language:
- zh
license: apache-2.0
library_name: gguf
pipeline_tag: text-generation
base_model: LorisYounger/ChatVPet-qw35-9B-v1
base_model_relation: quantized
tags:
- conversational
- vpet
- llama.cpp
---

# ChatVPet-qw35-9B-v1-GGUF

**桌宠默认用纯文字版。** 下载 [Q5_K_M 主模型（6.19 GiB）](./ChatVPet-qw35-9B-v1-Q5_K_M.gguf) 和 [角色提示词](./system_prompt.txt) 两个文件即可。本仓库的三个量化档均来自同一份 [BF16 原模型](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1)，主文件只含语言模型权重，可以独立运行；如果截图由宿主的另一个视觉服务识别，萝莉斯会直接根据返回的文字继续回答。

## 怎么选量化版本

| 文件 | 文件大小 | 验证 loss ↓ | 桌宠回归通过 | 其中 48 道选版题 |
| --- | ---: | ---: | ---: | ---: |
| [Q5_K_M](./ChatVPet-qw35-9B-v1-Q5_K_M.gguf) | 6.19 GiB | 1.81073 | 339/401 | 33/48 |
| [Q6_K](./ChatVPet-qw35-9B-v1-Q6_K.gguf) | 7.04 GiB | 1.79775 | 335/401 | 34/48 |
| [Q4_K_M](./ChatVPet-qw35-9B-v1-Q4_K_M.gguf) | 5.38 GiB | 1.81760 | 328/401 | 30/48 |

Q5_K_M 在这组回归中通过的题最多。Q6_K 更常保留原始 BF16 的措辞，也保住了权重编辑修好的两道题：刚给手铃起名“浅滩”、给画架起名“江灯”，它都能直接答出名字；Q5_K_M 答对了画架，手铃那题仍会触发查日记。想把主文件控制在 5.4 GiB 以内可以选 Q4_K_M，运行时还需为上下文预留内存。

在同样的 401 道已揭晓回归题上，原生 Transformers BF16 通过 338 道，未量化的 BF16 GGUF 通过 337 道；三个量化档在真正需要查日记的 18 道题上全部通过（逐组结果见 [evaluation.json](./evaluation.json)）。

验证 loss 在早期版本用过的 128 条固定验证对话上计算，保留原有系统消息和历史，只统计 42,603 个桌宠回复 token。正式 v1 原生 BF16 为 1.79824，未量化 BF16 GGUF 为 1.79778，Q6_K 在这批对话上最接近未量化模型（详见 [完整 loss 结果](./loss-report.json)）。

## 纯文字运行

在支持 Qwen3.5 的 LM Studio 中加载主模型，将 `system_prompt.txt` 的内容粘贴到 System Prompt，并使用模型内嵌的聊天模板。也可以在模型目录用 llama.cpp 启动本地服务：

```sh
llama-server -m ChatVPet-qw35-9B-v1-Q5_K_M.gguf \
  --alias chatvpet --host 127.0.0.1 --port 8080 \
  -ngl 99 -np 1 -c 8192 --jinja --reasoning off
```

打开 `http://127.0.0.1:8080` 即可聊天。通过 API 接入时，向 `/v1/chat/completions` 发送对话，第一条 `system` 消息放入角色提示词；复现实测可设 `temperature=0`、`max_tokens=150`。回复第一行的标签用于选表情和请求操作，后面是说给玩家听的对白。

在 RTX 5090 D 上，使用 llama-server 将 Q5_K_M 全层放入 GPU（上下文 8192，单并发），回答同一句文字后，纯文字进程占用 6490 MiB 显存，额外加载视觉投影的进程占用 7366 MiB；两次都答出“灰翎”，纯文字配置节省 876 MiB 显存。

在模拟商店联调中，当玩家问“面包多少钱？只查价格，不要买。”时，Q5_K_M 的第一句回复是：

```text
[delegate][expression:conscientious]
我去看看面包的标价。
```

独立操作模型向宿主查到面包单价为 8 后，萝莉斯才回答“查到啦，面包8块。没买，只是看了价格。”按这个分工，桌宠的对白、查询、购买和结果续答通过了 21/21 项联调；把同一份 Q5_K_M 同时用于对白和操作时只通过 9/21 项。接入方式见 [宿主接入代码](https://github.com/LorisYounger/ChatVPet)。

多轮对话中模型仍可能记错名字，例如把玩家刚起的“柚白”写成“栲白”。

## 可选：让 ChatVPet 自己看图

三个量化档共用一份 876 MiB 的 [视觉投影](./mmproj-ChatVPet-qw35-9B-v1-F16.gguf)。需要向模型发图时，下载该文件并在上面的启动命令末尾追加：

```sh
--mmproj mmproj-ChatVPet-qw35-9B-v1-F16.gguf
```

在同一张测试图上，三个量化档都读出了数字 731、左边的红色正方形和右边的蓝色圆形。

完整下载后，可在目录中运行 `sha256sum -c SHA256SUMS` 核对文件，详见 [校验清单](./SHA256SUMS) · [Apache 2.0](./LICENSE)。

在 ChatVPet 插件中接入时，勾选“使用 ChatVPet 专用提示词”。项目内的[专用模板](../../../training/chatvpet/system-chatvpet-template.txt)与正式 v1 的 `system_prompt.txt` 使用相同内容，仅将姓名与主人称呼参数化；默认身份渲染后文本一致。通用聊天模型另用[完整兼容模板](../../../training/chatvpet/system-process-template.txt)。
