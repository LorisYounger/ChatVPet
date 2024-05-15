---
license: other
library_name: peft
tags:
- llama-factory
- lora
- generated_from_trainer
base_model: THUDM/chatglm3-6b
model-index:
- name: vpet_v0.11
  results: []
---

<!-- This model card has been generated automatically according to the information the Trainer had access to. You
should probably proofread and complete it, then remove this comment. -->

# vpet_v0.11

This model is a fine-tuned version of [THUDM/chatglm3-6b](https://huggingface.co/THUDM/chatglm3-6b) on the vpetjson dataset.
It achieves the following results on the evaluation set:
- Loss: 1.4258

## Model description

More information needed

## Intended uses & limitations

More information needed

## Training and evaluation data

More information needed

## Training procedure

### Training hyperparameters

The following hyperparameters were used during training:
- learning_rate: 5e-06
- train_batch_size: 1
- eval_batch_size: 1
- seed: 42
- gradient_accumulation_steps: 8
- total_train_batch_size: 8
- optimizer: Adam with betas=(0.9,0.999) and epsilon=1e-08
- lr_scheduler_type: cosine
- lr_scheduler_warmup_steps: 1000
- num_epochs: 12.0

### Training results

| Training Loss | Epoch | Step | Validation Loss |
|:-------------:|:-----:|:----:|:---------------:|
| 1.964         | 0.26  | 100  | 1.9912          |
| 2.0627        | 0.52  | 200  | 1.9883          |
| 1.9979        | 0.79  | 300  | 1.9766          |
| 1.966         | 1.05  | 400  | 1.9482          |
| 1.8569        | 1.31  | 500  | 1.8955          |
| 1.8528        | 1.57  | 600  | 1.8203          |
| 1.7853        | 1.84  | 700  | 1.7393          |
| 1.5913        | 2.1   | 800  | 1.6641          |
| 1.5513        | 2.36  | 900  | 1.6016          |
| 1.4199        | 2.62  | 1000 | 1.5527          |
| 1.5253        | 2.89  | 1100 | 1.5176          |
| 1.4297        | 3.15  | 1200 | 1.4922          |
| 1.4786        | 3.41  | 1300 | 1.4727          |
| 1.3521        | 3.67  | 1400 | 1.4580          |
| 1.3979        | 3.94  | 1500 | 1.4463          |
| 1.3501        | 4.2   | 1600 | 1.4375          |
| 1.3169        | 4.46  | 1700 | 1.4326          |
| 1.3372        | 4.72  | 1800 | 1.4277          |
| 1.3164        | 4.99  | 1900 | 1.4277          |
| 1.42          | 5.25  | 2000 | 1.4268          |
| 1.3295        | 5.51  | 2100 | 1.4268          |
| 1.3836        | 5.77  | 2200 | 1.4258          |
| 1.3964        | 6.03  | 2300 | 1.4277          |
| 1.3828        | 6.3   | 2400 | 1.4277          |
| 1.4012        | 6.56  | 2500 | 1.4287          |
| 1.3588        | 6.82  | 2600 | 1.4297          |
| 1.3519        | 7.08  | 2700 | 1.4287          |
| 1.3285        | 7.35  | 2800 | 1.4297          |
| 1.4344        | 7.61  | 2900 | 1.4307          |
| 1.3242        | 7.87  | 3000 | 1.4307          |
| 1.3298        | 8.13  | 3100 | 1.4307          |
| 1.3775        | 8.4   | 3200 | 1.4307          |
| 1.2555        | 8.66  | 3300 | 1.4316          |
| 1.3897        | 8.92  | 3400 | 1.4307          |
| 1.1804        | 9.18  | 3500 | 1.4316          |
| 1.3383        | 9.45  | 3600 | 1.4316          |
| 1.3388        | 9.71  | 3700 | 1.4307          |
| 1.2815        | 9.97  | 3800 | 1.4316          |
| 1.3346        | 10.23 | 3900 | 1.4316          |
| 1.3423        | 10.5  | 4000 | 1.4316          |
| 1.325         | 10.76 | 4100 | 1.4316          |
| 1.2668        | 11.02 | 4200 | 1.4316          |
| 1.2578        | 11.28 | 4300 | 1.4316          |
| 1.3384        | 11.54 | 4400 | 1.4316          |
| 1.2837        | 11.81 | 4500 | 1.4316          |


### Framework versions

- PEFT 0.10.0
- Transformers 4.39.1
- Pytorch 2.2.1+cu118
- Datasets 2.18.0
- Tokenizers 0.15.2