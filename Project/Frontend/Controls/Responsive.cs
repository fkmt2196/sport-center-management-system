using System.Windows;
using System.Windows.Controls;

namespace Project.Frontend.Controls
{
    /// <summary>
    /// Ẩn cột phụ của DataGrid khi bảng hẹp.
    /// XAML: &lt;DataGridTextColumn c:Responsive.HideBelow="760" .../&gt; và &lt;DataGrid c:Responsive.Enabled="True"&gt;
    /// </summary>
    public static class Responsive
    {
        public static readonly DependencyProperty HideBelowProperty =
            DependencyProperty.RegisterAttached("HideBelow", typeof(double), typeof(Responsive), new PropertyMetadata(0.0));
        public static double GetHideBelow(DependencyObject o) => (double)o.GetValue(HideBelowProperty);
        public static void SetHideBelow(DependencyObject o, double v) => o.SetValue(HideBelowProperty, v);

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(Responsive), new PropertyMetadata(false, OnEnabledChanged));
        public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid && (bool)e.NewValue)
            {
                // DataGrid co cột độ rộng cố định xuống MinWidth (mặc định 20) khi bảng hẹp lại
                // -> khóa MinWidth = Width để cột không bị bóp méo.
                grid.Loaded += (_, _) =>
                {
                    foreach (var col in grid.Columns)
                        if (col.Width.IsAbsolute && col.MinWidth < col.Width.Value) col.MinWidth = col.Width.Value;
                    Apply(grid);
                };
                grid.SizeChanged += (_, _) => Apply(grid);
            }
        }

        // Độ rộng khai báo ban đầu của từng cột (để khôi phục sau khi ẩn/hiện).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridColumn, object> OriginalWidths = new();

        public static void Apply(DataGrid grid)
        {
            var width = grid.ActualWidth;
            if (width <= 0) return;
            var changed = false;
            foreach (var col in grid.Columns)
            {
                var hideBelow = GetHideBelow(col);
                if (hideBelow <= 0) continue;
                var target = width < hideBelow ? Visibility.Collapsed : Visibility.Visible;
                if (col.Visibility != target) { col.Visibility = target; changed = true; }
            }
            if (!changed) return;

            // WPF không tự chia lại độ rộng "*" khi cột đổi Visibility -> gán lại độ rộng gốc.
            foreach (var col in grid.Columns)
            {
                var original = (DataGridLength)OriginalWidths.GetValue(col, c => c.Width);
                col.Width = DataGridLength.Auto;
                col.Width = original;
            }
        }
    }
}
