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
                // 🛠️ 终极修正：将 .HashSet() 修正为 .ToHashSet()，完美通过编译
                var existingDrives = DriveInfo.GetDrives().Select(d => d.Name.Substring(0, 1).ToUpper()).ToHashSet();
                
                var availableLetters = new List<string>();
                for (char c = 'Z'; c >= 'A'; c--) // 从 Z 倒着往前排，通常后面的字母更不容易被硬件占用
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
                    ComboDrive.SelectedIndex = 0; // 默认自动帮你选中第一个绝对可用的空闲盘符
                }
            }
            catch
            {
                // 备用方案：如果扫描失败，依然显示默认的几个
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
                DriveLetter = ComboDrive.Text.Replace(":", "").Trim().ToUpper(), // 剥离冒号，锁死单字母
                IsMounted = false
            };

            // 检查是不是跟现有的卡片盘符冲突了
            if (DriveList.Any(d => d.DriveLetter == newDrive.DriveLetter))
            {
                MessageBox.Show($"盘符 {newDrive.DriveLetter}: 已经在你的网盘卡片列表中了，请换个字母！", "提示");
                return;
            }

            DriveList.Add(newDrive);
            SaveConfig();
            
            // 刷新一下可用列表
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
                            Process.
