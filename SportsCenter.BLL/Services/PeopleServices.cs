using Microsoft.EntityFrameworkCore;
using SportsCenter.DAL.Data;
using SportsCenter.DAL.Models;

namespace SportsCenter.BLL.Services
{
    /// <summary>Dữ liệu nhập hồ sơ thành viên.</summary>
    public record MemberInput(
        string FullName, string Phone, string Email, Gender Gender, DateTime? DateOfBirth,
        double? HeightCm, double? WeightKg, FitnessLevel Level, string Goals, string HealthNotes);

    /// <summary>Dữ liệu nhập hồ sơ huấn luyện viên.</summary>
    public record CoachInput(
        string FullName, string Phone, string Email, string Specialty, string Certifications,
        string Bio, int YearsExperience, bool IsAvailable);

    // =====================================================================
    // TÀI KHOẢN & PHÂN QUYỀN (Quản lý)
    // =====================================================================
    public class AccountService
    {
        private readonly SportsCenterContext _db;
        internal AccountService(SportsCenterContext db) { _db = db; }

        public Task<List<User>> ListAsync()
        {
            Authorization.Require(UserRole.Manager);
            return _db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
        }

        /// <summary>Tạo tài khoản; tự tạo hồ sơ Member/Coach tương ứng với vai trò.</summary>
        public async Task<User> CreateAsync(string username, string password, string fullName, string phone, string email, UserRole role)
        {
            Authorization.Require(UserRole.Manager);
            username = username.Trim();
            if (await _db.Users.AnyAsync(u => u.Username == username))
                throw new ValidationException("Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.", "Username");

            var user = new User
            {
                Username = username, PasswordHash = PasswordHasher.Hash(password), FullName = fullName.Trim(),
                Phone = phone.Trim(), Email = email.Trim(), Role = role
            };
            if (role == UserRole.Member) user.Member = new Member();
            if (role == UserRole.Coach) user.Coach = new Coach();
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        /// <summary>Cập nhật thông tin, vai trò (phân quyền) và trạng thái khóa.</summary>
        public async Task UpdateAsync(int userId, string fullName, string phone, string email, UserRole role, bool isActive)
        {
            Authorization.Require(UserRole.Manager);
            var user = await _db.Users.Include(u => u.Member).Include(u => u.Coach).FirstAsync(u => u.Id == userId);
            if (userId == AppSession.UserId && (role != user.Role || !isActive))
                throw new ValidationException("Bạn không thể tự đổi vai trò hoặc khóa tài khoản của chính mình.");

            user.FullName = fullName.Trim();
            user.Phone = phone.Trim();
            user.Email = email.Trim();
            user.IsActive = isActive;
            if (user.Role != role)
            {
                user.Role = role;
                // Giữ hồ sơ cũ (lịch sử), chỉ bổ sung hồ sơ còn thiếu cho vai trò mới.
                if (role == UserRole.Member && user.Member == null) user.Member = new Member();
                if (role == UserRole.Coach && user.Coach == null) user.Coach = new Coach();
            }
            await _db.SaveChangesAsync();
        }

        public async Task ResetPasswordAsync(int userId, string newPassword)
        {
            Authorization.Require(UserRole.Manager);
            var user = await _db.Users.FirstAsync(u => u.Id == userId);
            user.PasswordHash = PasswordHasher.Hash(newPassword);
            await _db.SaveChangesAsync();
        }

        /// <summary>Chỉ xóa được tài khoản chưa phát sinh dữ liệu; còn lại hãy khóa tài khoản.</summary>
        public async Task DeleteAsync(int userId)
        {
            Authorization.Require(UserRole.Manager);
            if (userId == AppSession.UserId) throw new ValidationException("Bạn không thể xóa tài khoản đang đăng nhập.");
            var user = await _db.Users.Include(u => u.Member).Include(u => u.Coach).FirstAsync(u => u.Id == userId);

            var hasHistory =
                (user.Member != null && (await _db.Subscriptions.AnyAsync(s => s.MemberId == user.Member.Id)
                                      || await _db.Enrollments.AnyAsync(e => e.MemberId == user.Member.Id)
                                      || await _db.Attendances.AnyAsync(a => a.MemberId == user.Member.Id)
                                      || await _db.Payments.AnyAsync(p => p.MemberId == user.Member.Id)
                                      || await _db.CheckIns.AnyAsync(c => c.MemberId == user.Member.Id)
                                      || await _db.WorkoutLogs.AnyAsync(w => w.MemberId == user.Member.Id)))
                || (user.Coach != null && (await _db.Classes.AnyAsync(c => c.CoachId == user.Coach.Id)
                                        || await _db.TrainingPlans.AnyAsync(t => t.CoachId == user.Coach.Id)
                                        || await _db.ProgressReviews.AnyAsync(r => r.CoachId == user.Coach.Id)))
                || await _db.Payments.AnyAsync(p => p.ReceivedByUserId == userId)
                || await _db.SupportRequests.AnyAsync(s => s.CreatedByUserId == userId);
            if (hasHistory)
                throw new ValidationException("Tài khoản đã có dữ liệu (gói, lớp, thanh toán, lịch sử tập...). Hãy khóa tài khoản thay vì xóa để giữ lịch sử.");

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }

    // =====================================================================
    // THÀNH VIÊN
    // =====================================================================
    public class MemberService
    {
        private readonly SportsCenterContext _db;
        internal MemberService(SportsCenterContext db) { _db = db; }

        /// <summary>Danh sách thành viên kèm tài khoản và các gói (để tính trạng thái/thời hạn).</summary>
        public Task<List<Member>> ListAsync()
        {
            Authorization.Require(UserRole.Manager, UserRole.Receptionist);
            return _db.Members.AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Subscriptions).ThenInclude(s => s.Plan)
                .OrderBy(m => m.User!.FullName).ToListAsync();
        }

        public Task<Member?> GetAsync(int memberId) =>
            _db.Members.AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Subscriptions).ThenInclude(s => s.Plan)
                .Include(m => m.Enrollments).ThenInclude(e => e.Class).ThenInclude(c => c!.Sport)
                .FirstOrDefaultAsync(m => m.Id == memberId);

