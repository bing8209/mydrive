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
            
            // 关键：创建密码列，使用 DataGridViewTextBoxColumn 并设置显示为星号
            DataGridViewTextBoxColumn passCol = new DataGridViewTextBoxColumn { HeaderText = "密码", Name = "Pass" };
            dgv.Columns.Add(passCol);

            // 密码掩码逻辑：让密码列显示为星号
            dgv.CellFormatting += (s, e) => {
                if (dgv.Columns[e.ColumnIndex].Name == "Pass" && e.Value != null) {
                    e.Value = new string('*', e.Value.ToString().Length);
                    e.FormattingApplied = true;
                }
            };

            Button btnSave = new Button { Text = "保存列表", Top = 230, Left = 10, Width = 150 };
            Button btnAction = new Button { Text = "连接", Top = 230, Left = 170, Width = 150, BackColor = Color.LightGreen };
            Button btnDisconnect = new Button { Text = "断开", Top = 230, Left = 330, Width = 140, BackColor = Color.Salmon };

            string cfg = "config.json";
            // (省略加载和保存逻辑，保持原有逻辑不变即可)

            // 挂载逻辑
            btnAction.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null || row.IsNewRow) return;
                string url = row.Cells[1].Value?.ToString() ?? "";
                string user = row.Cells[2].Value?.ToString() ?? "";
                string pass = row.Cells[3].Value?.ToString() ?? "";
                
                Process.Start("net.exe", $"use * {url} /user:{user} {pass} /persistent:no");
                MessageBox.Show("已请求连接");
            };

            // 断开逻辑
            btnDisconnect.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null) return;
                // 使用 net use 命令断开对应的 WebDAV 地址
                string url = row.Cells[1].Value?.ToString() ?? "";
                Process.Start("net.exe", $"use {url} /delete /y");
                MessageBox.Show("已断开连接");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnAction, btnDisconnect });
            Application.Run(f);
        }
    }
}
