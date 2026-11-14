using SportsCenter.DAL.Data;
using SportsCenter.DAL.Models;

namespace SportsCenter.BLL.Services
{
    /// <summary>Kiểm tra quyền ở tầng nghiệp vụ: mỗi vai trò chỉ gọi được chức năng của mình.</summary>
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
