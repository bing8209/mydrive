using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using Fsp;

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var newDrive = new DriveConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = TxtName.Text,
                Url = TxtUrl.Text,
                User = TxtUser.Text,
                Pass = TxtPass.Password,
                DriveLetter = ComboDrive.Text.Contains(":") ? ComboDrive.Text : ComboDrive.Text + ":",
                IsMounted = false
            };

            DriveList.Add(newDrive);
            SaveConfig();
        }

        private void BtnToggleMount_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            string id = btn.Tag.ToString()!;
            var drive = DriveList.FirstOrDefault(d => d.Id == id);

            if (drive == null) return;

            if (!drive.IsMounted)
            {
                try
                {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    
                    // 🔌 真正的 WinFsp 内核驱动挂载，直接跳过恶心的 Windows 自动 WebClient 引擎
                    var myFs = new LuckyWebDavFileSystem(drive.Url, drive.User, drive.Pass);
                    drive.Host = new FileSystemHost(myFs);
                    
                    // 挂载参数：盘符、标签、是否留在前台（false代表完全后台线程驱动）
                    drive.Host.Mount(drive.DriveLetter, drive.Name, true, false);

                    drive.IsMounted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"驱动挂载失败! \n请确保您的电脑已经安装了 WinFsp 驱动硬件。\n原因：{ex.Message}", "错误");
                    drive.Host?.Dispose();
                    drive.Host = null;
                }
            }
            else
            {
                try
                {
                    drive.Host?.Unmount();
                    drive.Host?.Dispose();
                    drive.Host = null;
                }
                catch { }
                drive.IsMounted = false;
            }

            ListDrives.Items.Refresh();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListDrives.SelectedItem is DriveConfig selected)
            {
                if (selected.IsMounted)
                {
                    MessageBox.Show("请先断开该盘符的挂载，再进行删除！", "提示");
                    return;
                }
                DriveList.Remove(selected);
                SaveConfig();
            }
        }

        private void ListDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListDrives.SelectedItem is DriveConfig selected)
            {
                TxtName.Text = selected.Name;
                TxtUrl.Text = selected.Url;
                TxtUser.Text = selected.User;
                TxtPass.Password = selected.Pass;
                ComboDrive.Text = selected.DriveLetter;
            }
        }

        private void SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var saveList = DriveList.Select(d => new DriveConfig {
                    Id = d.Id, Name = d.Name, Url = d.Url, User = d.User, Pass = d.Pass, DriveLetter = d.DriveLetter
                }).ToList();
                File.WriteAllText(_configPath, JsonSerializer.Serialize(saveList, options));
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var list = JsonSerializer.Deserialize<System.Collections.Generic.List<DriveConfig>>(json);
                    if (list != null)
                    {
                        foreach (var item in list) DriveList.Add(item);
                    }
                }
            }
            catch { }
        }
    }

    public class DriveConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsMounted { get; set; } = false;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public FileSystemHost? Host { get; set; }

        public string StatusText => IsMounted ? $"● 已通过驱动映射到 {DriveLetter}" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
