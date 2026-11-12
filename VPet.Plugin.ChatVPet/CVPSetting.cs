using LinePutScript;
using LinePutScript.Localization.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPet.Plugin.ChatVPet
{
    public partial class CVPPlugin
    {
        /// <summary>
        /// 是否在聊天位置显示Token数量
        /// </summary>
        public bool ShowToken
        {
            get => !MW.Set["ChatVPetProcess"][(gbol)"noshowtoken"];
            set => MW.Set["ChatVPetProcess"][(gbol)"noshowtoken"] = !value;
        }
        /// <summary>
        /// 知识库设置
        /// </summary>
        public string KnowledgeDataBase
        {
            get => MW.Set["ChatVPetProcess"][(gstr)"KnowledgeDataBase"] ?? "";
            set => MW.Set["ChatVPetProcess"][(gstr)"KnowledgeDataBase"] = value.Replace("\r", "");
        }
        /// <summary>
        /// 最大反复调用次数
        /// </summary>
        public int MaxRecallCount
        {
            get => MW.Set["ChatVPetProcess"].GetInt("MaxRecallCount", 5);
            set => MW.Set["ChatVPetProcess"][(gint)"MaxRecallCount"] = value;
        }
        /// <summary>
        /// 累计使用Token数量
        /// </summary>
        public int TokenCount
        {
            get => MW.Set["ChatVPetProcess"][(gint)"TokenCount"];
            set => MW.Set["ChatVPetProcess"][(gint)"TokenCount"] = value;
        }

        public string AzureKey
        {
            get => MW.Set["ChatVPetProcess"][(gstr)"AzureKey"] ?? "";
            set => MW.Set["ChatVPetProcess"][(gstr)"AzureKey"] = value;
        }
        public string AzureRegion
        {
            get => MW.Set["ChatVPetProcess"][(gstr)"AzureRegion"] ?? "";
            set => MW.Set["ChatVPetProcess"][(gstr)"AzureRegion"] = value;
        }
        public bool AzureVoiceEnable
        {
            get => MW.Set["ChatVPetProcess"][(gbol)"AzureVoiceEnable"];
            set => MW.Set["ChatVPetProcess"][(gbol)"AzureVoiceEnable"] = value;
        }
        public string AzureRecognitionLanguage
        {
            get
            {
                var lang = MW.Set["ChatVPetProcess"][(gstr)"AzureRecognitionLanguage"];
                if (string.IsNullOrWhiteSpace(lang))
                {
                    switch (LocalizeCore.CurrentCulture)
                    {
                        case "zh-Hans":
                            lang = "zh-CN";
                            break;
                        case "zh-Hant":
                            lang = "zh-HK";
                            break;
                        default:
                            lang = "en-US";
                            break;
                    }
                    AzureRecognitionLanguage = lang;
                }
                return lang;
            }
            set => MW.Set["ChatVPetProcess"][(gstr)"AzureRecognitionLanguage"] = value;
        }

        public int MaxHistoryCount
        {
            get => MW.Set["ChatVPetProcess"].GetInt("MaxHistoryCount", 6);
            set => MW.Set["ChatVPetProcess"][(gint)"MaxHistoryCount"] = value;
        }

        public int MaxToolCount
        {
            get => MW.Set["ChatVPetProcess"].GetInt("MaxToolCount", 4);
            set => MW.Set["ChatVPetProcess"][(gint)"MaxToolCount"] = value;
        }

        public int MaxKnowledgeCount
        {
            get => MW.Set["ChatVPetProcess"].GetInt("MaxKnowledgeCount", 4);
            set => MW.Set["ChatVPetProcess"][(gint)"MaxKnowledgeCount"] = value;
        }

        public int TotalTokensUsage
        {
            get => MW.Set["ChatVPetProcess"].GetInt("TotalTokensUsage");
            set => MW.Set["ChatVPetProcess"][(gint)"TotalTokensUsage"] = value;
        }

        /// <summary>
        /// 当 Dialogues 条数超过此值时触发历史压缩（<=0 表示禁用）
        /// </summary>
        public int MaxHistoryBeforeCompress
        {
            get => MW.Set["ChatVPetProcess"].GetInt("MaxHistoryBeforeCompress", 24);
            set => MW.Set["ChatVPetProcess"][(gint)"MaxHistoryBeforeCompress"] = value;
        }

        /// <summary>
        /// 压缩时保留最近的对话条数
        /// </summary>
        public int DiaryKeepRecentCount
        {
            get => MW.Set["ChatVPetProcess"].GetInt("DiaryKeepRecentCount", 10);
            set => MW.Set["ChatVPetProcess"][(gint)"DiaryKeepRecentCount"] = value;
        }

        /// <summary>
        /// 每次对话后对未被命中的日记条目施加的权重衰减率
        /// </summary>
        public float DiaryDecayRate
        {
            get => (float)MW.Set["ChatVPetProcess"].GetDouble("DiaryDecayRate", 0.03);
            set => MW.Set["ChatVPetProcess"][(gdbe)"DiaryDecayRate"] = value;
        }

    }
}
