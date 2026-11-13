using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Project.Frontend.Controls;

namespace Project.Frontend.Shell
{
    /// <summary>Hộp thoại xác nhận hiển thị trong lớp phủ của cửa sổ chính.</summary>
    public class ConfirmDialog : Border
    {
        public ConfirmDialog(string title, string message, string confirmText, bool danger)
        {
            Style = (Style)FindResource("Card");
            Padding = new Thickness(24);
            MaxWidth = 440;
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 8, Direction = 270, Opacity = 0.2 };

            var root = new StackPanel();

            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            head.Children.Add(new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Margin = new Thickness(0, 0, 14, 0),
                Background = (Brush)FindResource(danger ? "DangerSoftBrush" : "PrimarySoftBrush"),
                Child = new TextBlock
                {
                    Text = danger ? Icons.Warning : Icons.Info, Style = (Style)FindResource("Text.Icon"), FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (Brush)FindResource(danger ? "DangerBrush" : "PrimaryBrush")
                }
            });
            head.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Text.H2"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 330 });
            root.Children.Add(head);

            root.Children.Add(new TextBlock { Text = message, Style = (Style)FindResource("Text.Secondary"), Margin = new Thickness(54, 0, 0, 24) });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Hủy", Style = (Style)FindResource("Btn.Secondary"), Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
            cancel.Click += (_, _) => UiHost.CloseDialog(false);
            var ok = new Button { Content = confirmText, Style = (Style)FindResource(danger ? "Btn.Danger" : "Btn.Primary") };
            ok.Click += (_, _) => UiHost.CloseDialog(true);
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancel, "ConfirmCancel");
            System.Windows.Automation.AutomationProperties.SetAutomationId(ok, "ConfirmOk");
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            Child = root;
            Loaded += (_, _) => cancel.Focus();
        }
    }
}