        public Task<Member?> GetByUserIdAsync(int userId) =>
            _db.Members.AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Subscriptions).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(m => m.UserId == userId);

        /// <summary>Đăng ký thành viên mới tại quầy (Lễ tân / Quản lý).</summary>
        public async Task<Member> RegisterAsync(string username, string password, MemberInput input)
        {
            Authorization.Require(UserRole.Receptionist, UserRole.Manager);
            username = username.Trim();
            if (await _db.Users.AnyAsync(u => u.Username == username))
                throw new ValidationException("Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.", "Username");

            var member = new Member
            {
                User = new User { Username = username, PasswordHash = PasswordHasher.Hash(password), Role = UserRole.Member },
                JoinDate = DateTime.Today
            };
            Apply(member, input);
            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            return member;
        }

        /// <summary>Cập nhật hồ sơ: Quản lý/Lễ tân, HLV (mục tiêu, trình độ) hoặc chính thành viên đó.</summary>
        public async Task UpdateAsync(int memberId, MemberInput input)
        {
            var member = await _db.Members.Include(m => m.User).FirstAsync(m => m.Id == memberId);
            if (!Authorization.Is(UserRole.Manager, UserRole.Receptionist, UserRole.Coach) && AppSession.UserId != member.UserId && AppSession.IsSignedIn)
                throw new UnauthorizedAccessException("Bạn chỉ được cập nhật hồ sơ của chính mình.");
            Apply(member, input);
            await _db.SaveChangesAsync();
        }

