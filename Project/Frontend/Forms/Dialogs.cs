using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Shell;

namespace Project.Frontend.Forms
{
    /// <summary>Một mục thông tin: tiêu đề + các cặp nhãn/giá trị, hoặc một đoạn văn dài (Body).</summary>
    public record InfoSection(string Title, IReadOnlyList<(string Label, string Value)>? Rows = null, string? Body = null);

    internal static class DialogChrome
    {
        public static T R<T>(string key) => (T)Application.Current.FindResource(key);

        /// <summary>Khung chung: header (icon + tiêu đề + phụ đề + nút đóng), nội dung, footer.</summary>
        public static Border Build(string title, string subtitle, string icon, UIElement body, UIElement? footer, double maxWidth)
        {
            var card = new Border
            {
                Style = R<Style>("Card"), Padding = new Thickness(0), MaxWidth = maxWidth,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 10, Direction = 270, Opacity = 0.22 }
            };
            var root = new DockPanel();
            var header = new DockPanel { Margin = new Thickness(24, 20, 16, 16) };
            var close = new Button { Style = R<Style>("Btn.Icon"), Content = Icons.Close, FontSize = 11, ToolTip = "Đóng (Esc)", VerticalAlignment = VerticalAlignment.Top };
            System.Windows.Automation.AutomationProperties.SetAutomationId(close, "DialogClose");
            close.Click += (_, _) => UiHost.CloseDialog(false);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var iconBox = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = R<Brush>("PrimarySoftBrush"), Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock { Text = icon, Style = R<Style>("Text.Icon"), FontSize = 20, Foreground = R<Brush>("PrimaryBrush"), HorizontalAlignment = HorizontalAlignment.Center }
            };
            DockPanel.SetDock(iconBox, Dock.Left);
            header.Children.Add(iconBox);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = title, Style = R<Style>("Text.H2"), TextWrapping = TextWrapping.Wrap });
            if (subtitle.Length > 0) titles.Children.Add(new TextBlock { Text = subtitle, Style = R<Style>("Text.Secondary"), Margin = new Thickness(0, 2, 0, 0) });
            header.Children.Add(titles);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            if (footer != null)
            {
                var f = new Border { BorderBrush = R<Brush>("LineBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 14, 24, 16), Background = R<Brush>("SurfaceAltBrush"), CornerRadius = new CornerRadius(0, 0, 12, 12), Child = footer };
                DockPanel.SetDock(f, Dock.Bottom);
                root.Children.Add(f);
            }
            root.Children.Add(body);
            card.Child = root;
            return card;
        }
    }

    /// <summary>Hộp thoại xem chi tiết (chỉ đọc) với các nút hành động tùy chọn.</summary>
    public static class InfoDialog
    {
        public static Task<bool> ShowAsync(string title, string subtitle, string icon, IEnumerable<InfoSection> sections,
            params (string Text, string Icon, Func<Task<bool>> Run)[] actions)
        {
            var body = new StackPanel { Margin = new Thickness(24, 0, 24, 20) };
            foreach (var s in sections)
            {
                body.Children.Add(new TextBlock { Text = s.Title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = DialogChrome.R<Brush>("TextMutedBrush"), Margin = new Thickness(0, 12, 0, 8) });
                if (s.Rows != null)
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    foreach (var (label, value) in s.Rows)
                    {
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        var l = new TextBlock { Text = label, Style = DialogChrome.R<Style>("Text.Secondary"), Margin = new Thickness(0, 4, 12, 4) };
                        var v = new TextBlock { Text = string.IsNullOrWhiteSpace(value) ? "—" : value, Style = DialogChrome.R<Style>("Text.Body"), Margin = new Thickness(0, 4, 0, 4) };
                        Grid.SetRow(l, grid.RowDefinitions.Count - 1); Grid.SetRow(v, grid.RowDefinitions.Count - 1); Grid.SetColumn(v, 1);
                        grid.Children.Add(l); grid.Children.Add(v);
                    }
                    body.Children.Add(grid);
                }
                if (s.Body != null)
                    body.Children.Add(new Border
                    {
                        Background = DialogChrome.R<Brush>("SurfaceAltBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10),
                        Child = new TextBlock { Text = string.IsNullOrWhiteSpace(s.Body) ? "—" : s.Body, Style = DialogChrome.R<Style>("Text.Body"), LineHeight = 20 }
                    });
            }
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 560 };

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var (text, actionIcon, run) in actions)
            {
                var b = TablePage<object>.IconTextButton(text, actionIcon, "Btn.Secondary");
                b.Margin = new Thickness(0, 0, 10, 0);
                b.Click += async (_, _) => { if (await run()) UiHost.CloseDialog(true); };
                footer.Children.Add(b);
            }
            var close = new Button { Content = "Đóng", Style = DialogChrome.R<Style>("Btn.Primary"), MinWidth = 90 };
            close.Click += (_, _) => UiHost.CloseDialog(false);
            footer.Children.Add(close);
            return UiHost.ShowDialogAsync(DialogChrome.Build(title, subtitle, icon, scroll, footer, 680));
        }
    }

    /// <summary>Nhúng một trang (vd. TablePage) vào hộp thoại lớn.</summary>
    public static class HostDialog
    {
        public static Task<bool> ShowAsync(string title, string subtitle, string icon, FrameworkElement content, double width = 980, double height = 600)
        {
            var host = new Border { Padding = new Thickness(24, 0, 24, 20), Height = height, Child = content };
            var card = DialogChrome.Build(title, subtitle, icon, host, null, width);
            card.Width = width;
            card.Loaded += (_, _) =>
            {
                if (Window.GetWindow(card) is { } w)
                {
                    void Fit() { card.Width = Math.Max(320, Math.Min(width, w.ActualWidth - 64)); host.Height = Math.Max(300, Math.Min(height, w.ActualHeight - 220)); }
                    w.SizeChanged += (_, _) => Fit(); Fit();
                }
            };
            return UiHost.ShowDialogAsync(card);
        }
    }
}
