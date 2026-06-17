using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace MountTool
{
    public partial class MainForm : Form
    {
        // 用于存储 盘符 -> rclone进程ID 的映射，方便断开时精准杀死进程
        private Dictionary<string, int> _activeMounts = new Dictionary<string, int>();

        public MainForm()
        {
            InitializeComponent();
            InitializeCustomEvents();
        }

        private void InitializeCustomEvents()
        {
            // =================【 1. 挂载按钮逻辑 】=================
            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;

                string url = row.Cells[1].Value?.ToString()?.Trim() ?? "";
                string user = row.Cells[2].Value?.ToString()?.Trim() ?? "";
                string pass = row.Cells[3].Value?.ToString()?.Trim() ?? "";
                string drv = row.Cells[4].Value?.ToString()?.Trim() ?? "";

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(drv)) {
                    MessageBox.Show("请检查输入：地址和盘符不能为空！");
                    return;
                }

                string targetDrive = drv.EndsWith(":") ? drv : drv + ":";

                // 检查本地目录下是否存在内核文件
                string rclonePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
                if (!File.Exists(rclonePath)) {
                    MessageBox.Show("错误：未在本程序目录下找到核心模块 rclone.exe！", "缺少依赖", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try {
                    // 1. 异步将明文密码转化为 rclone 认识的密文
                    string obscuredPass = ObscurePassword(rclonePath, pass);

                    // 2. 组装免配置文件参数
                    // --vfs-cache-mode off  => 核心：强制实时流式传输，本地绝不产生垃圾缓存，C盘不爆红
                    // --network-mode        => 伪装成网络本地盘，提高 Windows 系统的写入兼容性
                    string arguments = $"mount :webdav: {targetDrive} " +
                                       $"--webdav-url \"{url}\" " +
                                       $"--webdav-user \"{user}\" " +
                                       $"--webdav-pass \"{obscuredPass}\" " +
                                       $"--vfs-cache-mode off " + 
                                       $"--network-mode";

                    ProcessStartInfo psi = new ProcessStartInfo(rclonePath, arguments) {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    // 3. 启动后台引擎
                    Process p = Process.Start(psi);
                    
                    // 4. 记录盘符与进程，以便后续管理
                    _activeMounts[targetDrive.ToUpper()] = p.Id;
                    
                    MessageBox.Show($"盘符 {targetDrive} 挂载成功！\n现在你可以正常写入、截图或解压文件了。", "提示");
                }
                catch (Exception ex) {
                    MessageBox.Show($"挂载失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // =================【 2. 断开按钮逻辑 】=================
            btnDisconnect.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null) return;
                string drv = row.Cells[4].Value?.ToString()?.Trim() ?? "";
                string targetDrive = (drv.EndsWith(":") ? drv : drv + ":").ToUpper();

                // 寻找该盘符对应的后台进程并进行清理
                if (_activeMounts.TryGetValue(targetDrive, out int pid)) {
                    try {
                        Process p = Process.GetProcessById(pid);
                        p.Kill(); // 结束进程，WinFsp 驱动会自动瞬间卸载该虚拟盘符
                        _activeMounts.Remove(targetDrive);
                        MessageBox.Show($"盘符 {targetDrive} 已成功断开连接。");
                    }
                    catch {
                        // 进程可能已经被意外关闭
                        _activeMounts.Remove(targetDrive);
                    }
                } else {
                    MessageBox.Show("该盘符未通过本工具挂载，或已处于断开状态。");
                }
            };
        }

        // 辅助方法：调用 rclone 自带的加密功能获取混淆密码
        private string ObscurePassword(string rclonePath, string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return "";
            
            ProcessStartInfo psi = new ProcessStartInfo(rclonePath, $"obscure \"{plainPassword}\"") {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (Process p = Process.Start(psi)) {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output.Trim();
            }
        }
    }
}
