using SportsCenter.DAL.Models;

namespace SportsCenter.DAL.Data
{
    /// <summary>
    /// Người dùng đang đăng nhập (dùng chung cho kiểm tra quyền và ghi audit log).
    /// Ứng dụng desktop chỉ có một phiên tại một thời điểm nên dùng trạng thái tĩnh.
    /// </summary>
    public static class AppSession
    {
        public static int? UserId { get; private set; }
        public static string Username { get; private set; } = "system";
        public static string FullName { get; private set; } = "Hệ thống";
        public static UserRole? Role { get; private set; }
        public static bool IsSignedIn => UserId.HasValue;

        public static void SignIn(User user)
        {
            UserId = user.Id;
            Username = user.Username;
            FullName = user.FullName;
            Role = user.Role;
        }

        public static void SignOut()
        {
            UserId = null;
            Username = "system";
            FullName = "Hệ thống";
            Role = null;
        }
    }
}
