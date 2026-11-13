using SportsCenter.Core.Models;

namespace SportsCenter.Core.Data
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

    /// <summary>Kiểm tra quyền ở tầng service: mỗi vai trò chỉ gọi được chức năng của mình.</summary>
    public static class Authorization
    {
        /// <summary>Ném lỗi nếu người đang đăng nhập không thuộc các vai trò cho phép (bỏ qua khi chạy nền / seed).</summary>
        public static void Require(params UserRole[] roles)
        {
            if (!AppSession.IsSignedIn) return;
            if (AppSession.Role is UserRole r && roles.Contains(r)) return;
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");
        }

        public static bool Is(params UserRole[] roles) => AppSession.Role is UserRole r && roles.Contains(r);
    }
}
