using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization; // 记得引用 System.Web.Extensions
using System.Windows.Forms;

public partial class Form1 : Form
{
    private string configPath = "accounts.json"; // 配置文件名

    private void Form1_Load(object sender, EventArgs e) {
        // 读取 JSON 配置文件
        if (File.Exists(configPath)) {
            string json = File.ReadAllText(configPath);
            dgvAccounts.DataSource = new JavaScriptSerializer().Deserialize<List<Account>>(json);
        }
    }

    private void btnSave_Click(object sender, EventArgs e) {
        // 将表格数据转为 JSON 并保存
        var list = (List<Account>)dgvAccounts.DataSource;
        string json = new JavaScriptSerializer().Serialize(list);
        File.WriteAllText(configPath, json);
        MessageBox.Show("账号列表已更新！");
    }

    private void btnMount_Click(object sender, EventArgs e) {
        // 获取当前选中的行
        var row = dgvAccounts.CurrentRow;
        if (row == null) return;
        
        string url = row.Cells["Url"].Value.ToString();
        string user = row.Cells["User"].Value.ToString();
        string pass = row.Cells["Pass"].Value.ToString();

        // 挂载逻辑同前...
    }
}
