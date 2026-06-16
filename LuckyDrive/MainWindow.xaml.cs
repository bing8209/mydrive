using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;

namespace LuckyDrive
{
    public partial class MainWindow : Window
    {
        // 核心数据源
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        // 纯代码 UI 控件引用
        private TextBox txtName = null!;
        private TextBox txtUrl = null!;
        private TextBox txtUser = null!;
        private PasswordBox txtPass = null!;
        private ComboBox comboDrive = null!;
        private ListBox listDrives = null!;

        public MainWindow()
        {
            // 彻底跳过系统对 XAML 的解析，我们自己用手把界面“画”出来！
            BuildPureCodeUI();
            
            try
            {
                DriveList = new ObservableCollection<DriveConfig>();
                listDrives.ItemsSource = DriveList;
                LoadConfig();
            }
            catch { }

            RefreshAvailableDriveLetters();
        }

        // 🧬 金刚不坏：纯 C# 动态渲染的精致双栏卡片界面
        private void BuildPureCodeUI()
        {
            this.Title = "LuckyDrive - 纯绿色多网盘卡片挂载器 (纯净版)";
            this.Height = 650;
            this.Width = 1000;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));

            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ================== 左侧表单面板 ==================
            Border leftBorder = new Border { Background = Brushes.White, Padding = new Thickness(25) };
            StackPanel leftPanel = new StackPanel();

            leftPanel.Children.Add(new TextBlock { Text = "添加网盘卡片", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });
            
