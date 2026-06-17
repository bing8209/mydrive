using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;

namespace MountTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm()); // 启动下面定义的单文件窗体
        }
    }

    // 将所有的挂载器逻辑、UI 绘制、资源释放全部闭环在这个类中
    public class MainForm : Form
    {
        private readonly string rclonePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
        private readonly string msiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winfsp_install.msi");
        private Dictionary<string, int> _activeMounts = new Dictionary<string, int>();

        // 你的 UI 控件定义（例如 DataGridView、Button 等，这里按你原本的加）
        private Button btnAction = new Button() { Text = "挂载", Top = 20, Left = 20 };
        private Button btnDisconnect = new Button() { Text = "断开", Top = 20, Left = 120 };

        public MainForm()
        {
            // 窗体基础设置
            this.Text = "Lucky WebDAV 定制挂载器";
            this.Width = 600;
            this.Height = 400;
            this.Controls.Add(btnAction);
            this.Controls.Add(btnDisconnect);

            // 绑定事件
            this.Load += MainForm_Load;
            InitializeMountEvents();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // 1. 运行瞬间，去 Exe 肚子里把 rclone.exe 吐出来
            ExtractInternalResource("rclone.exe", rclonePath);

            // 2. 检查对方系统装没装 WinFsp 驱动
            if (!CheckIfWinFspInstalled())
            {
                var result = MessageBox.Show(
                    "检测到您的电脑尚未安装虚拟磁盘驱动（WinFsp）。\n\n点击“确定”将自动为您拉起静默安装，完成后即可正常挂载！", 
                    "首次运行提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

                if (result == DialogResult.OK)
                {
                    try {
                        ExtractInternalResource("winfsp.msi", msiPath);
                        Process p = Process.Start("msiexec.exe", $"/i \"{msiPath}\" /passive");
                        p.WaitForExit();
                        if (File.Exists(msiPath)) File.Delete(msiPath); // 装完就把临时msi删了
                        
                        if (CheckIfWinFspInstalled()) MessageBox.Show("驱动安装成功！");
                    }
                    catch (Exception ex) {
                        MessageBox.Show("驱动安装失败: " + ex.Message);
                    }
                }
            }
        }

        private void InitializeMountEvents()
        {
            // 挂载按钮事件
            btnAction.Click += (s, e) => {
                // 这里写你从 Lucky 获取动态数据的逻辑，以下为核心挂载伪代码：
                string url = "https://你的Lucky反代域名:端口/aaa"; 
                string targetDrive = "Z:"; 

                string arguments = $"mount :webdav: {targetDrive} --webdav-url \"{url}\" --webdav-user \"bing\" --webdav-pass \"{ObscurePassword("你的WebDAV密码")}\" --vfs-cache-mode off --network-mode";

                ProcessStartInfo psi = new ProcessStartInfo(rclonePath, arguments) {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process p = Process.Start(psi);
                _activeMounts[targetDrive.ToUpper()] = p.Id; // 记录进程ID
                MessageBox.Show($"{targetDrive} 挂载成功！支持截图直存，且C盘零缓存！");
            };

            // 断开按钮事件
            btnDisconnect.Click += (s, e) => {
                string targetDrive = "Z:";
                if (_activeMounts.TryGetValue(targetDrive.ToUpper(), out int pid)) {
                    try {
                        Process.GetProcessById(pid).Kill();
                        _activeMounts.Remove(targetDrive.ToUpper());
                        MessageBox.Show($"盘符 {targetDrive} 已成功断开。");
                    } catch { _activeMounts.Remove(targetDrive.ToUpper()); }
                }
            };
        }

        // 核心：流式读取独立标签资源
        private void ExtractInternalResource(string resourceName, string outputPath)
        {
            if (File.Exists(outputPath)) return;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return;
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        private bool CheckIfWinFspInstalled()
        {
            using (RegistryKey? key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinFsp"))
            using (RegistryKey? key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\WinFsp"))
            {
                return (key64 != null || key32 != null);
            }
        }

        private string ObscurePassword(string plainPassword)
        {
            ProcessStartInfo psi = new ProcessStartInfo(rclonePath, $"obscure \"{plainPassword}\"") {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (Process p = Process.Start(psi)) {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output.Trim();
            }
        }
    }
}
