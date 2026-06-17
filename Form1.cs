using System.Diagnostics;

public partial class Form1 : Form
{
    // 这里写入你的 WebDAV 配置
    private string webdavUrl = "https://你的WebDAV地址";
    private string username = "你的账号";
    private string password = "你的密码";
    private string driveLetter = "Z:";

    private void btnMount_Click(object sender, EventArgs e)
    {
        // 核心逻辑：利用 Windows 的 net use 命令，调用系统底层的 WebDAV 支持
        // 这种方式不需要安装任何第三方臃肿客户端，不产生缓存，极其稳定
        string arguments = $"use {driveLetter} {webdavUrl} /user:{username} {password} /persistent:no";
        
        ProcessStartInfo psi = new ProcessStartInfo("net.exe", arguments);
        psi.WindowStyle = ProcessWindowStyle.Hidden; // 隐藏运行黑框
        Process.Start(psi);
        
        MessageBox.Show("网盘已挂载到 " + driveLetter);
    }
}
