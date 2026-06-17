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
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(580, 400), StartPosition = FormStartPosition.CenterScreen };

            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 540, Height = 220, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
            dgv.Columns.Add("Name", "名称");
            dgv.Columns.Add("Url", "地址");
            dgv.Columns.Add("User", "账号");
            DataGridViewTextBoxColumn passCol = new DataGridViewTextBoxColumn { HeaderText = "密码", Name = "Pass" };
            dgv.Columns.Add(passCol);
            dgv.Columns.Add("Drive", "盘符(如U:)");

            // 密码显示为星号
            dgv.CellFormatting += (s, e) => { if (dgv.Columns[e.ColumnIndex].Name == "Pass" && e.Value != null) { e.Value = new string('*', e.Value.ToString().Length); e.FormattingApplied = true; } };

            Button btnSave = new Button { Text = "保存列表", Top = 250, Left = 10, Width = 170 };
            Button btnAction = new Button { Text = "连接", Top = 250, Left = 190, Width = 180, BackColor = Color.LightGreen };
            Button btnDisconnect = new Button { Text = "断开指定盘符", Top = 250, Left = 380, Width = 170, BackColor = Color.Salmon };

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
                MessageBox.Show("保存成功！");
            };

            // 连接按钮逻辑
            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                
                string drv = row.Cells[4].Value?.ToString()?.Trim() ?? "";
                string url = row.Cells[1].Value?.ToString()?.Trim() ?? "";
                string user = row.Cells[2].Value?.ToString()?.Trim() ?? "";
                string pass = row.Cells[3].Value?.ToString()?.Trim() ?? "";
                
                string target = string.IsNullOrEmpty(drv) ? "*" : (drv.EndsWith(":") ? drv : drv + ":");
                
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use {target} {url} /user:{user} {pass} /persistent:no") { 
                    WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false 
                };
                
                Process.Start(psi);
                // 移除 MessageBox 避免阻塞，用户点击后会瞬间执行完毕
            };

            // 断开按钮逻辑
            btnDisconnect.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                
                string drv = row.Cells[4].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(drv)) { MessageBox.Show("请先在【盘符】列填写要断开的盘符(如 U:)"); return; }
                
                string target = drv.EndsWith(":") ? drv : drv + ":";
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use {target} /delete /y") { 
                    WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false 
                };
                
                Process.Start(psi);
                MessageBox.Show($"已尝试断开 {target}");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnAction, btnDisconnect });
            Application.Run(f);
        }
    }
}
