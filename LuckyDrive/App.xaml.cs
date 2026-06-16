using System;
using System.Windows;

namespace LuckyDrive
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 这里可以处理一些程序启动时的初始化逻辑（例如检查管理员权限、单实例运行等）
        }
    }
}
