using SportsCenter.BLL;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SportsCenter.DAL.Models;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Shell;

namespace Project.Frontend.Views
{
    /// <summary>
    /// Cửa sổ chính sau khi đăng nhập: sidebar + header + nội dung + footer.
    /// Bố cục thích ứng theo độ rộng: ≥1200 sidebar đầy đủ, 800–1199 chỉ icon, &lt;800 menu trượt.
    /// </summary>
    public partial class DashboardWindow : Window, IUiHost
    {
        private const double WideBreakpoint = 1200;
        private const double NarrowBreakpoint = 800;
        private const double SidebarFullWidth = 240;
        private const double SidebarCompactWidth = 72;

        private enum LayoutMode { Wide, Medium, Narrow }

        private readonly User _currentUser;
        private readonly List<(RadioButton Button, NavItem Item)> _nav = new();
        private LayoutMode _mode = LayoutMode.Wide;
        private bool _userCollapsed;     // người dùng tự thu gọn sidebar khi màn hình rộng
        private bool _drawerOpen;        // menu trượt đang mở (màn hình hẹp)
        private TaskCompletionSource<bool>? _dialogTcs;

        public DashboardWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            UiHost.Register(this);
            Closed += (_, _) => UiHost.Unregister(this);

            Title = $"{AppInfo.CenterName} - {AppInfo.RoleDisplayName(user.Role)}";
            CenterNameText.Text = AppInfo.CenterName;
            SidebarCenterName.Text = AppInfo.CenterName;
            SidebarTagline.Text = AppInfo.CenterTagline;
            var today = DateTime.Today;
            TodayText.Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(today.ToString("dddd", CultureInfo.CurrentCulture)) + ", " + today.ToString("dd/MM/yyyy");
            FooterText.Text = $"© {today.Year} {AppInfo.CenterName} · {AppInfo.CenterTagline} v{AppInfo.Version}";
            FooterEnvText.Text = $"Môi trường: {App.EnvironmentName}";

            LoadDashboard();

            SizeChanged += (_, _) => ApplyLayout();
            PreviewKeyDown += Window_PreviewKeyDown;
            Loaded += (_, _) => ApplyLayout();
        }

        private void LoadDashboard()
        {
            UserInfoTextBlock.Text = _currentUser.FullName;
            UserRoleText.Text = AppInfo.RoleDisplayName(_currentUser.Role);
            AvatarText.Text = AppInfo.Initials(_currentUser.FullName);

            var items = Navigation.ForUser(_currentUser);
            if (items.Count == 0)
            {
                MessageBox.Show("Vai trò không hợp lệ.");
                return;
            }

            var section = "";
            foreach (var item in items)
            {
                if (item.Section != section && item.Section.Length > 0)
                {
                    section = item.Section;
                    var label = new TextBlock
                    {
                        Text = section.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("SidebarMutedBrush"), Margin = new Thickness(14, NavPanel.Children.Count == 0 ? 0 : 14, 0, 6)
                    };
                    _sectionLabels.Add(label);
                    NavPanel.Children.Add(label);
                }
                var button = new RadioButton
                {
                    Style = (Style)FindResource("Nav.Item"),
                    GroupName = "MainNav",
                    Tag = item.Icon,
                    Content = item.Title,
                    ToolTip = item.Title
                };
                button.Checked += (_, _) => Navigate(item);
                NavPanel.Children.Add(button);
                _nav.Add((button, item));
            }
            _nav[0].Button.IsChecked = true;
            NavSectionLabel.Visibility = Visibility.Collapsed;
            BellButton.Visibility = _nav.Any(n => n.Item.Title == Navigation.NotificationsTitle) ? Visibility.Visible : Visibility.Collapsed;
            Loaded += async (_, _) => await InitNotificationsAsync();
        }

        private readonly List<TextBlock> _sectionLabels = new();

