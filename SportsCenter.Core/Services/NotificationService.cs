using Microsoft.EntityFrameworkCore;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Services
{
    public class NotificationService
    {
        private readonly SportsCenterContext _db;
        public NotificationService(SportsCenterContext db) { _db = db; }

        public Task<List<Notification>> ForUserAsync(int userId) =>
            _db.Notifications.AsNoTracking().Include(n => n.Sender)
                .Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<int> UnreadCountAsync(int userId) =>
            _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task MarkReadAsync(int notificationId)
        {
            var n = await _db.Notifications.FirstAsync(x => x.Id == notificationId);
            if (n.UserId != AppSession.UserId && AppSession.IsSignedIn) return;
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }

        public async Task MarkAllReadAsync(int userId)
        {
            await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        /// <summary>Thêm thông báo cho nhiều tài khoản (chưa SaveChanges - để gộp vào giao dịch của nghiệp vụ gọi nó).</summary>
        internal void Queue(IEnumerable<int> userIds, string title, string body, NotificationType type, bool fromSystem = false)
        {
            foreach (var id in userIds.Distinct())
                _db.Notifications.Add(new Notification { UserId = id, SenderUserId = fromSystem ? null : AppSession.UserId, Title = title, Body = body, Type = type });
        }

        /// <summary>HLV/Quản lý gửi thông báo hoặc bài tập về nhà cho một thành viên.</summary>
        public async Task SendToMemberAsync(int memberId, string title, string body, NotificationType type)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager, UserRole.Receptionist);
            var userId = await _db.Members.Where(m => m.Id == memberId).Select(m => m.UserId).FirstAsync();
            Queue(new[] { userId }, title, body, type);
            await _db.SaveChangesAsync();
        }

        /// <summary>Gửi cho toàn bộ thành viên đang học một lớp. Trả về số người nhận.</summary>
        public async Task<int> SendToClassAsync(int classId, string title, string body, NotificationType type)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            var userIds = await _db.Enrollments.Where(e => e.ClassId == classId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.Member!.UserId).ToListAsync();
            Queue(userIds, title, body, type);
            await _db.SaveChangesAsync();
            return userIds.Count;
        }

        /// <summary>
        /// Nhắc tự động cho thành viên khi đăng nhập: gói sắp hết hạn (≤ 7 ngày) và buổi tập ngày mai.
        /// Không tạo trùng nếu đã nhắc.
        /// </summary>
        public async Task GenerateRemindersAsync(int userId)
        {
            var member = await _db.Members.Include(m => m.Subscriptions).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(m => m.UserId == userId);
            if (member == null) return;
            var today = DateTime.Today;

            var end = member.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).Select(s => (DateTime?)s.EndDate).Max();
            if (end.HasValue && end.Value >= today && end.Value <= today.AddDays(7))
            {
                var title = $"Gói thành viên sắp hết hạn ({end:dd/MM/yyyy})";
                if (!await _db.Notifications.AnyAsync(n => n.UserId == userId && n.Title == title))
                    Queue(new[] { userId }, title, $"Gói của bạn sẽ hết hạn vào ngày {end:dd/MM/yyyy}. Hãy gia hạn để không bị gián đoạn lịch tập.", NotificationType.Expiry, fromSystem: true);
            }
            else if (end.HasValue && end.Value < today && end.Value >= today.AddDays(-30))
            {
                var title = $"Gói thành viên đã hết hạn ({end:dd/MM/yyyy})";
                if (!await _db.Notifications.AnyAsync(n => n.UserId == userId && n.Title == title))
                    Queue(new[] { userId }, title, "Gói của bạn đã hết hạn. Vui lòng gia hạn tại quầy hoặc trong mục Gói thành viên.", NotificationType.Expiry, fromSystem: true);
            }

            var tomorrow = today.AddDays(1);
            var sessions = await _db.Sessions.Include(s => s.Class).Include(s => s.Room)
                .Where(s => s.Status == SessionStatus.Scheduled && s.StartTime >= tomorrow && s.StartTime < tomorrow.AddDays(1)
                            && s.Class!.Enrollments.Any(e => e.MemberId == member.Id && e.Status == EnrollmentStatus.Active))
                .ToListAsync();
            foreach (var s in sessions)
            {
                var title = $"Nhắc lịch: {s.Class!.Name} ngày {s.StartTime:dd/MM}";
                if (!await _db.Notifications.AnyAsync(n => n.UserId == userId && n.Title == title))
                    Queue(new[] { userId }, title, $"Bạn có buổi tập {s.Class.Name} lúc {s.StartTime:HH:mm} ngày {s.StartTime:dd/MM/yyyy} tại {s.Room?.Name}.", NotificationType.General, fromSystem: true);
            }
            await _db.SaveChangesAsync();
        }
    }
}
