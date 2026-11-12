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
        private readonly VPetChatProcess process;

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
            process = plugin.VPetChatProcess;
            Loaded += async (_, _) => await LoadDiary();
        }

        private async System.Threading.Tasks.Task LoadDiary()
        {
            var entries = (await System.Threading.Tasks.Task.Run(process.ReadMemories)).Diary;
            DiaryItems = new ObservableCollection<DiaryViewModel>(
                entries
                    .OrderByDescending(e => e.RecordedAt)
                    .Select(e => new DiaryViewModel(e)));

            lbDiary.ItemsSource = DiaryItems;
            tbCount.Text = "共 {0} 条日记".Translate(DiaryItems.Count);
        }

        private async void MenuItem_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.CommandParameter is DiaryViewModel vm)
            {
                await System.Threading.Tasks.Task.Run(() => process.DeleteMemory(vm.Entry));
                await LoadDiary();
                MessageBox.Show("删除成功".Translate());
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
