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
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        // 核心组件释放路径
        private readonly string rclonePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
        private readonly string msiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winfsp_install.msi");
        private Dictionary<string, int> _activeMounts = new Dictionary<string, int>();

        // --- 声明界面控件 ---
        private Label lblUrl = new Label() { Text = "Lucky WebDAV 地址:", Left = 20, Top = 25, Width = 110 };
        private TextBox txtUrl = new TextBox() { Left = 140, Top = 22, Width = 280, Text = "http://127.0.0.1:18800/aaa" };

        private Label lblUser = new Label() { Text = "WebDAV 用户名:", Left = 20, Top = 65, Width = 110 };
        private TextBox txtUser = new TextBox() { Left = 140, Top = 62, Width = 280, Text = "bing" };

        private Label lblPass = new Label() { Text = "WebDAV 密  码:", Left = 20, Top = 105, Width = 110 };
        private TextBox txtPass = new TextBox() { Left = 140, Top = 102, Width = 280, PasswordChar = '*', Text = "" };

        private Label lblDrive = new Label() { Text = "选择挂载盘符:", Left = 20, Top = 145, Width = 110 };
        private ComboBox cmbDrive = new ComboBox() { Left = 140, Top = 142, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };

        private Button btnAction = new Button() { Text = "🚀 立即挂载", Left = 140, Top = 190, Width = 120, Height = 35 };
        private Button btnDisconnect = new Button() { Text = "❌ 断开连接", Left = 280, Top = 190, Width = 120, Height = 35 };

        public MainForm()
        {
            // 窗体基础样式美化
            this.Text = "Lucky WebDAV 定制挂载器 v1.0";
            this.Width = 470;
            this.Height = 290;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;

            // 初始化盘符下拉菜单（从 Z 倒着排到 H，防止冲突）
            string[] drives = { "Z:", "Y:", "X:", "W:", "V:", "U:", "T:", "S:", "R:", "Q:", "P:", "O:", "N:", "M:", "L:", "K:", "J:", "I:", "Current" };
            cmbDrive.Items.AddRange(drives);
            cmbDrive.SelectedIndex = 0; // 默认选中 Z:

            // 将所有新加的控件加载到窗体中
            this.Controls.Add(lblUrl); this.Controls.Add(txtUrl);
            this.Controls.Add(lblUser); this.Controls.Add(txtUser);
            this.Controls.Add(lblPass); this.Controls.Add(txtPass);
            this.Controls.Add(lblDrive); this.Controls.Add(cmbDrive);
            this.Controls.Add(btnAction); this.Controls.Add(btnDisconnect);

            // 绑定生命周期与核心事件
            this.Load += MainForm_Load;
            InitializeMountEvents();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // 启动瞬间自动从 Exe 内部释放组件
            ExtractInternalResource("rclone.exe", rclonePath);

            // 驱动环境检测
            if (!CheckIfWinFspInstalled())
            {
                var result = MessageBox.Show(
                    "检测到您的电脑尚未安装虚拟磁盘驱动（WinFsp）。\n\n点击“确定”将自动为您拉起轻量安装包，完成后即可正常挂载！", 
                    "首次运行提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

                if (result == DialogResult.OK)
                {
                    try {
                        ExtractInternalResource("winfsp.msi", msiPath);
                        Process p = Process.Start("msiexec.exe", $"/i \"{msiPath}\" /passive");
                        p.WaitForExit();
                        if (File.Exists(msiPath)) File.Delete(msiPath);
                        
                        if (CheckIfWinFspInstalled()) MessageBox.Show("驱动环境初始化成功！", "提示");
                    }
                    catch (Exception ex) {
                        MessageBox.Show("驱动安装失败，请手动安装: " + ex.Message, "错误");
                    }
                }
            }
        }

        private void InitializeMountEvents()
        {
            // 【挂载点击事件】
            btnAction.Click += (s, e) => {
                string url = txtUrl.Text.Trim();
                string user = txtUser.Text.Trim();
                string pass = txtPass.Text;
                string targetDrive = cmbDrive.SelectedItem?.ToString() ?? "Z:";

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(user)) {
                    MessageBox.Show("请先完整填写 Lucky 地址和用户名！", "提示");
                    return;
                }

                // 检查是否重复挂载同一个盘符
                if (_activeMounts.ContainsKey(targetDrive.ToUpper())) {
                    MessageBox.Show($"盘符 {targetDrive} 已经在挂载中，请勿重复操作！", "提示");
                    return;
                }

                try {
                    // 动态加密输入的密码
                    string obscuredPass = ObscurePassword(pass);

                    // 构造动态 rclone 挂载参数
                    string arguments = $"mount :webdav: {targetDrive} --webdav-url \"{url}\" --webdav-user \"{user}\" --webdav-pass \"{obscuredPass}\" --vfs-cache-mode off --network-mode";

                    ProcessStartInfo psi = new ProcessStartInfo(rclonePath, arguments) {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    Process? p = Process.Start(psi);
                    if (p != null) {
                        _activeMounts[targetDrive.ToUpper()] = p.Id; // 记录盘符和对应的进程ID
                        MessageBox.Show($"盘符 {targetDrive} 挂载命令已发出！\n请去「此电脑」中查看是否成功出现网络驱动器。", "成功");
                    }
                }
                catch (Exception ex) {
                    MessageBox.Show("挂载执行失败: " + ex.Message, "错误");
                }
            };

            // 【断开点击事件】
            btnDisconnect.Click += (s, e) => {
                string targetDrive = cmbDrive.SelectedItem?.ToString() ?? "Z:";
                
                if (_activeMounts.TryGetValue(targetDrive.ToUpper(), out int pid)) {
                    try {
                        // 结束 rclone 进程，盘符自动安全消失
                        Process.GetProcessById(pid).Kill();
                        _activeMounts.Remove(targetDrive.ToUpper());
                        MessageBox.Show($"盘符 {targetDrive} 已成功断开。");
                    } 
                    catch { 
                        _activeMounts.Remove(targetDrive.ToUpper()); 
                    }
                } else {
                    MessageBox.Show($"本地记录中未发现 {targetDrive} 的挂载进程，您可以尝试直接在资源管理器里右键断开。", "提示");
                }
            };
        }

        // 动态加密密码的核心算法
        private string ObscurePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return "";
            ProcessStartInfo psi = new ProcessStartInfo(rclonePath, $"obscure \"{plainPassword}\"") {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (Process? p = Process.Start(psi)) {
                if (p == null) return "";
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output.Trim();
            }
        }

        // 内嵌资源动态提取
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

        // 驱动注册表检测
        private bool CheckIfWinFspInstalled()
        {
            using (RegistryKey? key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinFsp"))
            using (RegistryKey? key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\WinFsp"))
            {
                return (key64 != null || key32 != null);
            }
        }
    }
}
