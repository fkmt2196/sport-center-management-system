using SportsCenter.Core.Models;

namespace Project.Frontend
{
    /// <summary>Thông tin hiển thị của trung tâm (đổi tên/khẩu hiệu tại đây).</summary>
    public static class AppInfo
    {
        public const string CenterName = "FitZone Sports Center";
        public const string CenterTagline = "Quản lý trung tâm thể thao";
        public const string Version = "2.0";

        public static string RoleDisplayName(UserRole role) => role switch
        {
            UserRole.Manager => "Quản lý trung tâm",
            UserRole.Receptionist => "Lễ tân",
            UserRole.Coach => "Huấn luyện viên",
            UserRole.Member => "Thành viên",
            _ => role.ToString()
        };

        /// <summary>"Nguyễn Văn An" -> "VA" (dùng cho avatar).</summary>
        public static string Initials(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[^2][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}
