using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
            
            // 启动时自动刷新下拉框，只把没被占用的空闲盘符展现出来
            RefreshAvailableDriveLetters();
        }

        // 🚀 核心黑科技：扫描全盘，把 A-Z 已经被占用的全部剔除
        private void RefreshAvailableDriveLetters()
        {
            try
            {
                var existingDrives = DriveInfo.GetDrives().Select(d => d.Name.Substring(0, 1).ToUpper()).ToHashSet();
                
                var availableLetters = new List<string>();
                for (char c = 'Z'; c >= 'A'; c--) // 从 Z 倒着往前排
                {
                    string letter = c.ToString();
                    if (!existingDrives.Contains(letter))
                    {
                        availableLetters.Add(letter + ":");
                    }
                }

                // 把过滤后的空闲盘符喂给界面的下拉选择框
                ComboDrive.ItemsSource = availableLetters;
                if (availableLetters.Count > 0)
                {
                    ComboDrive.SelectedIndex = 0; 
                }
            }
            catch
            {
                ComboDrive.ItemsSource = new List<string> { "X:", "Y:", "Z:", "W:", "V:", "U:" };
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ComboDrive.Text))
            {
                MessageBox.Show("请选择或输入一个可用的盘符字母！", "提示");
                return;
            }

            var newDrive = new DriveConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = TxtName.Text,
                Url = TxtUrl.Text.Trim(),
                User = TxtUser.Text.Trim(),
                Pass = TxtPass.Password.Trim(),
                DriveLetter = ComboDrive.Text.Replace(":", "").Trim().ToUpper(), 
                IsMounted = false
            };

            if (DriveList.Any(d => d.DriveLetter == newDrive.DriveLetter))
            {
                MessageBox.Show($"盘符 {newDrive.DriveLetter}: 已经在你的网盘卡片列表中了，请换个字母！", "提示");
                return;
            }

            DriveList.Add(newDrive);
            SaveConfig();
            RefreshAvailableDriveLetters();
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
                    string uriString = drive.Url;
                    if (!uriString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        uriString = "http://" + uriString;
                    }

                    Uri uri = new Uri(uriString);
                    string host = uri.Host;
                    int port = uri.Port;
                    string path = uri.AbsolutePath.Replace("/", "\\").Trim('\\');

                    string uncPath = (port == 80 || port == 443) 
                        ? $"\\\\{host}\\{path}" 
                        : $"\\\\{host}@{port}\\{path}";

                    string args = $"use {drive.DriveLetter}: \"{uncPath}\" \"{drive.Pass}\" /user:\"{drive.User}\" /persistent:no";
                    
                    var psi = new ProcessStartInfo("net", args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                        string error = process?.StandardError.ReadToEnd() ?? "";

                        if (process?.ExitCode == 0 || error.Contains("1219") || error.Contains("已经连接"))
                        {
                            drive.IsMounted = true;
                            Process.Start("explorer.exe", $"{drive.DriveLetter}:");
                        }
                        else
                        {
                            if (error.Contains("67") || error.Contains("网络名"))
                            {
                                MessageBox.Show("挂载失败！检测到您的 Windows 系统未开启 WebClient 组件。\n\n解决办法：\n请在键盘按 Win+R 输入 services.msc，把【WebClient】服务的启动类型改为【自动】并点击【启动】即可！", "提示");
                            }
                            else
                            {
                                MessageBox.Show($"挂载失败，Windows 系统内核提示：\n{error}", "提示");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"挂载崩溃: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    string args = $"use {drive.DriveLetter}: /delete /y";
                    var psi = new ProcessStartInfo("net", args) { CreateNoWindow = true, UseShellExecute = false };
                    using (var process = Process.Start(psi)) process?.WaitForExit();
                    
                    drive.IsMounted = false;
                }
                catch { }
            }

            ListDrives.Items.Refresh();
            RefreshAvailableDriveLetters(); 
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListDrives.SelectedItem is DriveConfig selected)
            {
                if (selected.IsMounted)
                {
                    MessageBox.Show("请先断开该盘符的连接，再进行删除！", "提示");
                    return;
                }
                DriveList.Remove(selected);
                SaveConfig();
                RefreshAvailableDriveLetters();
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

        public string StatusText => IsMounted ? $"● 已映射到虚拟磁盘 {DriveLetter}:" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
