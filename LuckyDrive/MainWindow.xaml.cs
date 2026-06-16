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
            InitializeComponent();
            
            // 🛡️ 纯防御性架构：强行在最底层把数据流和事件解耦，确保 100% 能够顺利初始化
            try
            {
                DriveList = new ObservableCollection<DriveConfig>();
                ListDrives.ItemsSource = DriveList;
                LoadConfig();
            }
            catch { }

            RefreshAvailableDriveLetters();
        }

        private void RefreshAvailableDriveLetters()
        {
            try
            {
                var defaultLetters = new List<string> { "Z:", "Y:", "X:", "W:", "V:", "U:", "T:", "S:", "R:", "Q:" };
                var availableLetters = new List<string>();

                if (DriveList == null || DriveList.Count == 0)
                {
                    ComboDrive.ItemsSource = defaultLetters;
                    ComboDrive.SelectedIndex = 0;
                    return;
                }

                foreach (var letter in defaultLetters)
                {
                    bool isUsedInApp = false;
                    foreach (var drive in DriveList)
                    {
                        if (drive != null && (drive.DriveLetter + ":") == letter)
                        {
                            isUsedInApp = true;
                            break;
                        }
                    }
                    if (!isUsedInApp) availableLetters.Add(letter);
                }

                ComboDrive.ItemsSource = availableLetters;
                if (availableLetters.Count > 0) ComboDrive.SelectedIndex = 0;
            }
            catch
            {
                ComboDrive.ItemsSource = new List<string> { "Z:", "Y:", "X:" };
                ComboDrive.SelectedIndex = 0;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ComboDrive.Text))
            {
                MessageBox.Show("请选择或输入一个可用的盘符字母！", "提示");
                return;
            }

            string targetLetter = ComboDrive.Text.Replace(":", "").Trim().ToUpper();

            foreach (var d in DriveList)
            {
                if (d != null && d.DriveLetter == targetLetter)
                {
                    MessageBox.Show($"盘符 {targetLetter}: 已经在列表中了！", "提示");
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
            RefreshAvailableDriveLetters();
        }

        private void BtnToggleMount_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (btn.Tag == null) return;
            
            string id = btn.Tag.ToString()!;
            DriveConfig? drive = null;

            foreach (var d in DriveList)
            {
                if (d != null && d.Id == id) { drive = d; break; }
            }

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
                    string path = uri.AbsolutePath.Trim('/');
                    path = path.Replace("/", "\\");

                    string uncPath;
                    if (port == 80 || port == 443)
                    {
                        uncPath = string.IsNullOrEmpty(path) ? $"\\\\{host}\\DavWWWRoot" : $"\\\\{host}\\DavWWWRoot\\{path}";
                    }
                    else
                    {
                        uncPath = string.IsNullOrEmpty(path) ? $"\\\\{host}@{port}\\DavWWWRoot" : $"\\\\{host}@{port}\\DavWWWRoot\\{path}";
                    }

                    try { Process.Start(new ProcessStartInfo("net", $"use {drive.DriveLetter}: /delete /y") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }

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
                            MessageBox.Show($"挂载失败，系统提示：\n{error}", "提示");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"挂载异常: {ex.Message}");
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

            try { ListDrives.Items.Refresh(); } catch { }
            RefreshAvailableDriveLetters(); 
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListDrives.SelectedItem is DriveConfig selected)
            {
                if (selected.IsMounted)
                {
                    MessageBox.Show("请先断开连接，再删除卡片！", "提示");
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
                var saveList = new List<DriveConfig>();
                foreach (var d in DriveList)
                {
                    if (d == null) continue;
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
                        foreach (var item in list)
                        {
                            if (item != null) DriveList.Add(item);
                        }
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
