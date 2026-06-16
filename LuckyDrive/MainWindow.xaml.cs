using System;
using System.Windows;
using System.Windows.Media;
using Fsp; // 必须依赖电脑里安装的 WinFsp 驱动

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        private FileSystemHost? _fsHost;
        private bool _isMounted = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMounted)
            {
                try
                {
                    string driveLetter = ComboDrive.Text; // 例如 "Z:"
                    
                    // 1. 这里初始化你自定义的零缓存 WebDAV 文件系统逻辑
                    // (实际完整开发需要实现并传入继承自 FileSystemBase 的类，此处做骨架演示)
                    // var myFileSystem = new MyWebDavFileSystem(TxtUrl.Text, TxtUser.Text, TxtPass.Password);
                    
                    // 2. 召唤 WinFsp 注入 Windows 核心
                    // _fsHost = new FileSystemHost(myFileSystem);
                    // _fsHost.Mount(driveLetter, null, true, false);

                    // 模拟成功状态
                    LblStatus.Text = $"状态: 已成功挂载至 {driveLetter} 盘";
                    LblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                    BtnConnect.Content = "断开连接";
                    BtnConnect.Background = new SolidColorBrush(Colors.Crimson);
                    _isMounted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"挂载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // 断开挂载
                _fsHost?.Unmount();
                _fsHost?.Dispose();
                _fsHost = null;

                LblStatus.Text = "状态: 未连接";
                LblStatus.Foreground = new SolidColorBrush(Colors.Orange);
                BtnConnect.Content = "立即挂载";
                BtnConnect.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                _isMounted = false;
            }
        }
    }
}
