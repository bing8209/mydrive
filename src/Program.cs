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
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(500, 380), StartPosition = FormStartPosition.CenterScreen };

            // DataGridView 初始化
            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 460, Height = 220, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };
            dgv.Columns.Add("Name", "名称");
            dgv.Columns.Add("Url", "WebDAV 地址");
            dgv.Columns.Add("User", "账号");
            DataGridViewTextBoxColumn passCol = new DataGridViewTextBoxColumn { HeaderText = "密码", Name = "Pass" };
            dgv.Columns.Add(passCol);
            
            // 密码掩码逻辑
            dgv.CellFormatting += (s, e) => { if (dgv.Columns[e.ColumnIndex].Name == "Pass" && e.Value != null) { e.Value = new string('*', e.Value.ToString().Length); e.FormattingApplied = true; } };

            Button btnSave = new Button { Text = "保存列表", Top = 240, Left = 10, Width = 150 };
            Button btnAction = new Button { Text = "连接", Top = 240, Left = 170, Width = 150, BackColor = Color.LightGreen };
            Button btnDisconnect = new Button { Text = "断开", Top = 240, Left = 330, Width = 140, BackColor = Color.Salmon };

            // 加载逻辑
            string cfg = "config.json";
            if (File.Exists(cfg)) {
                var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(cfg));
                if (list != null) foreach (var a in list) dgv.Rows.Add(a.Name, a.Url, a.User, a.Pass);
            }

            // 保存
            btnSave.Click += (s, e) => {
                var list = new List<Account>();
                foreach (DataGridViewRow row in dgv.Rows) {
                    if (row.IsNewRow) continue;
                    list.Add(new Account { Name = row.Cells[0].Value?.ToString() ?? "", Url = row.Cells[1].Value?.ToString() ?? "", User = row.Cells[2].Value?.ToString() ?? "", Pass = row.Cells[3].Value?.ToString() ?? "" });
                }
                File.WriteAllText(cfg, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("已保存！");
            };

            // 完美无感连接
            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string url = row.Cells[1].Value?.ToString() ?? "";
                string user = row.Cells[2].Value?.ToString() ?? "";
                string pass = row.Cells[3].Value?.ToString() ?? "";
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", $"use * {url} /user:{user} {pass} /persistent:no") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                MessageBox.Show("已发送连接请求，请检查资源管理器！");
            };

            // 绝对断开：使用 * /delete /y 确保清理掉所有相关的网络挂载
            btnDisconnect.Click += (s, e) => {
                ProcessStartInfo psi = new ProcessStartInfo("net.exe", "use * /delete /y") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                MessageBox.Show("所有网络映射已尝试断开。");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnAction, btnDisconnect });
            Application.Run(f);
        }
    }
}
