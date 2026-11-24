# ChatVPet

[简体中文](README.md)

## VPet's life is only two minutes, I want to give her a full life

**ChatVPet** is a dedicated language model and chat processing framework built for *VPet Simulator* (*虚拟桌宠模拟器*). In early AI chat tests, general models without domain training were often slow, inconsistent in persona, or prone to off-character replies. We started ChatVPet to train a tailored conversational model on player-approved chat data, alongside an open-source processing library and VPet plugin.

The v1 model **ChatVPet-qw35-9B-v1** is now available on Hugging Face in both full BF16 and local-friendly GGUF formats. The companion `ChatVPet.ChatProcess` library and `VPet.Plugin.ChatVPet` plugin have been rewritten to separate roleplay dialogue from tool execution, with memory retrieval, diary/recap compression, and receipt-verified host actions.

## Model Downloads (`ChatVPet-qw35-9B-v1`)

**ChatVPet-qw35-9B-v1** is fine-tuned from `Qwen/Qwen3.5-9B` with LoRA, followed by targeted post-training for game actions, host receipts, and diary recall, plus selective MLP weight editing to reduce redundant recall requests.

| Repository | Format & Use Case | Recommended Start |
|---|---|---|
| [LorisYounger/ChatVPet-qw35-9B-v1](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1) | Merged BF16 weights (~19.3 GB across 4 Safetensors shards) for Transformers or further fine-tuning | Full repository |
| [LorisYounger/ChatVPet-qw35-9B-v1-GGUF](https://huggingface.co/LorisYounger/ChatVPet-qw35-9B-v1-GGUF) | Quantized GGUF builds for llama.cpp / LM Studio (`Q5_K_M`, `Q6_K`, `Q4_K_M`) plus optional F16 `mmproj` for vision | **Q5_K_M** (6.19 GiB, text-only) |

Text-only weights are sufficient for normal VPet deployment. Download the GGUF file and `system_prompt.txt`, then run an OpenAI-compatible server with `llama-server` (or load directly in LM Studio):

```sh
llama-server -m ChatVPet-qw35-9B-v1-Q5_K_M.gguf \
  --alias chatvpet --host 127.0.0.1 --port 8080 \
  -ngl 99 -np 1 -c 8192 --jinja --reasoning off
```

When connecting from the VPet plugin, enable **"Use ChatVPet Dedicated Prompt"** ("使用 ChatVPet 专用提示词") in settings so the plugin renders the matching prompt template ([system-chatvpet-template.txt](training/chatvpet/system-chatvpet-template.txt)). For benchmarks, quantization comparisons, SHA-256 checksums, and vision setup, see [Release.md](Release.md) and the model cards ([BF16](docs/models/ChatVPet-qw35-9B-v1/README.md) · [GGUF](docs/models/ChatVPet-qw35-9B-v1-GGUF/README.md)).

> Due to training sample availability, the current release supports **Simplified Chinese only**. Other languages are planned for future updates.

## Architecture: Dialogue & Tool Separation

To keep conversation natural while ensuring reliable in-game actions (checking prices, buying items, feeding, animations), responsibilities are split across four parts:

- **ChatVPet** handles dialogue, speaking expressions, and intent headers (`[delegate]`, `[recall]`, `[screenshot]`, `[animation]`, `[quit]`).
- **ChatVPetProcess** manages per-save sessions, hybrid knowledge/memory retrieval (embedding + reranker), diary and rolling recap compression, protocol formatting, and receipt tracking.
- **ToolCallAI** (a separate tool-calling and vision-capable model, such as DeepSeek or OpenAI models) selects host tools, reads screenshots, and generates diary/recap summaries.
- **VPet Plugin** validates prices, budgets, inventory, and pet state before execution, persists host receipts to prevent duplicate purchases on retry or restart, and hands verified outcomes back to **ChatVPet** for the follow-up reply.

In simulated store and host integration tests, pairing `ChatVPet-qw35-9B-v1` (`Q5_K_M`) for dialogue with a separate tool-calling model passed **21/21** checks, compared to **9/21** when using ChatVPet for both dialogue and tool execution.

## Repository Contents

| Project / Doc | Description |
|---|---|
| [ChatVPet.ChatProcess](ChatVPet.ChatProcess) | .NET 8 chat processing library (retrieval, diaries & recaps, protocol parsing, model API client, receipt state) |
| [VPet.Plugin.ChatVPet](VPet.Plugin.ChatVPet) | .NET 8 Windows/WPF plugin integrating `ChatVPet.ChatProcess` into *VPet Simulator* |
| [ChatVPetProcess.md](ChatVPetProcess.md) | Library installation, API setup, dialogue protocol, and receipt specification (Chinese) |
| [VPet.Plugin.ChatVPet/README.md](VPet.Plugin.ChatVPet/README.md) | Plugin settings, save layout, build, and offline verification guide (Chinese) |
| [Release.md](Release.md) | Model release history and evaluation summary (Chinese) |

Open [ChatVPet.sln](ChatVPet.sln) in Visual Studio to build and develop.

## Roadmap & Milestones

- **Data Collection**: Ongoing opt-in Simplified Chinese chat data collection via [ChatGPT for Workshop Creators](https://steamcommunity.com/sharedfiles/filedetails/?id=3157090829).
- **Early Prototype (`ChatVPet_v0.11`, 2024-05-15)**: Initial ChatGLM-6B experiment on early data.
- **Framework Rewrite (2026-11)**: Rewrote `ChatVPetProcess` and the VPet plugin with separated dialogue/tool roles, memory retrieval, diary/recap compression, and receipt recovery.
- **v1 Model Release (`ChatVPet-qw35-9B-v1`, 2026-11-24 — Current)**: Published Qwen3.5-9B-based BF16 and GGUF weights on Hugging Face.
- **Next**: Improve long-conversation recap usage, multi-turn renaming memory, strict output formatting, and expand language coverage as more data becomes available.

## Training Data & Licenses

- **Training Data**: Collected only with explicit player consent and not redistributed publicly. See [TrainingProtocol.md](TrainingProtocol.md).
- **Source Code**: Licensed under [GPL-3.0](LICENSE).
- **Model Weights**: Licensed under Apache 2.0 (provided in the Hugging Face model repositories).
