using System.Windows;
using Project.Frontend.Controls;
using Project.Frontend.Views.Coaching;
using Project.Frontend.Views.Common;
using Project.Frontend.Views.Manager;
using Project.Frontend.Views.MemberArea;
using Project.Frontend.Views.Shared;
using SportsCenter.DAL.Models;

namespace Project.Frontend.Shell
{
    /// <summary>Một mục trên sidebar. Section: nhóm hiển thị (tiêu đề nhỏ phía trên).</summary>
    public record NavItem(string Title, string Icon, Func<FrameworkElement> Create, string Section = "");

    /// <summary>Menu theo vai trò: mỗi vai trò chỉ thấy chức năng của mình (tầng service cũng kiểm tra quyền).</summary>
    public static class Navigation
    {
        public const string NotificationsTitle = "Thông báo";

        // Glyph Segoe Fluent Icons dùng riêng cho menu
        private const string IcoReport = "", IcoKey = "", IcoSport = "", IcoPayment = "", IcoBell = "";

        private static NavItem Soon(string title, string icon, string section) => new(title, icon, () => EmptyState.ComingSoon(title), section);

        public static IReadOnlyList<NavItem> ForUser(User user) => user.Role switch
        {
            UserRole.Manager => new[]
            {
                new NavItem("Tổng quan", Icons.Home, () => new DashboardPage(), "Tổng quan"),
                new NavItem("Báo cáo", IcoReport, () => new ReportsPage(), "Tổng quan"),
                new NavItem("Huấn luyện viên", Icons.Education, ManagerPages.Coaches, "Con người"),
                new NavItem("Tài khoản & quyền", IcoKey, ManagerPages.Accounts, "Con người"),
                new NavItem("Lớp học", Icons.Library, () => ManagerPages.Classes(), "Vận hành"),
                new NavItem("Lịch hoạt động", Icons.Calendar, () => ManagerPages.Sessions(), "Vận hành"),
                new NavItem("Bộ môn", IcoSport, ManagerPages.Sports, "Vận hành"),
                new NavItem("Phòng tập", Icons.Location, ManagerPages.Rooms, "Vận hành"),
                new NavItem("Gói thành viên", Icons.Money, ManagerPages.Plans, "Kinh doanh"),
            },
            UserRole.Coach => new[]
            {
                new NavItem("Lịch dạy", Icons.Calendar, () => ManagerPages.Sessions(Data.Me.CoachId ?? -1), "Giảng dạy"),
                new NavItem("Lớp của tôi", Icons.Library, () => ManagerPages.Classes(Data.Me.CoachId ?? -1), "Giảng dạy"),
                new NavItem("Học viên", Icons.People, CoachPages.Members, "Giảng dạy"),
                new NavItem("Kế hoạch tập", Icons.Edit, CoachPages.Plans, "Theo dõi"),
                new NavItem("Kết quả tập", Icons.Checklist, CoachPages.Logs, "Theo dõi"),
            },
            UserRole.Member => new[]
            {
                new NavItem("Lịch tập của tôi", Icons.Calendar, MemberPages.Schedule, "Tập luyện"),
                new NavItem("Lớp học", Icons.Library, MemberPages.Classes, "Tập luyện"),
                new NavItem("Gói thành viên", Icons.Money, MemberPages.Membership, "Tập luyện"),
                new NavItem("Điểm danh", Icons.Checklist, MemberPages.Attendance, "Tiến độ"),
                new NavItem("Kết quả & nhận xét", Icons.Success, MemberPages.Progress, "Tiến độ"),
                new NavItem("Kế hoạch tập", Icons.Edit, MemberPages.Plans, "Tiến độ"),
            },
            UserRole.Receptionist => new[]
            {
                new NavItem("Check-in", Icons.Checklist, Views.Reception.ReceptionPages.CheckIn, "Quầy lễ tân"),
                new NavItem("Thành viên", Icons.People, () => OperationsPages.Members(), "Quầy lễ tân"),
                new NavItem("Đăng ký gói", Icons.Money, OperationsPages.Subscriptions, "Quầy lễ tân"),
                new NavItem("Thanh toán & hóa đơn", IcoPayment, () => OperationsPages.Payments(), "Quầy lễ tân"),
            },
            _ => Array.Empty<NavItem>()
        };
    }
}
