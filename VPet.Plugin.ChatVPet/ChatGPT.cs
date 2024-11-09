using ChatGPT.API.Framework;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using VPet_Simulator.Windows.Interface;
using LinePutScript.Localization.WPF;

namespace VPet.Plugin.ChatVPet
{
    public class ChatVPetPlugin : MainPlugin
    {
        public ChatVPetPlugin(IMainWindow mainwin) : base(mainwin) { }
        public ChatGPTClient? CGPTClient;
        public override void LoadPlugin()
        {
            if (File.Exists(ExtensionValue.BaseDirectory + @"\ChatVPetSetting.json"))
                CGPTClient = ChatGPTClient.Load(File.ReadAllText(ExtensionValue.BaseDirectory + @"\ChatVPetSetting.json"));
            MW.TalkAPI.Add(new ChatGPTTalkAPI(this));
            var menuItem = new MenuItem()
            {
                Header = "ChatVPetProcess".Translate(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            menuItem.Click += (s, e) => { Setting(); };
            MW.Main.ToolBar.MenuMODConfig.Items.Add(menuItem);
        }
        public override void Save()
        {
            if (CGPTClient != null)
                File.WriteAllText(ExtensionValue.BaseDirectory + @"\ChatGPTSetting.json", CGPTClient.Save());
        }
        public override void Setting()
        {
            new winSetting(this).ShowDialog();
        }
        public override string PluginName => "ChatVPetProcess";
        /// <summary>
        /// 是否在聊天位置显示Token数量
        /// </summary>
        public bool ShowToken
        {
            get => !MW.Set["CGPTV"][(gbol)"noshowtoken"];
            set => MW.Set["CGPTV"][(gbol)"noshowtoken"] = !value;
        }

        public bool AllowSubmit
        {
            get => MW.Set["CGPTV"][(gbol)"submit"];
            set => MW.Set["CGPTV"][(gbol)"submit"] = value;
        }
    }
}
