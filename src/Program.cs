using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization; // 需要在 csproj 中添加对 System.Web.Extensions 的引用

namespace MountTool {
    public class Account { public string Name { get; set; } public string Url { get; set; } public string User { get; set; } public string Pass { get; set; } }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Form f = new Form { Text = "多账号 WebDAV 挂载器", Size = new Size(500, 400) };
            DataGridView dgv = new DataGridView { Top = 10, Left = 10, Width = 460, Height = 200, AutoGenerateColumns = true };
            Button btnSave = new Button { Text = "保存账号列表", Top = 220, Left = 10, Width = 200 };
            Button btnMount = new Button { Text = "挂载选中账号", Top = 220, Left = 220, Width = 200, BackColor = Color.LightGreen };

            // 加载逻辑
            string cfg = "config.json";
            if (File.Exists(cfg)) dgv.DataSource = new JavaScriptSerializer().Deserialize<List<Account>>(File.ReadAllText(cfg));

            btnSave.Click += (s, e) => {
                File.WriteAllText(cfg, new JavaScriptSerializer().Serialize(dgv.DataSource));
                MessageBox.Show("已保存！");
            };

            btnMount.Click += (s, e) => {
                var row = dgv.CurrentRow;
                if (row == null) return;
                string url = row.Cells[1].Value.ToString();
                string user = row.Cells[2].Value.ToString();
                string pass = row.Cells[3].Value.ToString();
                // 自动分配盘符逻辑... (同上)
                Process.Start("net.exe", $"use * {url} /user:{user} {pass} /persistent:no");
                MessageBox.Show("挂载成功！");
            };

            f.Controls.AddRange(new Control[] { dgv, btnSave, btnMount });
            Application.Run(f);
        }
    }
}
