using System;
using System.Diagnostics;
using System.Windows.Forms;

public partial class Form1 : Form
{
    private void btnMount_Click(object sender, EventArgs e)
    {
        // 从界面控件读取输入
        string webdavUrl = txtUrl.Text;
        string username = txtUser.Text;
        string password = txtPass.Text;
        string driveLetter = "Z:"; // 或者增加一个 txtDrive 控件

        // 构建命令
        string arguments = $"use {driveLetter} {webdavUrl} /user:{username} {password} /persistent:no";
        
        ProcessStartInfo psi = new ProcessStartInfo("net.exe", arguments);
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.UseShellExecute = false; // 必须设置为 false 才能成功隐藏黑框
        psi.CreateNoWindow = true;   // 彻底不显示黑框
        
        try {
            Process.Start(psi);
            MessageBox.Show("成功挂载到 " + driveLetter);
        } catch (Exception ex) {
            MessageBox.Show("挂载失败: " + ex.Message);
        }
    }
}