        /// <summary>Thành viên: sinh nhắc lịch/hết hạn gói; mọi vai trò: cập nhật số thông báo chưa đọc.</summary>
        private async Task InitNotificationsAsync()
        {
            try
            {
                if (_currentUser.Role == UserRole.Member)
                    await ServiceScope.Run(s => s.Notifications.GenerateRemindersAsync(_currentUser.Id));
                await RefreshNotificationsAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public async Task RefreshNotificationsAsync()
        {
            var unread = await ServiceScope.Run(s => s.Notifications.UnreadCountAsync(_currentUser.Id));
            BellBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            BellCount.Text = unread > 99 ? "99+" : unread.ToString();
            BellButton.ToolTip = unread > 0 ? $"{unread} thông báo chưa đọc" : "Thông báo";
        }

        private void BellButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Navigation.NotificationsTitle);

        /// <summary>Chuyển tới mục menu theo tên (dùng cho chuông thông báo và liên kết giữa các trang).</summary>
        public void NavigateTo(string title)
        {
            var target = _nav.FirstOrDefault(n => n.Item.Title == title);
            if (target.Button == null) return;
            if (target.Button.IsChecked == true) Navigate(target.Item);
            else target.Button.IsChecked = true;
        }

        private void Navigate(NavItem item)
        {
            PageTitleText.Text = item.Title;
            ContentArea.Content = item.Create();
            if (_mode == LayoutMode.Narrow) SetDrawer(false);
        }

        #region Bố cục thích ứng

        private void ApplyLayout()
        {
            var width = ActualWidth;
            _mode = width >= WideBreakpoint ? LayoutMode.Wide : width >= NarrowBreakpoint ? LayoutMode.Medium : LayoutMode.Narrow;

            var narrow = _mode == LayoutMode.Narrow;
            HeaderDate.Visibility = _mode == LayoutMode.Wide ? Visibility.Visible : Visibility.Collapsed;
            UserInfoPanel.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            CenterNameText.Visibility = BreadcrumbSeparator.Visibility = width < 560 ? Visibility.Collapsed : Visibility.Visible;
            FooterEnvText.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            ContentArea.Margin = new Thickness(narrow ? 12 : 24);

            if (narrow)
            {
                SidebarColumn.Width = new GridLength(0);
                Grid.SetColumnSpan(Sidebar, 2);
                Sidebar.HorizontalAlignment = HorizontalAlignment.Left;
                Sidebar.Width = SidebarFullWidth + 20;
                SetCompact(false);
                SetDrawer(_drawerOpen);
            }
            else
            {
                _drawerOpen = false;
                SidebarBackdrop.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(Sidebar, 1);
                Sidebar.HorizontalAlignment = HorizontalAlignment.Stretch;
                Sidebar.Width = double.NaN;
                Sidebar.Visibility = Visibility.Visible;
                var compact = _mode == LayoutMode.Medium || _userCollapsed;
                SidebarColumn.Width = new GridLength(compact ? SidebarCompactWidth : SidebarFullWidth);
                SetCompact(compact);
            }
        }

        private void SetCompact(bool compact)
        {
            Ui.SetIsCompact(Sidebar, compact);
            BrandText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            foreach (var l in _sectionLabels) l.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            BrandPanel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            BrandPanel.Margin = compact ? new Thickness(-6, 0, -6, 0) : new Thickness(0);
            foreach (var (button, _) in _nav) ToolTipService.SetIsEnabled(button, compact);
            ToolTipService.SetIsEnabled(SidebarLogout, compact);
        }

        private void SetDrawer(bool open)
        {
            _drawerOpen = open;
            Sidebar.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            SidebarBackdrop.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == LayoutMode.Narrow) SetDrawer(!_drawerOpen);
            else if (_mode == LayoutMode.Wide) { _userCollapsed = !_userCollapsed; ApplyLayout(); }
        }

        private void SidebarBackdrop_MouseDown(object sender, MouseButtonEventArgs e) => SetDrawer(false);

        #endregion

        #region Đăng xuất

        private void SidebarLogout_Checked(object sender, RoutedEventArgs e)
        {
            SidebarLogout.IsChecked = false;
            LogoutButton_Click(sender, e);
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try { await ServiceScope.Run(s => s.Auth.LogoutAsync()); } catch { /* vẫn đăng xuất khỏi giao diện */ }
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        #endregion

        #region IUiHost: hộp thoại và toast

        public Task<bool> ShowDialogAsync(FrameworkElement content)
        {
            _dialogTcs?.TrySetResult(false);
            _dialogTcs = new TaskCompletionSource<bool>();
            DialogHost.Content = content;
            DialogLayer.Visibility = Visibility.Visible;
            DialogLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            return _dialogTcs.Task;
        }

        public void CloseDialog(bool result)
        {
            DialogLayer.Visibility = Visibility.Collapsed;
            DialogHost.Content = null;
            var tcs = _dialogTcs;
            _dialogTcs = null;
            tcs?.TrySetResult(result);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (DialogLayer.Visibility == Visibility.Visible) { CloseDialog(false); e.Handled = true; }
            else if (_drawerOpen) { SetDrawer(false); e.Handled = true; }
        }

        public void ShowToast(string message, ToastKind kind)
        {
            var (icon, tone) = kind switch
            {
                ToastKind.Error => (Icons.Error, "Danger"),
                ToastKind.Warning => (Icons.Warning, "Warning"),
                ToastKind.Info => (Icons.Info, "Primary"),
                _ => (Icons.Success, "Success")
            };
            var accent = (Brush)FindResource(tone + "Brush");

            var content = new DockPanel();
            var close = new Button { Style = (Style)FindResource("Btn.Icon"), Content = Icons.Close, FontSize = 10, Width = 26, Height = 26, Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(close, Dock.Right);
            content.Children.Add(close);
            var iconBlock = new TextBlock { Text = icon, Style = (Style)FindResource("Text.Icon"), FontSize = 16, Foreground = accent, Margin = new Thickness(0, 0, 12, 0) };
            DockPanel.SetDock(iconBlock, Dock.Left);
            content.Children.Add(iconBlock);
            content.Children.Add(new TextBlock { Text = message, Style = (Style)FindResource("Text.Body"), VerticalAlignment = VerticalAlignment.Center });

            var toast = new Border
            {
                Width = Math.Min(380, Math.Max(240, ActualWidth - 48)),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(16, 12, 10, 12),
                Background = (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.15 },
                Child = new Border
                {
                    BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 0, 0, 0),
                    Child = content
                },
                RenderTransform = new TranslateTransform(0, 12)
            };

            void Remove()
            {
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                fade.Completed += (_, _) => ToastLayer.Children.Remove(toast);
                toast.BeginAnimation(OpacityProperty, fade);
            }
            close.Click += (_, _) => Remove();

            ToastLayer.Children.Add(toast);
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            toast.RenderTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == ToastKind.Error ? 6 : 3.5) };
            timer.Tick += (_, _) => { timer.Stop(); Remove(); };
            timer.Start();
        }

        #endregion
    }
}
