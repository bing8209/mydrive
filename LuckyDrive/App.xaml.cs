using System;
using System.Windows;

namespace LuckyDrive
{
    // 🚀 建立一个纯 C# 的正统启动引擎，彻底跟 XAML 编译器说再见
    public class Program
    {
        [STAThread] // WPF 必须的单线程公寓标记
        public static void Main()
        {
            Application app = new Application();
            
            // 内存中直接实例化我们之前写好的纯 C# 渲染的主窗体
            MainWindow mainWindow = new MainWindow();
            
            // 启动程序并展示主界面
            app.Run(mainWindow);
        }
    }
}
