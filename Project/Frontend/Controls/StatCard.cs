using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project.Frontend.Controls
{
    /// <summary>Thẻ thống kê: icon màu + tiêu đề + giá trị lớn + dòng phụ (có thể tô màu xu hướng).</summary>
    public class StatCard : Border
    {
        private readonly TextBlock _value = new() { FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4), TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _subtitle = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };

        public StatCard(string icon, string tone, string title)
        {
            Style = (Style)Application.Current.FindResource("Card");
            Margin = new Thickness(8);
            _value.Foreground = Res("TextBrush");
            _subtitle.Foreground = Res("TextMutedBrush");

            var head = new DockPanel();
            var badge = new Border
            {
                Width = 42, Height = 42, CornerRadius = new CornerRadius(10),
                Background = Res(tone + "SoftBrush"),
                Child = new TextBlock
                {
                    Text = icon, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center,
                    Style = (Style)Application.Current.FindResource("Text.Icon"), Foreground = Res(tone + "Brush")
                }
            };
            DockPanel.SetDock(badge, Dock.Right);
            head.Children.Add(badge);
            head.Children.Add(new TextBlock
            {
                Text = title, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Res("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            });

            var root = new StackPanel();
            root.Children.Add(head);
            root.Children.Add(_value);
            root.Children.Add(_subtitle);
            Child = root;
            SetValue("—", "Đang tải...");
        }

        /// <param name="trend">Không null: tô màu dòng phụ (true = xanh, false = đỏ).</param>
        public void SetValue(string value, string subtitle, bool? trend = null)
        {
            _value.Text = value;
            _subtitle.Text = subtitle;
            _subtitle.Foreground = trend switch
            {
                true => Res("SuccessBrush"),
                false => Res("DangerBrush"),
                _ => Res("TextMutedBrush")
            };
        }

        private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
    }
}
