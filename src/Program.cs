using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace MountTool {
    public class Account { public string Name { get; set; } = ""; public string Url { get; set; } = ""; public string User { get; set; } = ""; public string Pass { get; set; } = ""; }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(500, 380), StartPosition = FormStartPosition.CenterScreen };

            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 460, Height = 220, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
            dgv.Columns.Add("Name", "名称");
            dgv.Columns.Add("Url", "WebDAV 地址");
            dgv.Columns.Add("User", "账号");
            DataGridViewTextBoxColumn passCol = new DataGridViewTextBoxColumn { HeaderText = "密码", Name = "Pass" };
            dgv.Columns.Add(passCol);

            dgv.CellFormatting += (s, e) => { if (dgv.Columns[e.ColumnIndex].Name == "Pass" && e.Value != null) { e.Value = new string('*', e.Value.ToString().Length); e.FormattingApplied = true; } };

            Button btnSave = new Button { Text = "保存列表", Top = 240, Left = 10, Width = 150 };
            Button btnAction = new Button { Text = "连接", Top = 240, Left = 170, Width = 150, BackColor = Color.LightGreen };
            Button btnDisconnect = new Button { Text = "断开选中", Top = 240, Left = 330, Width = 140, BackColor = Color.Salmon };

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
                MessageBox.Show("列表已保存。");
            };

            // 精准连接：使用 * 自动找空盘，不会影响现有映射
            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string url = row.Cells[1].Value?.ToString() ?? "";
                string user = row.Cells[2].Value?.ToString() ?? "";
                string pass = row.Cells[3].Value?.ToString() ?? "";
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use * {url} /user:{user} {pass} /persistent:no") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                MessageBox.Show("连接请求已发送。");
            };

            // 精准断开：通过查找对应的盘符来断开，不会波及其他共享
            btnDisconnect.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string url = row.Cells[1].Value?.ToString() ?? "";
                
                // 找到该网址对应的映射盘符
                var process = Process.Start(new ProcessStartInfo("net.exe", "use") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                string output = process.StandardOutput.ReadToEnd();
                var lines = output.Split('\n');
                foreach(var line in lines) {
                    if(line.Contains(url)) {
                        var parts = line.Split(' ').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                        string drive = parts.FirstOrDefault(p => p.Contains(":"));
                        if(!string.IsNullOrEmpty(drive)) {
                            Process.Start(new ProcessStartInfo("net.exe", $"use {drive} /delete /y") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false });
                            MessageBox.Show($"已断开驱动器 {drive}");
                            return;
                        }
                    }
                }
                MessageBox.Show("未找到对应的挂载盘符。");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnAction, btnDisconnect });
            Application.Run(f);
        }
    }
}
