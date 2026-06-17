using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace MountTool {
    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form form = new Form { Text = "WebDAV 挂载器", Width = 300, Height = 250 };
            TextBox txtUrl = new TextBox { Top = 20, Left = 20, Width = 250, Text = "WebDAV 地址" };
            TextBox txtUser = new TextBox { Top = 50, Left = 20, Width = 250, Text = "账号" };
            Button btn = new Button { Top = 90, Left = 20, Text = "开始挂载", Width = 250 };
            btn.Click += (s, e) => {
                Process.Start("net.exe", $"use Z: {txtUrl.Text} /user:{txtUser.Text} /persistent:no");
                MessageBox.Show("已执行挂载指令");
            };
            form.Controls.Add(txtUrl); form.Controls.Add(txtUser); form.Controls.Add(btn);
            Application.Run(form);
        }
    }
}
