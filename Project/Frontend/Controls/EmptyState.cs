using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project.Frontend.Controls
{
    /// <summary>Khối "trạng thái trống": icon tròn + tiêu đề + mô tả, căn giữa.</summary>
    public class EmptyState : StackPanel
    {
        public EmptyState(string icon, string title, string description)
        {
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(24);
            MaxWidth = 420;

            Children.Add(new Border
            {
                Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Margin = new Thickness(0, 0, 0, 16),
                Background = (Brush)Application.Current.FindResource("PrimarySoftBrush"),
                Child = new TextBlock
                {
                    Text = icon, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center,
                    Style = (Style)Application.Current.FindResource("Text.Icon"),
                    Foreground = (Brush)Application.Current.FindResource("PrimaryBrush")
                }
            });
            Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.FindResource("Text.H3"), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            Children.Add(new TextBlock { Text = description, Style = (Style)Application.Current.FindResource("Text.Secondary"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
        }

        public static FrameworkElement ComingSoon(string feature) =>
            new EmptyState(Icons.Construction, feature, "Tính năng này đang được phát triển và sẽ sớm ra mắt.");
    }

    /// <summary>Phiên bản dùng được trong XAML: &lt;c:EmptyStateHost Icon="..." Title="..." Description="..."/&gt;</summary>
    public class EmptyStateHost : ContentControl
    {
        public string Icon { get; set; } = Icons.Info;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Focusable = false;
            Content = new EmptyState(Icon, Title, Description);
        }
    }
}
