using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace MountTool {
    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "WebDAV 挂载器", Size = new Size(350, 250), StartPosition = FormStartPosition.CenterScreen };
            
            TextBox txtUrl = new TextBox { PlaceholderText = "WebDAV 地址", Top = 20, Left = 20, Width = 300 };
            TextBox txtUser = new TextBox { PlaceholderText = "账号", Top = 50, Left = 20, Width = 300 };
            TextBox txtPass = new TextBox { PlaceholderText = "密码", Top = 80, Left = 20, Width = 300, PasswordChar = '*' };
            Button btn = new Button { Text = "自动挂载到空闲盘符", Top = 120, Left = 20, Width = 300 };

            btn.Click += (s, e) => {
                // 1. 自动寻找空闲盘符
                var usedDrives = Environment.GetLogicalDrives().Select(d => d.Substring(0, 1)).ToList();
                string targetDrive = "";
                for (char c = 'V'; c >= 'E'; c--) {
                    if (!usedDrives.Contains(c.ToString())) {
                        targetDrive = c + ":";
                        break;
                    }
                }

                // 2. 执行挂载
                string args = $"use {targetDrive} {txtUrl.Text} /user:{txtUser.Text} {txtPass.Text} /persistent:no";
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", args) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                
                try {
                    Process.Start(psi);
                    MessageBox.Show($"成功挂载到 {targetDrive} 盘！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } catch (Exception ex) {
                    MessageBox.Show("错误: " + ex.Message);
                }
            };

            f.Controls.AddRange(new Control[] { txtUrl, txtUser, txtPass, btn });
            Application.Run(f);
        }
    }
}
