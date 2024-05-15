# ChatVPet

`Please use colloquial, cute, feminine, and naughty language style to communicate with me\nYou are a desktop pet, your name is {data.petname}, you like to play <VPet>`

Table Pet Language Model. Trained on VPet Chat Data

All training data collection has been approved by the user. [Training Protocol](TrainingProtocol.md)

### Model Introduction

**ChatVPet** is based on **ChatGLM-6B**, trained using **LLaMA-Factory** through user chat data collection. The goal is to provide **Pets** with a full life

### Timeline

* Collect user chat data [ChatGPT for Creative Workshop author](https://steamcommunity.com/sharedfiles/filedetails/?id=3157090829) **<- Currently here**
* Train the initial model [instruction supervision fine-tuning] *(waiting for data to accumulate to 50,000)*
* Collecting chat data for the initial model *(by asking users during the answer whether it is compatible with & ChatGPT/ChatVPet hybrid resolution)*
* Train the second-generation model [reward model training] *(waiting for data to accumulate to 10,000 pieces)*
* ... to be continued

### Currently it is still a shell project, used for introduction

## Due to insufficient sample size, training materials other than simplified Chinese will not be released at this time
