using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json; // 换成微软官方内置的库

namespace MountTool {
    public class Account { public string Name { get; set; } public string Url { get; set; } public string User { get; set; } public string Pass { get; set; } }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(500, 400) };
            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 460, Height = 200 };
            Button btnSave = new Button { Text = "保存账号列表", Top = 220, Left = 10, Width = 200 };
            Button btnMount = new Button { Text = "挂载选中账号", Top = 220, Left = 220, Width = 200, BackColor = Color.LightGreen };

            string cfg = "config.json";
            if (File.Exists(cfg)) {
                var json = File.ReadAllText(cfg);
                dgv.DataSource = JsonSerializer.Deserialize<List<Account>>(json);
            }

            btnSave.Click += (s, e) => {
                var list = (List<Account>)dgv.DataSource;
                File.WriteAllText(cfg, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("已保存！");
            };

            btnMount.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null) return;
                var acc = (Account)row.DataBoundItem;
                Process.Start("net.exe", $"use * {acc.Url} /user:{acc.User} {acc.Pass} /persistent:no");
                MessageBox.Show("挂载命令已发送！");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnMount });
            Application.Run(f);
        }
    }
}
