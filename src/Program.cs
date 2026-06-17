using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace MountTool {
    public class Account { public string Name { get; set; } = ""; public string Url { get; set; } = ""; public string User { get; set; } = ""; public string Pass { get; set; } = ""; }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(500, 350), StartPosition = FormStartPosition.CenterScreen };
            
            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 460, Height = 200, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
            dgv.Columns.Add("Name", "名称");
            dgv.Columns.Add("Url", "WebDAV 地址");
            dgv.Columns.Add("User", "账号");
            dgv.Columns.Add("Pass", "密码");

            Button btnSave = new Button { Text = "保存账号列表", Top = 230, Left = 10, Width = 220 };
            Button btnMount = new Button { Text = "挂载选中账号", Top = 230, Left = 250, Width = 220, BackColor = Color.LightGreen };

            string cfg = "config.json";
            if (File.Exists(cfg)) {
                var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(cfg));
                if (list != null) foreach (var a in list) dgv.Rows.Add(a.Name, a.Url, a.User, a.Pass);
            }

            btnSave.Click += (s, e) => {
                var list = new List<Account>();
                foreach (DataGridViewRow row in dgv.Rows) {
                    if (row.IsNewRow) continue;
                    list.Add(new Account { Name = row.Cells[0].Value?.ToString() ?? "", Url = row.Cells[1].Value?.ToString() ?? "", User = row.Cells[2].Value?.ToString() ?? "", Pass = row.Cells[3].Value?.ToString() ?? "" });
                }
                File.WriteAllText(cfg, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("账号列表已保存！");
            };

            btnMount.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string url = row.Cells[1].Value?.ToString() ?? "";
                string user = row.Cells[2].Value?.ToString() ?? "";
                string pass = row.Cells[3].Value?.ToString() ?? "";
                
                // 挂载指令：使用 * 自动分配空闲盘符
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use * {url} /user:{user} {pass} /persistent:no") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                Process.Start(psi);
                MessageBox.Show("挂载请求已发送，请查看“此电脑”中的新驱动器。");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnMount });
            Application.Run(f);
        }
    }
}
