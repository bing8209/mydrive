using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;

namespace MountTool {
    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "WebDAV 挂载器", Size = new Size(350, 250), StartPosition = FormStartPosition.CenterScreen };
            
            TextBox txtUrl = new TextBox { PlaceholderText = "WebDAV 地址", Top = 20, Left = 20, Width = 300 };
            TextBox txtUser = new TextBox { PlaceholderText = "账号", Top = 50, Left = 20, Width = 300 };
            TextBox txtPass = new TextBox { PlaceholderText = "密码", Top = 80, Left = 20, Width = 300, PasswordChar = '*' };
            Button btn = new Button { Text = "开始挂载", Top = 120, Left = 20, Width = 300 };

            btn.Click += (s, e) => {
                string args = $"use Z: {txtUrl.Text} /user:{txtUser.Text} {txtPass.Text} /persistent:no";
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", args) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                Process.Start(psi);
                MessageBox.Show("挂载命令已发送至系统！");
            };

            f.Controls.AddRange(new Control[] { txtUrl, txtUser, txtPass, btn });
            Application.Run(f);
        }
    }
}
