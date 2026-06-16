using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Data;

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

            // ================== 左侧配置面板 ==================
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

            listDrives = new ListBox();
            listDrives.Background = Brushes.Transparent;
            listDrives.BorderThickness = new Thickness(0);
            listDrives.SelectionChanged += ListDrives_SelectionChanged;
            listDrives.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.SetValue(Border.BackgroundProperty, Brushes.White);
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            itemBorder.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 10));
            itemBorder.SetValue(Border.PaddingProperty, new Thickness(15));
            itemBorder.SetValue(Border.WidthProperty, 540.0);

            FrameworkElementFactory itemStack = new FrameworkElementFactory(typeof(StackPanel));

            // 网盘名字
            FrameworkElementFactory txtTitle = new FrameworkElementFactory(typeof(TextBlock));
            Binding bindName = new Binding("Name");
            txtTitle.SetBinding(TextBlock.TextProperty, bindName);
            txtTitle.SetValue(TextBlock.FontSizeProperty, 16.0);
            txtTitle.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            txtTitle.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)));
            itemStack.AppendChild(txtTitle);

            // 盘符状态
            FrameworkElementFactory txtStatus = new FrameworkElementFactory(typeof(TextBlock));
            Binding bindStatusText = new Binding("StatusText");
            txtStatus.SetBinding(TextBlock.TextProperty, bindStatusText);
            Binding bindStatusColor = new Binding("StatusColor");
            txtStatus.SetBinding(TextBlock.ForegroundProperty, bindStatusColor);
            txtStatus.SetValue(TextBlock.FontSizeProperty, 13.0);
            txtStatus.SetValue(TextBlock.MarginProperty, new Thickness(0, 5, 0, 10));
            itemStack.AppendChild(txtStatus);

            // 按钮操作廊
            FrameworkElementFactory btnPanel = new FrameworkElementFactory(typeof(StackPanel));
            btnPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory bMount = new FrameworkElementFactory(typeof(Button));
            Binding bindBtnText = new Binding("ButtonText");
            bMount.SetBinding(Button.ContentProperty, bindBtnText);
            Binding bindBtnBg = new Binding("ButtonBg");
            bMount.SetBinding(Button.BackgroundProperty, bindBtnBg);
            Binding bindId = new Binding("Id");
            bMount.SetBinding(Button.TagProperty, bindId);
            bMount.SetValue(Button.ForegroundProperty, Brushes.White);
            bMount.SetValue(Button.FontWeightProperty, FontWeights.Bold);
            bMount.SetValue(Button.HeightProperty, 30.0);
            bMount.SetValue(Button.WidthProperty, 80.0);
            bMount.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            bMount.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnToggleMount_Click));
            btnPanel.AppendChild(bMount);

            FrameworkElementFactory bDel = new FrameworkElementFactory(typeof(Button));
            bDel.SetValue(Button.ContentProperty, "删除卡片");
            bDel.SetValue(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)));
            bDel.SetValue(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
            bDel.SetValue(Button.HeightProperty, 30.0);
            bDel.SetValue(Button.Width
