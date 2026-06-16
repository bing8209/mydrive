using System;
using System.Windows;

namespace LuckyDrive
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 🚀 核心关键：手动在内存里强行实例并展示我们纯 C# 的 MainWindow 窗口
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