            leftPanel.Children.Add(new TextBlock { Text = "网盘自定义名称:", Margin = new Thickness(0, 0, 0, 5), Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
            txtName = new TextBox { Height = 35, Margin = new Thickness(0, 0, 0, 15), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };
            leftPanel.Children.Add(txtName);

            leftPanel.Children.Add(new TextBlock { Text = "Lucky WebDAV URL (例如: 192.168.1.2:1234/dav):", Margin = new Thickness(0, 0, 0, 5), Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
            txtUrl = new TextBox { Height = 35, Margin = new Thickness(0, 0, 0, 15), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };
            leftPanel.Children.Add(txtUrl);

            leftPanel.Children.Add(new TextBlock { Text = "用户账号 (没有可留空):", Margin = new Thickness(0, 0, 0, 5), Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
            txtUser = new TextBox { Height = 35, Margin = new Thickness(0, 0, 0, 15), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };
            leftPanel.Children.Add(txtUser);

            leftPanel.Children.Add(new TextBlock { Text = "访问密码 (没有可留空):", Margin = new Thickness(0, 0, 0, 5), Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
            txtPass = new PasswordBox { Height = 35, Margin = new Thickness(0, 0, 0, 15), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };
            leftPanel.Children.Add(txtPass);

            leftPanel.Children.Add(new TextBlock { Text = "选择挂载虚拟盘符:", Margin = new Thickness(0, 0, 0, 5), Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
            comboDrive = new ComboBox { Height = 35, Margin = new Thickness(0, 0, 0, 20), VerticalContentAlignment = VerticalAlignment.Center };
            leftPanel.Children.Add(comboDrive);

            Button btnSave = new Button { Content = "保存配置卡片", Height = 40, Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
            btnSave.Click += BtnSave_Click;
            leftPanel.Children.Add(btnSave);

            leftBorder.Child = leftPanel;
            Grid.SetColumn(leftBorder, 0);
            mainGrid.Children.Add(leftBorder);

            // ================== 右侧卡片区域 ==================
            Grid rightGrid = new Grid { Margin = new Thickness(25) };
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            rightGrid.Children.Add(new TextBlock { Text = "我的 Lucky 网盘群", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            // 纯手工缔造的具有 WPF 现代渲染特征的 DataTemplate 树
            listDrives = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            listDrives.SelectionChanged += ListDrives_SelectionChanged;

            FrameworkElementFactory factoryPanel = new FrameworkElementFactory(typeof(WrapPanel));
            factoryPanel.SetValue(WrapPanel.WidthProperty, 540.0);
            listDrives.ItemsPanel = new ItemsPanelTemplate(factoryPanel);

            FrameworkElementFactory cardBorder = new FrameworkElementFactory(typeof(Border));
            cardBorder.SetValue(Border.WidthProperty, 250.0);
            cardBorder.SetValue(Border.HeightProperty, 160.0);
            cardBorder.SetValue(Border.BackgroundProperty, Brushes.White);
            cardBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            cardBorder.SetValue(Border.MarginProperty, new Thickness(10));
            cardBorder.SetValue(Border.PaddingProperty, new Thickness(15));

            FrameworkElementFactory cardGrid = new FrameworkElementFactory(typeof(Grid));
            cardGrid.AppendChild(new RowDefinition { Height = GridLength.Auto });
            cardGrid.AppendChild(new RowDefinition { Height = GridLength.Auto });
            cardGrid.AppendChild(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            FrameworkElementFactory titleTxt = new FrameworkElementFactory(typeof(TextBlock));
            titleTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            titleTxt.SetValue(TextBlock.FontSizeProperty, 16.0);
            titleTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            titleTxt.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)));
            titleTxt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            Grid.SetRow(titleTxt, 0);
            cardGrid.AppendChild(titleTxt);

            FrameworkElementFactory statusStack = new FrameworkElementFactory(typeof(StackPanel));
            statusStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            statusStack.SetValue(StackPanel.MarginProperty, new Thickness(0, 8, 0, 0));
            Grid.SetRow(statusStack, 1);

            FrameworkElementFactory dotDot = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
            dotDot.SetValue(System.Windows.Shapes.Ellipse.WidthProperty, 8.0);
            dotDot.SetValue(System.Windows.Shapes.Ellipse.HeightProperty, 8.0);
            dotDot.SetBinding(System.Windows.Shapes.Ellipse.FillProperty, new System.Windows.Data.Binding("StatusColor"));
            dotDot.SetValue(System.Windows.Shapes.Ellipse.VerticalAlignmentProperty, VerticalAlignment.Center);
            dotDot.SetValue(System.Windows.Shapes.Ellipse.MarginProperty, new Thickness(0, 0, 6, 0));
            statusStack.AppendChild(dotDot);

            FrameworkElementFactory statusTxt = new FrameworkElementFactory(typeof(TextBlock));
            statusTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StatusText"));
            statusTxt.SetValue(TextBlock.FontSizeProperty, 12.0);
            statusTxt.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)));
            statusStack.AppendChild(statusTxt);
            cardGrid.AppendChild(statusStack);

            FrameworkElementFactory actionGrid = new FrameworkElementFactory(typeof(Grid));
            actionGrid.SetValue(Grid.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            Grid.SetRow(actionGrid, 2);
            actionGrid.AppendChild(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.AppendChild(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElementFactory btnMount = new FrameworkElementFactory(typeof(Button));
            btnMount.SetBinding(Button.ContentProperty, new System.Windows.Data.Binding("ButtonText"));
            btnMount.SetBinding(Button.BackgroundProperty, new System.Windows.Data.Binding("ButtonBg"));
            btnMount.SetBinding(Button.TagProperty, new System.Windows.Data.Binding("Id"));
            btnMount.SetValue(Button.ForegroundProperty, Brushes.White);
            btnMount.SetValue(Button.FontWeightProperty, FontWeights.Bold);
            btnMount.SetValue(Button.HeightProperty, 32.0);
            btnMount.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            btnMount.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnToggleMount_Click));
            Grid.SetColumn(btnMount, 0);
            actionGrid.AppendChild(btnMount);

            FrameworkElementFactory btnDel = new FrameworkElementFactory(typeof(Button));
            btnDel.SetValue(Button.ContentProperty, "删除");
            btnDel.SetValue(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)));
            btnDel.SetValue(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
            btnDel.SetValue(Button.HeightProperty, 32.0);
            btnDel.SetValue(Button.WidthProperty, 50.0);
            btnDel.SetValue(Button.MarginProperty, new Thickness(10, 0, 0, 0));
            btnDel.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            btnDel.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnDelete_Click));
            Grid.SetColumn(btnDel, 1);
            actionGrid.AppendChild(btnDel);

            cardGrid.AppendChild(actionGrid);
            cardBorder.AppendChild(cardGrid);
            listDrives.ItemTemplate = new DataTemplate { VisualTree = cardBorder };

            Grid.SetRow(listDrives, 1);
            rightGrid.Children.Add(listDrives);

            Grid.SetColumn(rightGrid, 1);
            mainGrid.Children.Add(rightGrid);

            this.Content = mainGrid;
        }

        private void RefreshAvailableDriveLetters()
        {
            try
            {
                var defaultLetters = new List<string> { "Z:", "Y:", "X:", "W:", "V:", "U:", "T:", "S:", "R:", "Q:" };
                var availableLetters = new List<string>();

                if (DriveList == null || DriveList.Count == 0)
                {
                    comboDrive.ItemsSource = defaultLetters;
                    comboDrive.SelectedIndex = 0;
                    return;
                }

                foreach (var letter in defaultLetters)
                {
                    bool isUsedInApp = false;
                    foreach (var drive in DriveList)
                    {
                        if (drive != null && (drive.DriveLetter + ":") == letter)
                        {
                            isUsedInApp = true;
                            break;
                        }
                    }
                    if (!isUsedInApp) availableLetters.Add(letter);
                }

                comboDrive.ItemsSource = availableLetters;
                if (availableLetters.Count > 0) comboDrive.SelectedIndex = 0;
            }
            catch
            {
                comboDrive.ItemsSource = new List<string> { "Z:", "Y:", "X:" };
                comboDrive.SelectedIndex = 0;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(comboDrive.Text))
            {
                MessageBox.Show("请选择或输入一个可用的盘符字母！", "提示");
                return;
            }

            string targetLetter = comboDrive.Text.Replace(":", "").Trim().ToUpper();

            foreach (var d in DriveList)
            {
                if (d != null && d.DriveLetter == targetLetter)
                {
                    MessageBox.Show($"盘符 {targetLetter}: 已经在列表中了！", "提示");
                    return;
                }
            }

            var newDrive = new DriveConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = txtName.Text,
                Url = txtUrl.Text.Trim(),
                User = txtUser.Text.Trim(),
                Pass = txtPass.Password.Trim(),
                DriveLetter = targetLetter, 
                IsMounted = false
            };

            DriveList.Add(newDrive);
            SaveConfig();
            RefreshAvailableDriveLetters();
        }

        private void BtnToggleMount_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (btn.Tag == null) return;
            
            string id = btn.Tag.ToString()!;
            DriveConfig? drive = null;

            foreach (var d in DriveList)
            {
                if (d != null && d.Id == id) { drive = d; break; }
            }

            if (drive == null) return;

            if (!drive.IsMounted)
            {
                try
                {
                    string uriString = drive.Url;
                    if (!uriString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        uriString = "http://" + uriString;
                    }

                    Uri uri = new Uri(uriString);
                    string host = uri.Host;
                    int port = uri.Port;
                    string path = uri.AbsolutePath.Trim('/');
                    path = path.Replace("/", "\\");

                    string uncPath;
                    if (port == 80 || port == 443)
                    {
                        uncPath = string.IsNullOrEmpty(path) ? $"\\\\{host}\\DavWWWRoot" : $"\\\\{host}\\DavWWWRoot\\{path}";
                    }
                    else
                    {
                        uncPath = string.IsNullOrEmpty(path) ? $"\\\\{host}@{port}\\DavWWWRoot" : $"\\\\{host}@{port}\\DavWWWRoot\\{path}";
                    }

                    try { Process.Start(new ProcessStartInfo("net", $"use {drive.DriveLetter}: /delete /y") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }

                    string args = $"use {drive.DriveLetter}: \"{uncPath}\" \"{drive.Pass}\" /user:\"{drive.User}\" /persistent:no";
                    var psi = new ProcessStartInfo("net", args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                        string error = process?.StandardError.ReadToEnd() ?? "";

                        if (process?.ExitCode == 0 || error.Contains("1219") || error.Contains("已经连接"))
                        {
                            drive.IsMounted = true;
                            Process.Start("explorer.exe", $"{drive.DriveLetter}:");
                        }
                        else
                        {
                            MessageBox.Show($"挂载失败，系统提示：\n{error}", "提示");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"挂载异常: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    string args = $"use {drive.DriveLetter}: /delete /y";
                    var psi = new ProcessStartInfo("net", args) { CreateNoWindow = true, UseShellExecute = false };
                    using (var process = Process.Start(psi)) process?.WaitForExit();
                    
                    drive.IsMounted = false;
                }
                catch { }
            }

            try { listDrives.Items.Refresh(); } catch { }
            RefreshAvailableDriveLetters(); 
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (listDrives.SelectedItem is DriveConfig selected)
            {
                if (selected.IsMounted)
                {
                    MessageBox.Show("请先断开连接，再删除卡片！", "提示");
                    return;
                }
                DriveList.Remove(selected);
                SaveConfig();
                RefreshAvailableDriveLetters();
            }
        }

        private void ListDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listDrives.SelectedItem is DriveConfig selected)
            {
                txtName.Text = selected.Name;
                txtUrl.Text = selected.Url;
                txtUser.Text = selected.User;
                txtPass.Password = selected.Pass;
                comboDrive.Text = selected.DriveLetter + ":";
            }
        }

        private void SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var saveList = new List<DriveConfig>();
                foreach (var d in DriveList)
                {
                    if (d == null) continue;
                    saveList.Add(new DriveConfig {
                        Id = d.Id, Name = d.Name, Url = d.Url, User = d.User, Pass = d.Pass, DriveLetter = d.DriveLetter
                    });
                }
                File.WriteAllText(_configPath, JsonSerializer.Serialize(saveList, options));
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var list = JsonSerializer.Deserialize<List<DriveConfig>>(json);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (item != null) DriveList.Add(item);
                        }
                    }
                }
            }
            catch { }
        }
    }

    public class DriveConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsMounted { get; set; } = false;

        public string StatusText => IsMounted ? $"● 已映射到虚拟磁盘 {DriveLetter}:" : "○ 未连接";
        public SolidColorBrush StatusColor => IsMounted ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        public string ButtonText => IsMounted ? "断开" : "挂载";
        public SolidColorBrush ButtonBg => IsMounted ? new SolidColorBrush(Colors.Crimson) : new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }
}
