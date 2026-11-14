using SportsCenter.DAL.Models;

namespace Project.Frontend.Data
{
    /// <summary>Người dùng đang đăng nhập phía giao diện (kèm mã hồ sơ thành viên/HLV nếu có).</summary>
    public static class Me
    {
        public static User User { get; private set; } = new();
        public static int? MemberId { get; private set; }
        public static int? CoachId { get; private set; }

        public static void Init(User user, int? memberId, int? coachId)
        {
            User = user;
            MemberId = memberId;
            CoachId = coachId;
        }
    }
}
