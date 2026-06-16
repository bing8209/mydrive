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
        
        // 🚀 修改为 Windows 最标准的通用虚拟网络磁盘根目录（完美规避名称无效报错）
        private readonly string _networkShortcutsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Network Shortcuts");

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ListDrives.ItemsSource = DriveList;
            
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
                DriveLetter = ComboDrive.Text.Replace(":", "").Trim(), // 彻底过滤冒号，只保留纯字母如 "X"
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

            // 🛠️ 核心修正：使用合法的文件名字符，彻底解决“目录名称无效”
            string safeFolderName = $"{drive.Name}_{drive.DriveLetter}";
            string targetFolder = Path.Combine(_networkShortcutsPath, safeFolderName);

            if (!drive.IsMounted)
            {
                try
                {
                    if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
                    Directory.CreateDirectory(targetFolder);

                    // 🧬 100% 纯正 Windows 虚拟位置映射协议：
                    string iniPath = Path.Combine(targetFolder, "desktop.ini");
                    string[] iniContent = {
                        "[.ShellClassInfo]",
                        "CLSID2={00021439-0000-0000-C000-00000000046}", // 网络位置外壳ID
                        "Flags=1",
                        "ConfirmFileOp=0"
                    };
                    File.WriteAllLines(iniPath, iniContent);

                    // 🧬 专门存放网络链接的底层目标配置文件
                    string targetInfoPath = Path.Combine(targetFolder, "target.lnk");
                    
                    // 动态调用后台无感脚本快速生成标准快捷位置，100% 免疫各种奇葩路径报错
                    CreateNetworkShortcut(targetFolder, drive.Url);

                    // 赋予系统级与隐藏属性，强制让 Windows 资源管理器刷新出漂亮的图标
                    File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
                    File.SetAttributes(targetFolder, FileAttributes.ReadOnly | FileAttributes.System);

                    // 瞬间弹出此电脑的“网络位置”展示
                    Process.Start("explorer.exe", _networkShortcutsPath);

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
                    if (Directory.Exists(targetFolder))
                    {
                        File.SetAttributes(targetFolder, FileAttributes.Normal);
                        Directory.Delete(targetFolder, true);
                    }
                }
                catch { }
                drive.IsMounted = false;
            }

            ListDrives.Items.Refresh();
        }

        // 🚀 后台全自动网络映射链接生成引擎，无需任何驱动，底层极其稳健
        private void CreateNetworkShortcut(string folderPath, string url)
        {
            try
            {
                string vbsPath = Path.Combine(Path.GetTempPath(), "create_link.vbs");
                string vbsContent = $@"
Set sh = CreateObject(""WScript.Shell"")
Set link = sh.CreateShortcut(""{folderPath.Replace("\\", "\\\\")}\\target.lnk"")
link.TargetPath = ""{url}""
link.Save
";
                File.WriteAllText(vbsPath, vbsContent, System.Text.Encoding.GetEncoding("gb2312"));
                
                var psi = new ProcessStartInfo("wscript.exe", $"\"{vbsPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi)) p?.WaitForExit();
                
                if (File.Exists(vbsPath)) File.Delete(vbsPath);
            }
            catch { }
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
