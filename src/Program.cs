using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace MountTool {
    public class Account { public string Name { get; set; } = ""; public string Url { get; set; } = ""; public string User { get; set; } = ""; public string Pass { get; set; } = ""; public string Drive { get; set; } = ""; }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(550, 380), StartPosition = FormStartPosition.CenterScreen };

            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 510, Height = 220, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
            dgv.Columns.Add("Name", "名称");
            dgv.Columns.Add("Url", "地址");
            dgv.Columns.Add("User", "账号");
            dgv.Columns.Add("Pass", "密码");
            dgv.Columns.Add("Drive", "指定盘符(如U:)"); // 新增盘符列

            dgv.CellFormatting += (s, e) => { if (dgv.Columns[e.ColumnIndex].Name == "Pass" && e.Value != null) { e.Value = new string('*', e.Value.ToString().Length); e.FormattingApplied = true; } };

            Button btnSave = new Button { Text = "保存列表", Top = 240, Left = 10, Width = 160 };
            Button btnAction = new Button { Text = "连接", Top = 240, Left = 180, Width = 160, BackColor = Color.LightGreen };
            Button btnDisconnect = new Button { Text = "断开指定盘符", Top = 240, Left = 350, Width = 160, BackColor = Color.Salmon };

            string cfg = "config.json";
            if (File.Exists(cfg)) {
                var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(cfg));
                if (list != null) foreach (var a in list) dgv.Rows.Add(a.Name, a.Url, a.User, a.Pass, a.Drive);
            }

            btnSave.Click += (s, e) => {
                var list = new List<Account>();
                foreach (DataGridViewRow row in dgv.Rows) {
                    if (row.IsNewRow) continue;
                    list.Add(new Account { Name = row.Cells[0].Value?.ToString() ?? "", Url = row.Cells[1].Value?.ToString() ?? "", User = row.Cells[2].Value?.ToString() ?? "", Pass = row.Cells[3].Value?.ToString() ?? "", Drive = row.Cells[4].Value?.ToString() ?? "" });
                }
                File.WriteAllText(cfg, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("列表已保存。");
            };

            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string drv = row.Cells[4].Value?.ToString() ?? "";
                string url = row.Cells[1].Value?.ToString() ?? "";
                string user = row.Cells[2].Value?.ToString() ?? "";
                string pass = row.Cells[3].Value?.ToString() ?? "";
                
                // 使用指定盘符
                string target = string.IsNullOrEmpty(drv) ? "*" : drv;
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use {target} {url} /user:{user} {pass} /persistent:no") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                MessageBox.Show($"正在尝试挂载到 {target} ...");
            };

            btnDisconnect.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string drv = row.Cells[4].Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(drv)) { MessageBox.Show("请先在表格中填写要断开的盘符(如 U:)"); return; }

                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use {drv} /delete /y") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                MessageBox.Show($"已尝试断开 {drv} 盘。");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnAction, btnDisconnect });
            Application.Run(f);
        }
    }
}
