using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Project.Frontend.Controls
{
    /// <summary>
    /// Badge màu cho trạng thái. Kind: "member" | "class" | "attendance" | "coach".
    /// Ví dụ: &lt;c:StatusBadge Kind="class" Status="{Binding Status}"/&gt;
    /// </summary>
    public class StatusBadge : Border
    {
        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(nameof(Kind), typeof(string), typeof(StatusBadge), new PropertyMetadata("", (d, _) => ((StatusBadge)d).Refresh()));
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(StatusBadge), new PropertyMetadata("", (d, _) => ((StatusBadge)d).Refresh()));

        public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
        public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

        private readonly Ellipse _dot = new() { Width = 6, Height = 6, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _text = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

        public StatusBadge()
        {
            CornerRadius = new CornerRadius(999);
            Padding = new Thickness(10, 3, 10, 3);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(_dot);
            panel.Children.Add(_text);
            Child = panel;
            Refresh();
        }

        /// <summary>
        /// Nhãn tiếng Việt + tông màu cho từng loại trạng thái.
        /// Kind: membership | class | session | enroll | attendance | coach | account | role | support | level | plan | notify | payment | purpose
        /// </summary>
        public static (string Label, string Tone) Describe(string kind, string? status) => (kind, status) switch
        {
            ("membership", "Active") => ("Còn hạn", "Success"),
            ("membership", "Expiring") => ("Sắp hết hạn", "Warning"),
            ("membership", "Expired") => ("Hết hạn", "Danger"),
            ("membership", "Upcoming") => ("Chưa bắt đầu", "Info"),
            ("membership", "None") => ("Chưa có gói", "Neutral"),
            ("membership", "Cancelled") => ("Đã hủy", "Neutral"),
            ("class", "Open") => ("Đang mở", "Success"),
            ("class", "Completed") => ("Đã kết thúc", "Neutral"),
            ("class", "Cancelled") => ("Đã hủy", "Danger"),
            ("session", "Scheduled") => ("Sắp diễn ra", "Primary"),
            ("session", "Completed") => ("Đã diễn ra", "Neutral"),
            ("session", "Cancelled") => ("Đã hủy", "Danger"),
            ("session", "Today") => ("Hôm nay", "Warning"),
            ("enroll", "Active") => ("Đang học", "Success"),
            ("enroll", "Cancelled") => ("Đã hủy", "Neutral"),
            ("enroll", "None") => ("Chưa đăng ký", "Neutral"),
            ("enroll", "Full") => ("Hết chỗ", "Danger"),
            ("attendance", "Present") => ("Có mặt", "Success"),
            ("attendance", "Late") => ("Đi muộn", "Warning"),
            ("attendance", "Absent") => ("Vắng", "Danger"),
            ("attendance", "Excused") => ("Có phép", "Info"),
            ("attendance", "None") => ("Chưa điểm danh", "Neutral"),
            ("coach", "True") => ("Nhận lớp", "Success"),
            ("coach", "False") => ("Tạm ngưng", "Neutral"),
            ("account", "True") => ("Hoạt động", "Success"),
            ("account", "False") => ("Đã khóa", "Danger"),
            ("role", "Manager") => ("Quản lý", "Violet"),
            ("role", "Receptionist") => ("Lễ tân", "Info"),
            ("role", "Coach") => ("HLV", "Warning"),
            ("role", "Member") => ("Thành viên", "Primary"),
            ("support", "Open") => ("Mới", "Danger"),
            ("support", "InProgress") => ("Đang xử lý", "Warning"),
            ("support", "Resolved") => ("Đã xong", "Success"),
            ("level", "Beginner") => ("Mới bắt đầu", "Success"),
            ("level", "Intermediate") => ("Trung bình", "Warning"),
            ("level", "Advanced") => ("Nâng cao", "Danger"),
            ("plan", "True") => ("Đang bán", "Success"),
            ("plan", "False") => ("Ngừng bán", "Neutral"),
            ("notify", "General") => ("Thông báo", "Primary"),
            ("notify", "Homework") => ("Bài tập", "Violet"),
            ("notify", "ScheduleChange") => ("Đổi lịch", "Warning"),
            ("notify", "Expiry") => ("Gia hạn", "Danger"),
            ("payment", "Cash") => ("Tiền mặt", "Success"),
            ("payment", "Card") => ("Thẻ", "Info"),
            ("payment", "Transfer") => ("Chuyển khoản", "Primary"),
            ("payment", "Online") => ("Online", "Violet"),
            ("purpose", "Subscription") => ("Gói thành viên", "Primary"),
            ("purpose", "ClassFee") => ("Học phí lớp", "Violet"),
            ("purpose", "Other") => ("Khác", "Neutral"),
            ("progress", "Log") => ("Kết quả tập", "Primary"),
            ("progress", "Review") => ("Nhận xét", "Violet"),
            ("read", "Unread") => ("Chưa đọc", "Danger"),
            ("read", "Read") => ("Đã đọc", "Neutral"),
            ("active", "True") => ("Hoạt động", "Success"),
            ("active", "False") => ("Ngừng", "Neutral"),
            _ => (status ?? "", "Neutral")
        };

        private void Refresh()
        {
            var (label, tone) = Describe(Kind, Status);
            var fg = FindBrush(tone == "Primary" ? "PrimaryBrush" : tone + "Brush");
            Background = FindBrush(tone == "Primary" ? "PrimarySoftBrush" : tone + "SoftBrush");
            _text.Text = label;
            _text.Foreground = fg;
            _dot.Fill = fg;
        }

        private static Brush FindBrush(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
}
