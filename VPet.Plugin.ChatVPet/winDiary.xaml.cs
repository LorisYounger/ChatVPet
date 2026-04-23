using ChatVPet.ChatProcess;
using LinePutScript.Localization.WPF;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VPet.Plugin.ChatVPet
{
    /// <summary>
    /// winDiary.xaml 的交互逻辑
    /// </summary>
    public partial class winDiary : Window
    {
        private readonly CVPPlugin plugin;

        /// <summary>
        /// ViewModel wrapping a DiaryEntry with display-friendly labels.
        /// </summary>
        private class DiaryViewModel
        {
            public DiaryEntry Entry { get; }
            public string DateLabel => Entry.RecordedAt.ToString("yyyy-MM-dd HH:mm");
            public string MetaLabel =>
                $"#{Entry.Id:x}  {"命中".Translate()}: {Entry.HitCount}  {"权重".Translate()}: {Entry.ImportanceWeight_Muti:F2}  {"衰减".Translate()}: {Entry.DecayFactor:F2}";
            public string Answer => Entry.Answer;

            public DiaryViewModel(DiaryEntry entry) => Entry = entry;
        }

        private ObservableCollection<DiaryViewModel> DiaryItems = new ObservableCollection<DiaryViewModel>();

        public winDiary(CVPPlugin plugin)
        {
            InitializeComponent();
            Resources = Application.Current.Resources;
            this.plugin = plugin;
            LoadDiary();
        }

        private void LoadDiary()
        {
            DiaryItems = new ObservableCollection<DiaryViewModel>(
                plugin.VPetChatProcess.DiaryEntries
                    .OrderByDescending(e => e.RecordedAt)
                    .Select(e => new DiaryViewModel(e)));

            lbDiary.ItemsSource = DiaryItems;
            tbCount.Text = "共 {0} 条日记".Translate(DiaryItems.Count);
        }

        private void MenuItem_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.CommandParameter is DiaryViewModel vm)
            {
                plugin.VPetChatProcess.DiaryEntries.Remove(vm.Entry);
                plugin.Save();
                LoadDiary();
                MessageBox.Show("删除成功".Translate());
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
