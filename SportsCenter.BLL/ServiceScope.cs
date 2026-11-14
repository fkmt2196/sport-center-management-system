using Microsoft.EntityFrameworkCore;
using SportsCenter.BLL.Services;
using SportsCenter.DAL.Data;

namespace SportsCenter.BLL
{
    /// <summary>
    /// Điểm vào duy nhất của tầng giao diện vào tầng nghiệp vụ: mỗi thao tác mở một phạm vi ngắn
    /// (một DbContext mới) và dùng các service trong phạm vi đó. DbContext là internal của DAL/BLL,
    /// nên giao diện không thể tự truy vấn CSDL.
    /// </summary>
    public sealed class ServiceScope : IDisposable
    {
        private readonly SportsCenterContext _db = DbConfig.CreateContext();

        private AuthService? _auth;
        public AuthService Auth => _auth ??= new AuthService(_db);
        public AccountService Accounts => new(_db);
        public MemberService Members => new(_db);
        public CoachService Coaches => new(_db);
        public FacilityService Facilities => new(_db);
        public ClassService Classes => new(_db);
        public AttendanceService Attendance => new(_db);
        public MembershipService Memberships => new(_db);
        public PaymentService Payments => new(_db);
        public ReportService Reports => new(_db);
        public TrainingService Training => new(_db);
        public NotificationService Notifications => new(_db);
        public SupportService Support => new(_db);
        public AuditService Audit => new(_db);
        public AiContextService AiContext => new(_db);

        /// <summary>Chạy một thao tác với scope mới.</summary>
        public static async Task<T> Run<T>(Func<ServiceScope, Task<T>> work)
        {
            using var scope = new ServiceScope();
            return await work(scope);
        }

        public static async Task Run(Func<ServiceScope, Task> work)
        {
            using var scope = new ServiceScope();
            await work(scope);
        }

        public void Dispose() => _db.Dispose();
    }

    /// <summary>Khởi tạo CSDL khi ứng dụng khởi động (áp migration, seed dữ liệu mẫu).</summary>
    public static class DatabaseInitializer
    {
        /// <summary>Áp migration mới nhất; nếu <paramref name="seedSampleData"/> thì thêm dữ liệu mẫu khi CSDL còn trống.</summary>
        public static void Initialize(bool seedSampleData)
        {
            using var context = DbConfig.CreateContext();
            context.Database.Migrate();
            if (seedSampleData) DbSeeder.Seed(context, PasswordHasher.Hash);
        }
    }
}
