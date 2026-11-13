using SportsCenter.Core.Models;
using SportsCenter.Core.Services;

namespace Project.Frontend.Data
{
    /// <summary>Dòng hiển thị cho các bảng (chuỗi đã định dạng sẵn + giá trị gốc để sắp xếp).</summary>
    public static class Labels
    {
        public static string Gender(Gender g) => g switch { SportsCenter.Core.Models.Gender.Male => "Nam", SportsCenter.Core.Models.Gender.Female => "Nữ", _ => "Khác" };
        public static string Level(FitnessLevel l) => l switch { FitnessLevel.Beginner => "Mới bắt đầu", FitnessLevel.Intermediate => "Trung bình", _ => "Nâng cao" };
        public static string Attendance(AttendanceStatus s) => Project.Frontend.Controls.StatusBadge.Describe("attendance", s.ToString()).Label;

        /// <summary>Trạng thái gói của thành viên: Active / Expiring (≤7 ngày) / Expired / None.</summary>
        public static (string State, DateTime? End, string PlanName) Membership(Member m)
        {
            var end = MemberService.MembershipEnd(m);
            var current = MemberService.CurrentSubscription(m);
            var latest = m.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).OrderByDescending(s => s.EndDate).FirstOrDefault();
            var plan = (current ?? latest)?.Plan?.Name ?? "";
            if (end == null) return ("None", null, plan);
            if (current == null) return end < DateTime.Today ? ("Expired", end, plan) : ("Upcoming", end, plan);
            return (end.Value.Date <= DateTime.Today.AddDays(7) ? "Expiring" : "Active", end, plan);
        }

