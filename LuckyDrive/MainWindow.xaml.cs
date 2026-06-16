using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using System.Diagnostics;

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        
        // Windows 系统存放网络位置快捷磁盘的专用秘密通道
        private readonly string _networkShortcutsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Network Shortcuts");

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
            
            // 确保系统的网络快捷方式目录存在
            if (!Directory.Exists(_networkShortcutsPath)) Directory.CreateDirectory(_networkShortcutsPath);
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

            // 每一个网盘在“此电脑”里对应的虚拟磁盘文件夹名字
            string folderName = $"{drive.Name} ({drive.DriveLetter})";
            string targetFolder = Path.Combine(_networkShortcutsPath, folderName);

            if (!drive.IsMounted)
            {
                try
                {
                    if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
                    Directory.CreateDirectory(targetFolder);

                    // 🛠️ 纯 C# 绿色魔法：向 Windows 写入标准的虚拟网络位置元数据文件（desktop.ini）
                    // 这样 Windows 就会自动把它识别为带图标的“网络虚拟磁盘”，而且不需要任何驱动！
                    string iniPath = Path.Combine(targetFolder, "desktop.ini");
                    string[] iniContent = {
                        "[.ShellClassInfo]",
                        "CLSID2={00021439-0000-0000-C000-00000000046}", // 告诉 Windows 这是一个标准的网络连接外壳
                        $"Flags=1",
                        $"TargetInfo=URL={drive.Url}"
                    };
                    File.WriteAllLines(iniPath, iniContent);
                    
                    // 设置只读和系统属性，强迫 Windows 刷新资源管理器里的卡片外观
                    File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
                    File.SetAttributes(targetFolder, FileAttributes.ReadOnly);

                    // 自动拉起资源管理器展示给用户看
                    Process.Start("explorer.exe", targetFolder);

                    drive.IsMounted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"虚拟映射失败! \n原因：{ex.Message}", "错误");
                }
            }
            else
            {
                try
                {
                    // 断开挂载：直接物理移除这个虚拟网络外壳卡片，Windows 会瞬间把它从“此电脑”里拔掉
                    if (Directory.Exists(targetFolder))
                    {
                        // 先恢复属性才能顺利删除
                        File.SetAttributes(targetFolder, FileAttributes.Normal);
                        Directory.Delete(targetFolder, true);
                    }
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
                    MessageBox.Show("请先断开该盘符的连接，再进行删除！", "提示");
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

        public string StatusText => IsMounted ? $"● 已连接到网络位置 ({DriveLetter})" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "连接";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