        private static void Apply(Member m, MemberInput i)
        {
            m.User!.FullName = i.FullName.Trim();
            m.User.Phone = i.Phone.Trim();
            m.User.Email = i.Email.Trim();
            m.Gender = i.Gender;
            m.DateOfBirth = i.DateOfBirth;
            m.HeightCm = i.HeightCm;
            m.WeightKg = i.WeightKg;
            m.FitnessLevel = i.Level;
            m.Goals = i.Goals.Trim();
            m.HealthNotes = i.HealthNotes.Trim();
        }

        /// <summary>Gói đang hiệu lực hôm nay (gói kết thúc muộn nhất).</summary>
        public static Subscription? CurrentSubscription(Member m, DateTime? on = null) =>
            m.Subscriptions.Where(s => s.IsValidOn(on ?? DateTime.Today)).OrderByDescending(s => s.EndDate).FirstOrDefault();

        /// <summary>Ngày hết hạn xa nhất trong các gói chưa hủy (kể cả gói gia hạn trước).</summary>
        public static DateTime? MembershipEnd(Member m) =>
            m.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).Select(s => (DateTime?)s.EndDate).Max();
    }

    // =====================================================================
    // HUẤN LUYỆN VIÊN
    // =====================================================================
    public class CoachService
    {
        private readonly SportsCenterContext _db;
        internal CoachService(SportsCenterContext db) { _db = db; }

        public Task<List<Coach>> ListAsync() =>
            _db.Coaches.AsNoTracking().Include(c => c.User).Include(c => c.Classes)
                .OrderBy(c => c.User!.FullName).ToListAsync();

        public Task<Coach?> GetByUserIdAsync(int userId) =>
            _db.Coaches.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);

        public async Task<Coach> CreateAsync(string username, string password, CoachInput input)
        {
            Authorization.Require(UserRole.Manager);
            username = username.Trim();
            if (await _db.Users.AnyAsync(u => u.Username == username))
                throw new ValidationException("Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.", "Username");
            var coach = new Coach
            {
                User = new User { Username = username, PasswordHash = PasswordHasher.Hash(password), Role = UserRole.Coach },
                HireDate = DateTime.Today
            };
            Apply(coach, input);
            _db.Coaches.Add(coach);
            await _db.SaveChangesAsync();
            return coach;
        }

        public async Task UpdateAsync(int coachId, CoachInput input)
        {
            var coach = await _db.Coaches.Include(c => c.User).FirstAsync(c => c.Id == coachId);
            if (!Authorization.Is(UserRole.Manager) && AppSession.UserId != coach.UserId && AppSession.IsSignedIn)
                throw new UnauthorizedAccessException("Bạn chỉ được cập nhật hồ sơ của chính mình.");
            Apply(coach, input);
            await _db.SaveChangesAsync();
        }

        private static void Apply(Coach c, CoachInput i)
        {
            c.User!.FullName = i.FullName.Trim();
            c.User.Phone = i.Phone.Trim();
            c.User.Email = i.Email.Trim();
            c.Specialty = i.Specialty.Trim();
            c.Certifications = i.Certifications.Trim();
            c.Bio = i.Bio.Trim();
            c.YearsExperience = i.YearsExperience;
            c.IsAvailable = i.IsAvailable;
        }

        /// <summary>Học viên đang theo học các lớp do HLV phụ trách (kèm tên lớp).</summary>
        public async Task<List<(Member Member, List<string> Classes)>> MyMembersAsync(int coachId)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.Status == EnrollmentStatus.Active && e.Class!.CoachId == coachId)
                .Include(e => e.Class)
                .Include(e => e.Member).ThenInclude(m => m!.User)
                .Include(e => e.Member).ThenInclude(m => m!.Subscriptions)
                .ToListAsync();
            return enrollments.GroupBy(e => e.MemberId)
                .Select(g => (g.First().Member!, g.Select(e => e.Class!.Name).Distinct().ToList()))
                .OrderBy(x => x.Item1.User!.FullName).ToList();
        }
    }
}
