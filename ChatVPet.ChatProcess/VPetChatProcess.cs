using LinePutScript;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace ChatVPet.ChatProcess
{
    /// <summary>
    /// ChatVPet 聊天处理流程
    /// </summary>
    public class VPetChatProcess
    {
        /// <summary>
        /// GPT 调用方法
        /// </summary>
        /// <param name="system">系统消息</param>
        /// <param name="historys">历史消息</param>
        /// <param name="message">当前消息</param>
        /// <returns>返回的文本</returns>
        public delegate string GPTAsk(string system, List<string[]> historys, string message);
        /// <summary>
        /// GPT 调用方法
        /// </summary>
        [JsonIgnore] public GPTAsk? GPTAskFunction;
        /// <summary>
        /// 重要性计算方法 判断该段消息是否重要
        /// </summary>
        /// <param name="message">消息[0]Ask [1]Reply</param>
        /// <returns>分数, 范围:0-1</returns>
        public delegate double CalculateImportance(string[] message);
        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore] public CalculateImportance CalImportanceFunction = (x) => 0.5;

        /// <summary>
        /// 知识数据库
        /// </summary>
        public List<KnowledgeDataBase> KnowledgeDataBases = new List<KnowledgeDataBase>();
        /// <summary>
        /// 聊天历史
        /// </summary>
        public List<Dialogue> Dialogues = new List<Dialogue>();
        /// <summary>
        /// 工具库
        /// </summary>
        [JsonIgnore]
        public List<Tool> Tools = new List<Tool>();
        /// <summary>
        /// 本地化方法
        /// </summary>
        [JsonIgnore] public ILocalization Localization;

        /// <summary>
        /// 系统描述
        /// </summary>
        public string SystemDescription { get; set; } = "";

        /// <summary>
        /// ChatVPet 聊天处理流程
        /// </summary>
        /// <param name="localization">本地化</param>
        /// <param name="gptAskFunction">GPT调用方法</param>
        public VPetChatProcess(ILocalization localization, GPTAsk gptAskFunction)
        {
            Localization = localization;
            GPTAskFunction = gptAskFunction;
        }
        /// <summary>
        /// ChatVPet 聊天处理流程
        /// </summary>
        public VPetChatProcess()
        {
            Localization = new ILocalization.LChineseSimple();
        }
        /// <summary>
        /// 添加知识库内容 (纯文本)
        /// </summary>
        /// <param name="knowledgedb">知识库(文本)</param>
        public void AddKnowledgeDataBase(params string[] knowledgedb)
        {
            foreach (var knowledge in knowledgedb)
            {
                if (KnowledgeDataBases.Find(x => x.KnowledgeData == knowledge) == null)
                    KnowledgeDataBases.Add(new KnowledgeDataBase(knowledge, Localization));
            }
        }
        /// <summary>
        /// 消息最大历史记录数
        /// </summary>
        public int MaxHistoryCount { get; set; } = 10;
        /// <summary>
        /// 消息最大知识库数
        /// </summary>
        public int MaxKnowledgeCount { get; set; } = 10;
        /// <summary>
        /// 消息最大工具数
        /// </summary>
        public int MaxToolCount { get; set; } = 10;
        /// <summary>
        /// 消息输入聊天内容并获得回复 [注:默认卡线程等待回复,如在UI线程,记得TaskNew]
        /// </summary>
        /// <param name="message">用户输入的聊天内容</param>
        /// <param name="control">运行控制程序</param>
        /// <param name="ReturnResponse">返回的回复</param>
        public void Ask(string message, Action<ProcessResponse> ReturnResponse, ProcessControl control)
        {
            if (GPTAskFunction == null)
            {
                throw new Exception("GPTAskFunction is null");
            }

            int position = 0;

            var words = Localization.WordSplit(message);
            var keywords = IKeyWords.GetKeyWords(words);

            List<string> knowledgeDataBases = KnowledgeDataBases.Select(x => (x, x.InCheck(message, words, keywords)))
                    .OrderBy(x => x.Item2).Take(MaxKnowledgeCount).Where(x => x.Item2 < IInCheck.IgnoreValue).Select(x => x.x.KnowledgeData).ToList();
            List<Tool> tools = Tools.Select(x => (x, x.InCheck(message, words, keywords)))
                .OrderBy(x => x.Item2)
                .Take(MaxToolCount)
                .Where(x => x.Item2 < IInCheck.IgnoreValue).Select(x => x.x).ToList();
            //List<(Tool x, int)> test = new List<(Tool x, int)>();
            //foreach(var tool in Tools)
            //{
            //    var s = tool.InCheck(message, words, keywords);
            //    test.Add((tool, s));
            //}
            List<string[]> history = new List<string[]>();
            if (Dialogues.Count > 0)
            {
                List<(Dialogue, int)> list = new List<(Dialogue, int)>();
                List<(Dialogue, int)> willjoin = new List<(Dialogue, int)>();
                for (int i = 0; i < Dialogues.Count; i++)
                {
                    list.Add((Dialogues[i], i));
                }
                int nearmsg = MaxHistoryCount / 3;
                while (--nearmsg >= 0 && list.Count > 0)
                {
                    var last = list.Last();
                    list.Remove(last);
                    willjoin.Add(last);
                }
                if (list.Count > 0)
                {
                    List<(Dialogue, int, int)> dialogues = list.Select(x => (x.Item1, x.Item2, x.Item1.InCheck(message, words, keywords)))
                        .OrderBy(x => x.Item3).ToList();
                    foreach (var (dialogue, index, value) in dialogues)
                    {
                        if (willjoin.Count >= MaxHistoryCount)
                        {
                            break;
                        }
                        if (value < IInCheck.IgnoreValue)
                        {
                            willjoin.Add((dialogue, index));
                        }
                        else
                            break;
                    }
                }
                history.AddRange(willjoin.OrderBy(x => x.Item2).Select(x => x.Item1.ToMessages(Localization)));
            }

            string sysmessage = Localization.ToSystemMessage(SystemDescription, tools, knowledgeDataBases);
            bool isToolMessage = false;

            if (control.ForceToStop)
            {
                return;
            }

        retrytool:
            string respond = GPTAskFunction(sysmessage, history, message).Replace("\r", "");

            if (string.IsNullOrWhiteSpace(respond))
            {
                ReturnResponse.Invoke(new ProcessResponse()
                {
                    Reply = "GPTAskFunction return null",
                    IsEnd = true,
                    IsError = true,
                    ListPosition = position++
                });
                return;
            }
            else if (!respond.Contains(Localization.ToolCall))
            {
                ReturnResponse.Invoke(new ProcessResponse()
                {
                    Reply = "GPTAskFunction return error formart",
                    IsEnd = true,
                    IsError = true,
                    ListPosition = position++
                });
                //ReturnResponse.Invoke(new ProcessResponse()
                //{
                //    Reply = reply,
                //    IsEnd = true,
                //    IsError = false,
                //    ListPosition = position++
                //});
                return;
            }

            var res1 = Sub.Split(respond, '\n' + Localization.ToolCall + '\n', 1).Select(x => x.Trim([' ', '\n', '\r'])).ToList();
            if (res1.Count < 2)
            {
                res1.Add("[]");
            }
            string reply = res1[0];
            if (reply.Contains(Localization.Response))
                reply = reply.Substring(Localization.Response.Length);
            reply = reply.Trim([' ', '\n', '\r']);
            //发送返回消息
            ReturnResponse.Invoke(new ProcessResponse()
            {
                Reply = reply,
                IsEnd = false,
                IsError = false,
                ListPosition = position++
            });

            //添加聊天记录到历史            
            Dialogues.Add(new Dialogue(message, reply, res1[1], (isToolMessage ? 0 : CalImportanceFunction([message, reply])), Localization));

            List<ToolCall> toolcalls;
            var jsetting = ToolCall.jsonsetting;
            if (!res1[1].StartsWith('[') || !res1[1].EndsWith(']'))
                if (res1[1].StartsWith('{') && res1[1].EndsWith('}'))
                {
                    toolcalls = new List<ToolCall>();
                    var tc = JsonConvert.DeserializeObject<ToolCall>(res1[1], jsetting);
                    if (tc != null)
                        toolcalls.Add(tc);
                }
                else
                    toolcalls = new List<ToolCall>();
            else
                toolcalls = JsonConvert.DeserializeObject<List<ToolCall>>(res1[1], jsetting) ?? [];
            string toolreturn = "";
            //处理工具
            foreach (var tc in toolcalls)
            {
                Tool? tool = Tools.Find(x => x.Code == tc.Code);
                if (tool != null)
                {
                    var ret = tool.RunToolFunction(tc.Args);
                    if (!string.IsNullOrWhiteSpace(ret))
                    {
                        toolreturn += string.Format(Localization.ToolReturn, JsonConvert.SerializeObject(tc), ret);
                    }
                }
            }



            //工具消息多次调用
            if (toolreturn != "")
            {
                if (control.ForceToStop || control.StopBeforeNext)
                {
                    //发送结束消息
                    ReturnResponse.Invoke(new ProcessResponse()
                    {
                        Reply = "",
                        IsEnd = true,
                        IsError = false,
                        ListPosition = position++
                    });
                    return;
                }

                history.Add([message, respond]);
                message = toolreturn;
                isToolMessage = true;
                goto retrytool;
            }
            if (isToolMessage)
            {
                //最后一个ToolMessage可以有点重要性
                var last = Dialogues.Last();
                last.Importance = CalImportanceFunction([last.Question, last.Answer]);
            }

            //发送结束消息
            ReturnResponse.Invoke(new ProcessResponse()
            {
                Reply = "",
                IsEnd = true,
                IsError = false,
                ListPosition = position++
            });
        }

    }
}
