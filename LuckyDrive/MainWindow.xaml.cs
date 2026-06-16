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
        public ObservableCollection<DriveConfig> DriveList { get; set; } = new ObservableCollection<DriveConfig>();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private TextBox txtName = null!;
        private TextBox txtUrl = null!;
        private TextBox txtUser = null!;
        private PasswordBox txtPass = null!;
        private ComboBox comboDrive = null!;
        private ListBox listDrives = null!;

        public MainWindow()
        {
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

        private void BuildPureCodeUI()
        {
            this.Title = "LuckyDrive - 纯绿色多网盘卡片挂载器";
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

            // ================== 右侧列表区域 ==================
            Grid rightGrid = new Grid { Margin = new Thickness(25) };
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            rightGrid.Children.Add(new TextBlock { Text = "我的 Lucky 网盘群", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            // 🚀 重构亮点：改用 100% C# 兼容无死角的标准化 DataTemplate 树结构，彻底解决 CS0120/CS1503 错位问题
            listDrives = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            listDrives.SelectionChanged += ListDrives_SelectionChanged;

            // 修复：使用附加属性的正确 C# 赋值方式
            ScrollViewer.SetHorizontalScrollBarVisibility(listDrives, ScrollBarVisibility.Disabled);

            // 构造极其稳固的动态卡片模板工厂
            FrameworkElementFactory cardBorder = new FrameworkElementFactory(typeof(Border));
            cardBorder.SetValue(Border.WidthProperty, 540.0);
            cardBorder.SetValue(Border.BackgroundProperty, Brushes.White);
            cardBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            cardBorder.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 10));
            cardBorder.SetValue(Border.PaddingProperty, new Thickness(15));

            FrameworkElementFactory cardGrid = new FrameworkElementFactory(typeof(Grid));
            
            // 修复：完美利用工厂模式建立扁平化行列布局布局
            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            FrameworkElementFactory col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            
            cardGrid.AppendChild
