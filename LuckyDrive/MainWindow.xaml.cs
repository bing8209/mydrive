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
                DriveLetter = ComboDrive.Text,
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
                    
                    var myFs = new LuckyWebDavFileSystem(drive.Url, drive.User, drive.Pass);
                    drive.Host = new FileSystemHost(myFs);
                    drive.Host.Mount(drive.DriveLetter, null, true, false);

                    drive.IsMounted = true;
                }
                catch (Exception ex)
                {
                    // 👇 核心修复：修改为正宗的 C# 弹窗标准语法，彻底消灭编译报错
                    MessageBox.Show($"盘符 {drive.DriveLetter} 真正挂载失败! \n请检查WinFsp驱动或网络。\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public string StatusText => IsMounted ? $"● 已映射到 {DriveLetter}" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }

    public class LuckyWebDavFileSystem : FileSystemBase
    {
        private readonly string _url;
        private readonly string _user;
        private readonly string _pass;
        private readonly HttpClient _http;

        public LuckyWebDavFileSystem(string url, string user, string pass)
        {
            _url = url.EndsWith("/") ? url : url + "/";
            _user = user;
            _pass = pass;

            _http = new HttpClient();
            var authToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
        }

        public override int Read(object fileDesc, IntPtr buffer, long offset, uint length, out uint bytesRead)
        {
            string fileName = (string)fileDesc;
            bytesRead = 0;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _url + fileName.TrimStart('\\').Replace('\\', '/'));
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, offset + length - 1);

                using (var response = _http.Send(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var data = response.Content.ReadAsByteArrayAsync().Result;
                        System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                        bytesRead = (uint)data.Length;
                        return STATUS_SUCCESS;
                    }
                }
                return STATUS_UNSUCCESSFUL;
            }
            catch
            {
                return STATUS_UNSUCCESSFUL;
            }
        }

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 50ULL * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 25ULL * 1024 * 1024 * 1024;
            volumeInfo.SetVolumeLabel("LuckyDrive");
            return STATUS_SUCCESS;
        }

        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileDesc)
        {
            fileDesc = fileName;
            return STATUS_SUCCESS;
        }
    }
}