        public static string Age(DateTime? dob) => dob is DateTime d ? $"{DateTime.Today.Year - d.Year - (DateTime.Today.DayOfYear < d.DayOfYear ? 1 : 0)} tuổi" : "";
    }

    public class MemberRow
    {
        public required Member Entity { get; init; }
        public int Id => Entity.Id;
        public string FullName => Entity.User!.FullName;
        public string Username => Entity.User!.Username;
        public string Phone => Entity.User!.Phone;
        public string Email => Entity.User!.Email;
        public string Contact => $"{Phone} · {Email}";
        public string Level => Entity.FitnessLevel.ToString();
        public string Goals => Entity.Goals;
        public DateTime JoinDate => Entity.JoinDate;
        public string JoinText => JoinDate.ToString("dd/MM/yyyy");
        public string MembershipState { get; init; } = "None";
        public DateTime? MembershipEnd { get; init; }
        public string EndText => MembershipEnd?.ToString("dd/MM/yyyy") ?? "—";
        public string PlanName { get; init; } = "";
        public string Classes { get; init; } = "";
        public bool Active => Entity.User!.IsActive;

        public static MemberRow From(Member m, string classes = "")
        {
            var (state, end, plan) = Labels.Membership(m);
            return new MemberRow { Entity = m, MembershipState = state, MembershipEnd = end, PlanName = plan, Classes = classes };
        }
    }

    public class SessionRow
    {
        public required ClassSession Entity { get; init; }
        public int Enrolled { get; init; }
        public int Present { get; init; }
        public int Id => Entity.Id;
        public DateTime Start => Entity.StartTime;
        public string DateText => $"{Formats.DayOfWeekVi(Entity.StartTime.DayOfWeek)}, {Entity.StartTime:dd/MM/yyyy}";
        public string TimeText => $"{Entity.StartTime:HH:mm} – {Entity.EndTime:HH:mm}";
        public string ClassName => Entity.Class?.Name ?? "";
        public string SportName => Entity.Class?.Sport?.Name ?? "";
        public string CoachName => Entity.Class?.Coach?.User?.FullName ?? "(chưa phân công)";
        public string CoachPhone => Entity.Class?.Coach?.User?.Phone ?? "";
        public string RoomName => Entity.Room?.Name ?? "";
        public string Note => Entity.Note;
        public string Status => Entity.Status == SessionStatus.Scheduled && Entity.StartTime.Date == DateTime.Today ? "Today" : Entity.Status.ToString();
        public string AttendanceText => Entity.Status == SessionStatus.Cancelled ? "—" : $"{Present}/{Enrolled}";
        public bool IsPastOrToday => Entity.StartTime.Date <= DateTime.Today;
        public bool CanEdit => Entity.Status != SessionStatus.Cancelled;
    }

    public class ClassRow
    {
        public required ClassInfo Info { get; init; }
        public TrainingClass Entity => Info.Class;
        public int Id => Entity.Id;
        public string Name => Entity.Name;
        public string Description => Entity.Description;
        public string SportName => Entity.Sport?.Name ?? "";
        public string CoachName => Entity.Coach?.User?.FullName ?? "(chưa phân công)";
        public string Level => Entity.Level.ToString();
        public decimal Fee => Entity.Fee;
        public string FeeText => Entity.Fee == 0 ? "Gồm trong gói" : Formats.Money(Entity.Fee);
        public string SeatsText => $"{Info.Enrolled}/{Entity.Capacity}";
        public double FillPercent => Entity.Capacity > 0 ? Math.Min(100, 100.0 * Info.Enrolled / Entity.Capacity) : 0;
        public DateTime StartDate => Entity.StartDate;
        public string PeriodText => Entity.EndDate.HasValue ? $"{Entity.StartDate:dd/MM/yyyy} – {Entity.EndDate:dd/MM/yyyy}" : $"Từ {Entity.StartDate:dd/MM/yyyy}";
        public string NextText => Info.NextSession is DateTime n ? $"{Formats.DayOfWeekVi(n.DayOfWeek)} {n:dd/MM HH:mm}" : "—";
        public string Status => Entity.Status.ToString();
        public bool IsFull => Info.Enrolled >= Entity.Capacity;
        /// <summary>Trạng thái đăng ký của thành viên đang xem (trang Lớp học của thành viên).</summary>
        public string MyStatus { get; init; } = "None";
        public int? MyEnrollmentId { get; init; }
    }

    public class SubscriptionRow
    {
        public required Subscription Entity { get; init; }
        public int Id => Entity.Id;
        public string MemberName => Entity.Member?.User?.FullName ?? "";
        public string Username => Entity.Member?.User?.Username ?? "";
        public string PlanName => Entity.Plan?.Name ?? "";
        public DateTime StartDate => Entity.StartDate;
        public string PeriodText => $"{Entity.StartDate:dd/MM/yyyy} – {Entity.EndDate:dd/MM/yyyy}";
        public decimal Price => Entity.PricePaid;
        public string PriceText => Formats.Money(Entity.PricePaid);
        public string State => Entity.Status == SubscriptionStatus.Cancelled ? "Cancelled"
            : Entity.EndDate.Date < DateTime.Today ? "Expired"
            : Entity.StartDate.Date > DateTime.Today ? "Upcoming"
            : Entity.EndDate.Date <= DateTime.Today.AddDays(7) ? "Expiring" : "Active";
        public string Note => Entity.Note;
    }

    public class PaymentRow
    {
        public required Payment Entity { get; init; }
        public int Id => Entity.Id;
        public string InvoiceNo => Entity.InvoiceNo;
        public DateTime PaidAt => Entity.PaidAt;
        public string PaidText => Entity.PaidAt.ToString("dd/MM/yyyy HH:mm");
        public string MemberName => Entity.Member?.User?.FullName ?? "";
        public string Username => Entity.Member?.User?.Username ?? "";
        public decimal Amount => Entity.Amount;
        public string AmountText => Formats.Money(Entity.Amount);
        public string Method => Entity.Method.ToString();
        public string Purpose => Entity.Purpose.ToString();
        public string Note => Entity.Note;
        public string Cashier => Entity.ReceivedBy?.FullName ?? "Online";
    }

    public class NotificationRow
    {
        public required Notification Entity { get; init; }
        public int Id => Entity.Id;
        public string Title => Entity.Title;
        public string Body => Entity.Body;
        public string Type => Entity.Type.ToString();
        public DateTime CreatedAt => Entity.CreatedAt;
        public string TimeText => Entity.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        public string Sender => Entity.Sender?.FullName ?? "Hệ thống";
        public string ReadState => Entity.IsRead ? "Read" : "Unread";
    }
}
