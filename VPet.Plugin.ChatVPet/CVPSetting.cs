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
            get => !MW.Set["CGPTV"][(gbol)"noshowtoken"];
            set => MW.Set["CGPTV"][(gbol)"noshowtoken"] = !value;
        }
        /// <summary>
        /// 是否允许提交
        /// </summary>
        public bool AllowSubmit
        {
            get => MW.Set["CGPTV"][(gbol)"submit"];
            set => MW.Set["CGPTV"][(gbol)"submit"] = value;
        }
        /// <summary>
        /// 知识库设置
        /// </summary>
        public string KnowledgeDataBase
        {
            get => MW.Set["CGPTV"][(gstr)"KnowledgeDataBase"] ?? "";
            set => MW.Set["CGPTV"][(gstr)"KnowledgeDataBase"] = value.Replace("\r", "");
        }
        /// <summary>
        /// 最大反复调用次数
        /// </summary>
        public int MaxRecallCount
        {
            get => MW.Set["CGPTV"].GetInt("MaxRecallCount", 5);
            set => MW.Set["CGPTV"][(gint)"MaxRecallCount"] = value;
        }
        /// <summary>
        /// 累计使用Token数量
        /// </summary>
        public int TokenCount
        {
            get => MW.Set["CGPTV"][(gint)"TokenCount"];
            set => MW.Set["CGPTV"][(gint)"TokenCount"] = value;
        }

        public string AzureKey
        {
            get => MW.Set["CGPTV"][(gstr)"AzureKey"] ?? "";
            set => MW.Set["CGPTV"][(gstr)"AzureKey"] = value;
        }
        public string AzureRegion
        {
            get => MW.Set["CGPTV"][(gstr)"AzureRegion"] ?? "";
            set => MW.Set["CGPTV"][(gstr)"AzureRegion"] = value;
        }
        public bool AzureVoiceEnable
        {
            get => MW.Set["CGPTV"][(gbol)"AzureVoiceEnable"];
            set => MW.Set["CGPTV"][(gbol)"AzureVoiceEnable"] = value;
        }
        public string AzureRecognitionLanguage
        {
            get
            {
                var lang = MW.Set["CGPTV"][(gstr)"AzureRecognitionLanguage"];
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
            set => MW.Set["CGPTV"][(gstr)"AzureRecognitionLanguage"] = value;
        }

        public int MaxHistoryCount
        {
            get => MW.Set["CGPTV"].GetInt("MaxHistoryCount", 20);
            set => MW.Set["CGPTV"][(gint)"MaxHistoryCount"] = value;
        }

        public int MaxToolCount
        {
            get => MW.Set["CGPTV"].GetInt("MaxToolCount", 10);
            set => MW.Set["CGPTV"][(gint)"MaxToolCount"] = value;
        }

        public int MaxKnowledgeCount
        {
            get => MW.Set["CGPTV"].GetInt("MaxKnowledgeCount", 10);
            set => MW.Set["CGPTV"][(gint)"MaxKnowledgeCount"] = value;
        }

        public int TotalTokensUsage
        {
            get => MW.Set["CGPTV"].GetInt("TotalTokensUsage");
            set => MW.Set["CGPTV"][(gint)"TotalTokensUsage"] = value;
        }

    }
}
