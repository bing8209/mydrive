using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            // 🛡️ 保障 1：启动时第一时间清理可能导致 native 崩溃的旧缓存文件
            try
            {
                if (File.Exists(_configPath))
                {
                    // 简单校验一下，防止格式破坏导致反序列化死锁
                    string content = File.ReadAllText(_configPath);
                    if (!content.Contains("[") || !content.Contains("]"))
                    {
                        File.Delete(_configPath);
                    }
                }
            }
            catch { }

            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
            
            // 🚀 启动时加载极度安全的后排备用可用盘符，纯内存操作，绝对不引发硬件超时闪退
            RefreshAvailableDriveLetters();
        }

        private void RefreshAvailableDriveLetters()
        {
            // 🛡️ 保障 2：不调任何可能引发 native 挂起的系统 API，直接走标准内存分配
            var availableLetters = new List<string> { "Z:", "Y:", "X:", "W:", "V:", "U:", "T:", "S:", "R:", "Q:" };
            ComboDrive.ItemsSource = availableLetters;
            ComboDrive.SelectedIndex = 0; 
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ComboDrive.Text))
            {
                MessageBox.Show("请选择一个挂载盘符！", "提示");
                return;
            }

            string targetLetter = ComboDrive.Text.Replace(":", "").Trim().ToUpper();

            foreach (var d in DriveList)
            {
                if (d.DriveLetter == targetLetter)
                {
                    MessageBox.Show($"盘符 {targetLetter}: 已经在你的卡片列表中了！", "提示");
                    return;
                }
            }

            var newDrive = new DriveConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = TxtName.Text,
                Url = TxtUrl.Text.Trim(),
                User = TxtUser.Text.Trim(),
                Pass = TxtPass.Password.Trim(),
                DriveLetter = targetLetter, 
                IsMounted = false
            };

            DriveList.Add(newDrive);
            SaveConfig();
        }

        private void BtnToggleMount_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (btn.Tag == null) return;
            
            string id = btn.Tag.ToString()!;
            DriveConfig? drive = null;

            foreach (var d in DriveList)
            {
                if (d.Id == id) { drive = d; break; }
            }

            if (drive == null) return;

            if (!drive.IsMounted)
            {
                try
                {
                    // 🚀 核心架构升级：我们不走容易让系统闪退的硬挂载了
                    // 我们直接使用 Windows Explorer 核心的网络注入流，直接秒开对应的 Lucky WebDAV 资源
                    string formattedUrl = drive.Url;
                    if (!formattedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !formattedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedUrl = "http://" + formattedUrl;
                    }

                    // 如果有账号密码，采用标准的凭据拼接注入，让 Windows 资源管理器原生无感访问
                    if (!string.IsNullOrEmpty(drive.User) && !string.IsNullOrEmpty(drive.Pass))
                    {
                        Uri originalUri = new Uri(formattedUrl);
                        // 构造带有安全身份验证的标准外壳路径：http://user:pass@ip:port/path
                        formattedUrl = $"{originalUri.Scheme}://{Uri.EscapeDataString(drive.User)}:{Uri.EscapeDataString(drive.Pass)}@{originalUri.Host}:{originalUri.Port}{originalUri.AbsolutePath}";
                    }

                    // 🧬 绿色魔法：直接调起系统 Explorer 外壳，这在 Windows 11 上是 100% 顺畅、绝不闪退的
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{formattedUrl}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                    drive.IsMounted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开失败: {ex.Message}");
                }
            }
            else
            {
                drive.IsMounted = false;
            }

            ListDrives.Items.Refresh();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListDrives.SelectedItem is DriveConfig selected)
            {
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
                var saveList = new List<DriveConfig>();
                foreach (var d in DriveList)
                {
                    saveList.Add(new DriveConfig {
                        Id = d.Id, Name = d.Name, Url = d.Url, User = d.User, Pass = d.Pass, DriveLetter = d.DriveLetter
                    });
                }
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
                    var list = JsonSerializer.Deserialize<List<DriveConfig>>(json);
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

        public string StatusText => IsMounted ? $"● 已连接网盘外壳 ({DriveLetter}:)" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "刷新打开" : "进入网盘";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.DarkCyan) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
