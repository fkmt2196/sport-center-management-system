using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Project.Frontend.Controls
{
    public record BarPoint(string Label, double Value, string Tooltip, bool Highlight = false);

    /// <summary>
    /// Biểu đồ cột đơn giản (không cần thư viện ngoài): trục Y có lưới, nhãn giá trị trên cột,
    /// cột được nhấn mạnh tô màu đậm, rê chuột hiện tooltip. Tự vẽ lại khi đổi kích thước.
    /// </summary>
    public class BarChart : Canvas
    {
        private IReadOnlyList<BarPoint> _data = Array.Empty<BarPoint>();
        private const double AxisWidth = 36, LabelHeight = 26, TopPad = 22;

        public BarChart()
        {
            ClipToBounds = true;
            SizeChanged += (_, _) => Render();
        }

        public void SetData(IReadOnlyList<BarPoint> data)
        {
            _data = data;
            Render();
        }

        private static double NiceMax(double max)
        {
            if (max <= 0) return 4;
            var exp = Math.Pow(10, Math.Floor(Math.Log10(max)));
            foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
                if (m * exp >= max) return m * exp;
            return 10 * exp;
        }

        private void Render()
        {
            Children.Clear();
            if (_data.Count == 0 || ActualWidth < 50 || ActualHeight < 80) return;

            var res = Application.Current;
            var line = (Brush)res.FindResource("LineBrush");
            var muted = (Brush)res.FindResource("TextMutedBrush");
            var text = (Brush)res.FindResource("TextBrush");
            var primary = (Brush)res.FindResource("PrimaryBrush");
            var hover = (Brush)res.FindResource("PrimaryHoverBrush");
            var soft = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD));

            var plotW = ActualWidth - AxisWidth;
            var plotH = ActualHeight - LabelHeight - TopPad;
            var max = NiceMax(_data.Max(d => d.Value));
            const int ticks = 4;

            // Lưới ngang + nhãn trục Y
            for (int i = 0; i <= ticks; i++)
            {
                var y = TopPad + plotH - plotH * i / ticks;
                var gl = new Line { X1 = AxisWidth, X2 = ActualWidth, Y1 = y, Y2 = y, Stroke = line, StrokeThickness = 1, SnapsToDevicePixels = true };
                if (i > 0) gl.StrokeDashArray = new DoubleCollection { 4, 4 };
                Children.Add(gl);
                var lbl = new TextBlock { Text = (max * i / ticks).ToString("0.#"), FontSize = 11, Foreground = muted, Width = AxisWidth - 8, TextAlignment = TextAlignment.Right };
                SetLeft(lbl, 0); SetTop(lbl, y - 8);
                Children.Add(lbl);
            }

            var slot = plotW / _data.Count;
            var barW = Math.Min(56, slot * 0.55);
            for (int i = 0; i < _data.Count; i++)
            {
                var d = _data[i];
                var h = Math.Max(d.Value > 0 ? 3 : 0, plotH * d.Value / max);
                var x = AxisWidth + slot * i + (slot - barW) / 2;
                var fill = d.Highlight ? primary : soft;

                var bar = new Border
                {
                    Width = barW, Height = h, Background = fill, CornerRadius = new CornerRadius(6, 6, 0, 0),
                    ToolTip = d.Tooltip, Cursor = System.Windows.Input.Cursors.Hand
                };
                bar.MouseEnter += (_, _) => bar.Background = hover;
                bar.MouseLeave += (_, _) => bar.Background = fill;
                SetLeft(bar, x); SetTop(bar, TopPad + plotH - h);
                Children.Add(bar);

                var val = new TextBlock { Text = d.Value.ToString("0.#"), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = text, Width = slot, TextAlignment = TextAlignment.Center };
                SetLeft(val, AxisWidth + slot * i); SetTop(val, TopPad + plotH - h - 18);
                Children.Add(val);

                var lab = new TextBlock { Text = d.Label, FontSize = 12, Foreground = d.Highlight ? text : muted, FontWeight = d.Highlight ? FontWeights.SemiBold : FontWeights.Normal, Width = slot, TextAlignment = TextAlignment.Center };
                SetLeft(lab, AxisWidth + slot * i); SetTop(lab, TopPad + plotH + 6);
                Children.Add(lab);
            }
        }
    }
}
