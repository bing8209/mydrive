using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using Fsp;

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        // 动态列表，绑定到界面左侧
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
        }

        // 保存/新建按钮点击
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var newDrive = new DriveConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = TxtName.Text,
                Url = TxtUrl.Text,
                User = TxtUser.Text,
                Pass = TxtPass.Password,
                DriveLetter = ComboDrive.Text,
                IsMounted = false
            };

            DriveList.Add(newDrive);
            SaveConfig();
        }

        // 左侧卡片右侧的 [挂载/断开] 独立开关
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
                    
                    var myFs = new LuckyWebDavFileSystem(drive.Url, drive.User, drive.Pass);
                    drive.Host = new FileSystemHost(myFs);
                    drive.Host.Mount(drive.DriveLetter, null, true, false);

                    drive.IsMounted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Value = MessageBox.Show($"盘符 {drive.DriveLetter} 真正挂载失败! \n请检查WinFsp驱动或网络。\n{ex.Message}", "错误");
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

            // 强行刷新左侧卡片外观
            ListDrives.Items.Refresh();
        }

        // 删除卡片
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

        // 切换选择左侧卡片时，把信息回显到右侧
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

        // 本地 JSON 配置存取
        private void SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                // 过滤失活的数据（不保存运行中的实体驱动句柄）
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

    /// <summary>
    /// 每个网络盘的独立数据模型
    /// </summary>
    public class DriveConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        
        // 运行状态控制变量（不需要保存进 JSON）
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsMounted { get; set; } = false;
        [System.Text.Json.Serialization.JsonIgnore]
        public FileSystemHost? Host { get; set; }

        // 供界面绑定显示的动态计算属性
        public string StatusText => IsMounted ? $"● 已成功映射到 {DriveLetter}" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }

    // =======================================================
    // LuckyWebDavFileSystem 类保持上一版不动，写在最末尾即可
    // =======================================================
    public class LuckyWebDavFileSystem : FileSystemBase { /* 保持跟上一版一模一样 */ }
}
