using SportsCenter.Core.Data;
using SportsCenter.Core.Services;

namespace Project.Frontend.Data
{
    /// <summary>
    /// Phạm vi làm việc ngắn cho một thao tác giao diện: một DbContext mới + các service nghiệp vụ dùng chung (SportsCenter.Core).
    /// Giao diện chỉ gọi service, không tự viết truy vấn CSDL.
    /// </summary>
    public sealed class UiScope : IDisposable
    {
        public SportsCenterContext Db { get; } = DbConfig.CreateContext();

        private AuthService? _auth;
        public AuthService Auth => _auth ??= new AuthService(Db);
        public AccountService Accounts => new(Db);
        public MemberService Members => new(Db);
        public CoachService Coaches => new(Db);
        public FacilityService Facilities => new(Db);
        public ClassService Classes => new(Db);
        public AttendanceService Attendance => new(Db);
        public MembershipService Memberships => new(Db);
        public PaymentService Payments => new(Db);
        public ReportService Reports => new(Db);
        public TrainingService Training => new(Db);
        public NotificationService Notifications => new(Db);
        public SupportService Support => new(Db);
        public AuditService Audit => new(Db);
        public AiContextService AiContext => new(Db);

        /// <summary>Chạy một thao tác với scope mới.</summary>
        public static async Task<T> Run<T>(Func<UiScope, Task<T>> work)
        {
            using var scope = new UiScope();
            return await work(scope);
        }

        public static async Task Run(Func<UiScope, Task> work)
        {
            using var scope = new UiScope();
            await work(scope);
        }

        public void Dispose() => Db.Dispose();
    }

    public static class Formats
    {
        public static string Money(decimal value) => value.ToString("N0") + " ₫";

        /// <summary>312.500.000 -> "312,5 tr ₫"; 1.250.000.000 -> "1,25 tỷ ₫".</summary>
        public static string MoneyShort(decimal value) =>
            value >= 1_000_000_000 ? (value / 1_000_000_000m).ToString("0.##") + " tỷ ₫"
            : value >= 1_000_000 ? (value / 1_000_000m).ToString("0.#") + " tr ₫"
            : Money(value);

        public static string DayOfWeekVi(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            _ => "Chủ nhật"
        };

        public static string Time(TimeSpan t) => t.ToString(@"hh\:mm");
        public static string Time(DateTime t) => t.ToString("HH:mm");
    }

    public static class Text
    {
        /// <summary>Bỏ dấu + chữ thường: "Nguyễn Đức" -> "nguyen duc" (tìm kiếm không phân biệt dấu).</summary>
        public static string Fold(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var normalized = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (var ch in normalized)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
