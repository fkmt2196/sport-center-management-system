using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project.Frontend.Controls
{
    /// <summary>Thanh phân trang: "Hiển thị 1–10 trên 50" · số dòng/trang · ‹ 1 2 3 … 5 ›</summary>
    public class Pager : DockPanel
    {
        public event Action<int>? PageRequested;
        public event Action<int>? PageSizeChanged;

        private readonly TextBlock _summary = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 16, 4) };
        private readonly WrapPanel _buttons = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        private readonly ComboBox _size = new() { Width = 76, MinHeight = 34, Margin = new Thickness(8, 0, 16, 0) };
        private int _page = 1, _pageCount = 1;

        public static readonly int[] PageSizes = { 10, 20, 50 };

        public Pager()
        {
            LastChildFill = false;
            _summary.Style = (Style)Application.Current.FindResource("Text.Secondary");
            foreach (var s in PageSizes) _size.Items.Add(s);
            _size.SelectedIndex = 0;
            _size.SelectionChanged += (_, _) => { if (_size.SelectedItem is int s) PageSizeChanged?.Invoke(s); };

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(new TextBlock { Text = "Số dòng", Style = (Style)Application.Current.FindResource("Text.Secondary"), VerticalAlignment = VerticalAlignment.Center });
            right.Children.Add(_size);
            right.Children.Add(_buttons);

            SetDock(_summary, Dock.Left);
            SetDock(right, Dock.Right);
            Children.Add(_summary);
            Children.Add(right);

            // Hẹp: ẩn chọn số dòng, rồi ẩn dòng tóm tắt, để nút trang không bị tràn.
            _sizeLabel = (TextBlock)right.Children[0];
            SizeChanged += (_, _) =>
            {
                var compactSize = ActualWidth < 640 ? Visibility.Collapsed : Visibility.Visible;
                _sizeLabel.Visibility = compactSize;
                _size.Visibility = compactSize;
                _summary.Visibility = ActualWidth < 440 ? Visibility.Collapsed : Visibility.Visible;
            };
        }

        private readonly TextBlock _sizeLabel;

        public void Update(int page, int pageCount, int total, int pageSize)
        {
            _page = page; _pageCount = Math.Max(1, pageCount);
            var from = total == 0 ? 0 : (page - 1) * pageSize + 1;
            var to = Math.Min(total, page * pageSize);
            _summary.Text = total == 0 ? "Không có kết quả" : $"Hiển thị {from}–{to} trên {total} kết quả";

            _buttons.Children.Clear();
            _buttons.Children.Add(NavButton(Icons.ChevronLeft, page - 1, page > 1, "Trang trước"));
            foreach (var p in PageWindow(page, _pageCount))
            {
                if (p == 0)
                    _buttons.Children.Add(new TextBlock { Text = "…", Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.FindResource("TextMutedBrush") });
                else
                {
                    var b = new Button { Content = p.ToString(), Style = (Style)Application.Current.FindResource(p == page ? "Btn.PageActive" : "Btn.Page"), Margin = new Thickness(2, 0, 2, 0) };
                    var target = p;
                    b.Click += (_, _) => PageRequested?.Invoke(target);
                    _buttons.Children.Add(b);
                }
            }
            _buttons.Children.Add(NavButton(Icons.ChevronRight, page + 1, page < _pageCount, "Trang sau"));
        }

        private Button NavButton(string glyph, int target, bool enabled, string tip)
        {
            var b = new Button
            {
                Content = glyph, Style = (Style)Application.Current.FindResource("Btn.Page"), IsEnabled = enabled, ToolTip = tip,
                FontFamily = (FontFamily)Application.Current.FindResource("IconFont"), FontSize = 11, Margin = new Thickness(2, 0, 2, 0)
            };
            b.Click += (_, _) => PageRequested?.Invoke(target);
            return b;
        }

        /// <summary>Các số trang cần hiển thị; 0 = dấu "…".</summary>
        private static IEnumerable<int> PageWindow(int page, int count)
        {
            if (count <= 7) { for (int i = 1; i <= count; i++) yield return i; yield break; }
            var start = Math.Max(2, page - 1);
            var end = Math.Min(count - 1, page + 1);
            if (page <= 3) { start = 2; end = 4; }
            if (page >= count - 2) { start = count - 3; end = count - 1; }
            yield return 1;
            if (start > 2) yield return 0;
            for (int i = start; i <= end; i++) yield return i;
            if (end < count - 1) yield return 0;
            yield return count;
        }
    }
}
