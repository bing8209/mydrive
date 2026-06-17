using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;

namespace MountTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    // 单个账号的配置模型
    public class AccountConfig
    {
        public string ProfileName { get; set; } = "";
        public string Url { get; set; } = "";
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
        public string Drive { get; set; } = "Z:";
        public string VolName { get; set; } = "LuckyDrive";
        public string CacheDir { get; set; } = ""; // 新增：自定义缓存目录
    }

    // 全局持久化模型
    public class AppConfig
    {
        public string LastSelectedProfile { get; set; } = "";
        public List<AccountConfig> Accounts { get; set; } = new List<AccountConfig>();
    }

    public class MainForm : Form
    {
        private readonly string rclonePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
        private readonly string msiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winfsp_install.msi");
        private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        
        private Dictionary<string, int> _activeMounts = new Dictionary<string, int>();
        private AppConfig _appConfig = new AppConfig();

        // --- 界面布局控件 ---
        private Label lblProfile = new Label() { Text = "选择/管理账号:", Left = 20, Top = 25, Width = 110 };
        private ComboBox cmbProfile = new ComboBox() { Left = 140, Top = 22, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        private Button btnSaveProfile = new Button() { Text = "保存账号", Left = 310, Top = 20, Width = 70, Height = 26 };
        private Button btnDelProfile = new Button() { Text = "删除", Left = 385, Top = 20, Width = 55, Height = 26 };

        private Label lblUrl = new Label() { Text = "Lucky WebDAV 地址:", Left = 20, Top = 65, Width = 110 };
        private TextBox txtUrl = new TextBox() { Left = 140, Top = 62, Width = 300 };

        private Label lblUser = new Label() { Text = "WebDAV 用户名:", Left = 20, Top = 105, Width = 110 };
        private TextBox txtUser = new TextBox() { Left = 140, Top = 102, Width = 300 };

        private Label lblPass = new Label() { Text = "WebDAV 密  码:", Left = 20, Top = 145, Width = 110 };
        private TextBox txtPass = new TextBox() { Left = 140, Top = 142, Width = 300, PasswordChar = '*' };

        // 紧凑行：盘符 + 挂载名称
        private Label lblDrive = new Label() { Text = "盘符与显示名称:", Left = 20, Top = 185, Width = 110 };
        private ComboBox cmbDrive = new ComboBox() { Left = 140, Top = 182, Width = 65, DropDownStyle = ComboBoxStyle.DropDownList };
        private TextBox txtVolName = new TextBox() { Left = 215, Top = 182, Width = 225, Text = "LuckyDrive" };

        // 新增行：自定义缓存目录
        private Label lblCacheDir = new Label() { Text = "自定义缓存目录:", Left = 20, Top = 225, Width = 110 };
        private TextBox txtCacheDir = new TextBox() { Left = 140, Top = 222, Width = 235 };
        private Button btnBrowseCache = new Button() { Text = "浏览...", Left = 385, Top = 220, Width = 55, Height = 26 };

        private Button btnAction = new Button() { Text = "🚀 立即挂载", Left = 140, Top = 270, Width = 140, Height = 40 };
        private Button btnDisconnect = new Button() { Text = "❌ 断开连接", Left = 300, Top = 270, Width = 140, Height = 40 };

        public MainForm()
        {
            // 基础窗体属性设置
            this.Text = "Lucky WebDAV 定制挂载器 v3.0";
            this.Width = 480;
            this.Height = 365;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;

            // 初始化可选盘符
            string[] drives = { "Z:", "Y:", "X:", "W:", "V:", "U:", "T:", "S:", "R:", "Q:", "P:", "O:", "N:", "M:", "L:", "K:", "J:", "I:", "H:" };
            cmbDrive.Items.AddRange(drives);
            cmbDrive.SelectedIndex = 0;

            // 装载所有控件
            this.Controls.Add(lblProfile); this.Controls.Add(cmbProfile); this.Controls.Add(btnSaveProfile); this.Controls.Add(btnDelProfile);
            this.Controls.Add(lblUrl); this.Controls.Add(txtUrl);
            this.Controls.Add(lblUser); this.Controls.Add(txtUser);
            this.Controls.Add(lblPass); this.Controls.Add(txtPass);
            this.Controls.Add(lblDrive); this.Controls.Add(cmbDrive); this.Controls.Add(txtVolName);
            this.Controls.Add(lblCacheDir); this.Controls.Add(txtCacheDir); this.Controls.Add(btnBrowseCache);
            this.Controls.Add(btnAction); this.Controls.Add(btnDisconnect);

            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            InitializeEvents();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            ExtractInternalResource("rclone.exe", rclonePath);
            CheckDriverEnvironment();
            LoadConfig(); // 载入本地配置
        }

        private void InitializeEvents()
        {
            // 【切换账号下拉菜单】
            cmbProfile.SelectedIndexChanged += (s, e) => {
                string selectedName = cmbProfile.SelectedItem?.ToString() ?? "";
                var account = _appConfig.Accounts.Find(a => a.ProfileName == selectedName);
                if (account != null) {
                    txtUrl.Text = account.Url;
                    txtUser.Text = account.User;
                    txtPass.Text = account.Pass;
                    txtVolName.Text = account.VolName;
                    txtCacheDir.Text = account.CacheDir;
                    int driveIndex = cmbDrive.Items.IndexOf(account.Drive);
                    if (driveIndex >= 0) cmbDrive.SelectedIndex = driveIndex;
                }
            };

            // 【新增/保存当前账号】
            btnSaveProfile.Click += (s, e) => {
                string url = txtUrl.Text.Trim();
                string user = txtUser.Text.Trim();
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(user)) {
                    MessageBox.Show("请至少填写地址和用户名再保存！", "提示");
                    return;
                }

                string profileName = ShowInputDialog("请输入账号别名（如：家里NAS、办公室Lucky）:", "保存账号");
                if (string.IsNullOrEmpty(profileName)) return;

                var existing = _appConfig.Accounts.Find(a => a.ProfileName == profileName);
                if (existing != null) {
                    existing.Url = url; existing.User = user; existing.Pass = txtPass.Text;
                    existing.Drive = cmbDrive.SelectedItem?.ToString() ?? "Z:";
                    existing.VolName = txtVolName.Text.Trim();
                    existing.CacheDir = txtCacheDir.Text.Trim();
                } else {
                    _appConfig.Accounts.Add(new AccountConfig {
                        ProfileName = profileName, Url = url, User = user, Pass = txtPass.Text,
                        Drive = cmbDrive.SelectedItem?.ToString() ?? "Z:", VolName = txtVolName.Text.Trim(),
                        CacheDir = txtCacheDir.Text.Trim()
                    });
                }
                SaveConfig(profileName);
                MessageBox.Show($"账号「{profileName}」已成功保存！", "成功");
            };

            // 【删除选中的账号】
            btnDelProfile.Click += (s, e) => {
                string selectedName = cmbProfile.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(selectedName)) return;

                var account = _appConfig.Accounts.Find(a => a.ProfileName == selectedName);
                if (account != null) {
                    _appConfig.Accounts.Remove(account);
                    SaveConfig("");
                    ClearInputFields();
                }
            };

            // 【浏览选取本地缓存文件夹】
            btnBrowseCache.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog()) {
                    fbd.Description = "请选择网络盘的本地缓存目录（建议选择剩余空间较大的高速固态硬盘分区）";
                    if (fbd.ShowDialog() == DialogResult.OK) {
                        txtCacheDir.Text = fbd.SelectedPath;
                    }
                }
            };

            // 【核心挂载执行】
            btnAction.Click += (s, e) => {
                string url = txtUrl.Text.Trim();
                string user = txtUser.Text.Trim();
                string pass = txtPass.Text;
                string targetDrive = cmbDrive.SelectedItem?.ToString() ?? "Z:";
                string volName = string.IsNullOrEmpty(txtVolName.Text.Trim()) ? "LuckyDrive" : txtVolName.Text.Trim();
                string cacheDir = txtCacheDir.Text.Trim();

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(user)) {
                    MessageBox.Show("错误：地址和用户名不能为空！", "提示");
                    return;
                }

                if (_activeMounts.ContainsKey(targetDrive.ToUpper())) {
                    MessageBox.Show($"盘符 {targetDrive} 已经在挂载中！", "提示");
                    return;
                }

                try {
                    string obscuredPass = ObscurePassword(pass);
                    
                    // 动态组装自定义缓存路径参数
                    string cacheArgs = "";
                    if (!string.IsNullOrEmpty(cacheDir) && Directory.Exists(cacheDir)) {
                        cacheArgs = $" --cache-dir \"{cacheDir}\"";
                    }

                    // 执行高性能挂载命令
                    string arguments = $"mount :webdav: {targetDrive} --webdav-url \"{url}\" --webdav-user \"{user}\" --webdav-pass \"{obscuredPass}\" --vfs-cache-mode full --volname \"{volName}\"{cacheArgs} --network-mode";

                    ProcessStartInfo psi = new ProcessStartInfo(rclonePath, arguments) {
                        WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false
                    };

                    Process? p = Process.Start(psi);
                    if (p != null) {
                        _activeMounts[targetDrive.ToUpper()] = p.Id;
                        MessageBox.Show($"挂载指令已发出！\n盘符: {targetDrive}\n名称: {volName}\n已启用高性能全缓存托管模式。", "挂载成功");
                    }
                }
                catch (Exception ex) {
                    MessageBox.Show("挂载执行失败: " + ex.Message, "错误");
                }
            };

            // 【断开连接】
            btnDisconnect.Click += (s, e) => {
                string targetDrive = cmbDrive.SelectedItem?.ToString() ?? "Z:";
                if (_activeMounts.TryGetValue(targetDrive.ToUpper(), out int pid)) {
                    try {
                        Process.GetProcessById(pid).Kill();
                        _activeMounts.Remove(targetDrive.ToUpper());
                        MessageBox.Show($"盘符 {targetDrive} 已成功安全断开。");
                    } catch { _activeMounts.Remove(targetDrive.ToUpper()); }
                } else {
                    MessageBox.Show($"未发现由本工具托管的 {targetDrive} 挂载进程。", "提示");
                }
            };
        }

        // --- 配置序列化持久化控制 ---
        private void LoadConfig()
        {
            try {
                if (File.Exists(configPath)) {
                    string json = File.ReadAllText(configPath);
                    var parsed = JsonSerializer.Deserialize<AppConfig>(json);
                    if (parsed != null) _appConfig = parsed;
                }
                
                UpdateProfileComboBox();

                if (!string.IsNullOrEmpty(_appConfig.LastSelectedProfile)) {
                    int idx = cmbProfile.Items.IndexOf(_appConfig.LastSelectedProfile);
                    if (idx >= 0) cmbProfile.SelectedIndex = idx;
                } else if (cmbProfile.Items.Count > 0) {
                    cmbProfile.SelectedIndex = 0;
                } else {
                    ClearInputFields();
                }
            } catch { ClearInputFields(); }
        }

        private void SaveConfig(string selectProfileAfterRefresh)
        {
            try {
                _appConfig.LastSelectedProfile = selectProfileAfterRefresh;
                string json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json); // 彻底修复：移除了引发 FileNotFoundException 的 Read 操作
                
                UpdateProfileComboBox();
                
                int idx = cmbProfile.Items.IndexOf(selectProfileAfterRefresh);
                if (idx >= 0) cmbProfile.SelectedIndex = idx;
                else if (cmbProfile.Items.Count > 0) cmbProfile.SelectedIndex = 0;
            } catch { }
        }

        private void SaveConfigRaw()
        {
            try {
                string json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            } catch { }
        }

        private void UpdateProfileComboBox()
        {
            cmbProfile.Items.Clear();
            foreach (var acc in _appConfig.Accounts) {
                cmbProfile.Items.Add(acc.ProfileName);
            }
        }

        private void ClearInputFields()
        {
            txtUrl.Text = ""; txtUser.Text = ""; txtPass.Text = ""; 
            txtVolName.Text = "LuckyDrive"; txtCacheDir.Text = "";
            if (cmbDrive.Items.Count > 0) cmbDrive.SelectedIndex = 0;
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // 关闭软件前自动把界面上的改动无感保存入当前的账号别名中
            string currentProfile = cmbProfile.SelectedItem?.ToString() ?? "";
            if (!string.IsNullOrEmpty(currentProfile)) {
                var acc = _appConfig.Accounts.Find(a => a.ProfileName == currentProfile);
                if (acc != null) {
                    acc.Url = txtUrl.Text.Trim(); acc.User = txtUser.Text.Trim(); acc.Pass = txtPass.Text;
                    acc.Drive = cmbDrive.SelectedItem?.ToString() ?? "Z:";
                    acc.VolName = txtVolName.Text.Trim();
                    acc.CacheDir = txtCacheDir.Text.Trim();
                    _appConfig.LastSelectedProfile = currentProfile;
                    SaveConfigRaw();
                }
            }
        }

        // --- 核心辅助小工具 ---
        private string ShowInputDialog(string text, string caption)
        {
            Form prompt = new Form() { Width = 350, Height = 150, Text = caption, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = text, Width = 300 };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 290 };
            Button confirmation = new Button() { Text = "确定", Left = 210, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textBox); prompt.Controls.Add(textLabel); prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : "";
        }

        private string ObscurePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return "";
            ProcessStartInfo psi = new ProcessStartInfo(rclonePath, $"obscure \"{plainPassword}\"") {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (Process? p = Process.Start(psi)) {
                if (p == null) return "";
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output.Trim();
            }
        }

        private void ExtractInternalResource(string resourceName, string outputPath)
        {
            if (File.Exists(outputPath)) return;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) return;
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write)) {
                    stream.CopyTo(fs);
                }
            }
        }

        private void CheckDriverEnvironment()
        {
            if (!CheckIfWinFspInstalled()) {
                var result = MessageBox.Show("系统未检测到 WinFsp 驱动，点击“确定”将自动拉起静默安装。", "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (result == DialogResult.OK) {
                    try {
                        ExtractInternalResource("winfsp.msi", msiPath);
                        Process p = Process.Start("msiexec.exe", $"/i \"{msiPath}\" /passive");
                        p.WaitForExit();
                        if (File.Exists(msiPath)) File.Delete(msiPath);
                    } catch { }
                }
            }
        }

        private bool CheckIfWinFspInstalled()
        {
            using (RegistryKey? key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinFsp"))
            using (RegistryKey? key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\WinFsp")) {
                return (key64 != null || key32 != null);
            }
        }
    }
}
