using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;

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
                DriveLetter = ComboDrive.Text.Trim(':'), // 确保只有字母
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
                // 👇 使用 Windows 自带的 net use 引擎挂载网络盘
                try
                {
                    // 格式化 URL，Windows 挂载 WebDAV 喜欢标准的 http/https 链接
                    string formattedUrl = drive.Url.Replace("\\", "/");
                    
                    // 构建命令: net use Z: "http://your-ip:port" "password" /user:"username" /persistent:no
                    string args = $"use {drive.DriveLetter}: \"{formattedUrl}\" \"{drive.Pass}\" /user:\"{drive.User}\" /persistent:no";
                    
                    var psi = new ProcessStartInfo("net", args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                        string error = process?.StandardError.ReadToEnd() ?? "";
                        
                        if (process?.ExitCode == 0 || error.Contains("已经连接") || error.Contains("1219"))
                        {
                            drive.IsMounted = true;
                        }
                        else
                        {
                            MessageBox.Show($"挂载失败！Windows 错误提示：\n{error}\n\n提示：请确保 Windows 的 WebClient 服务已开启。", "挂载失败");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"执行挂载命令时发生崩溃: {ex.Message}");
                }
            }
            else
            {
                // 👇 断开挂载: net use Z: /delete /y
                try
                {
                    string args = $"use {drive.DriveLetter}: /delete /y";
                    var psi = new ProcessStartInfo("net", args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                    }
                    drive.IsMounted = false;
                }
                catch { }
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
                ComboDrive.Text = selected.DriveLetter + ":";
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

        public string StatusText => IsMounted ? $"● 已映射到 {DriveLetter}:" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
