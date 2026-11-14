using Microsoft.EntityFrameworkCore;
using SportsCenter.DAL.Data;
using SportsCenter.DAL.Models;

namespace SportsCenter.BLL.Services
{
    // =====================================================================
    // GÓI THÀNH VIÊN
    // =====================================================================
    public class MembershipService
    {
        private readonly SportsCenterContext _db;
        internal MembershipService(SportsCenterContext db) { _db = db; }

        public Task<List<MembershipPlan>> PlansAsync(bool activeOnly = false) =>
            _db.Plans.AsNoTracking().Where(p => !activeOnly || p.IsActive).OrderBy(p => p.DurationDays).ThenBy(p => p.Price).ToListAsync();

        public async Task SavePlanAsync(MembershipPlan input)
        {
            Authorization.Require(UserRole.Manager);
            if (input.DurationDays <= 0) throw new ValidationException("Thời hạn phải lớn hơn 0 ngày.", "DurationDays");
            var plan = input.Id == 0 ? _db.Plans.Add(new MembershipPlan()).Entity : await _db.Plans.FirstAsync(p => p.Id == input.Id);
            plan.Name = input.Name.Trim();
            plan.Description = input.Description.Trim();
            plan.Price = input.Price;
            plan.DurationDays = input.DurationDays;
            plan.IncludesGroupClasses = input.IncludesGroupClasses;
            plan.IsActive = input.IsActive;
            await _db.SaveChangesAsync();
        }

        public async Task DeletePlanAsync(int planId)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Subscriptions.AnyAsync(s => s.PlanId == planId))
                throw new ValidationException("Gói đã có người đăng ký. Hãy ngừng bán gói thay vì xóa.");
            _db.Plans.Remove(await _db.Plans.FirstAsync(p => p.Id == planId));
            await _db.SaveChangesAsync();
        }

        public Task<List<Subscription>> SubscriptionsAsync(int? memberId = null) =>
            _db.Subscriptions.AsNoTracking().Include(s => s.Plan).Include(s => s.Member).ThenInclude(m => m!.User)
                .Where(s => memberId == null || s.MemberId == memberId)
                .OrderByDescending(s => s.StartDate).ToListAsync();

        /// <summary>
        /// Đăng ký hoặc gia hạn gói. Gia hạn nối tiếp ngay sau ngày hết hạn của gói hiện tại.
        /// Tạo phiếu thu tương ứng. Thành viên tự đăng ký (thanh toán online) hoặc Lễ tân/Quản lý tại quầy.
        /// </summary>
        public async Task<(Subscription Subscription, Payment Payment)> SubscribeAsync(int memberId, int planId, PaymentMethod method, string note = "")
        {
            Authorization.Require(UserRole.Member, UserRole.Receptionist, UserRole.Manager);
            var member = await _db.Members.Include(m => m.Subscriptions).FirstAsync(m => m.Id == memberId);
            if (Authorization.Is(UserRole.Member) && member.UserId != AppSession.UserId)
                throw new UnauthorizedAccessException("Bạn chỉ được đăng ký gói cho chính mình.");
            var plan = await _db.Plans.FirstAsync(p => p.Id == planId);
            if (!plan.IsActive) throw new ValidationException("Gói này đã ngừng bán.");

            var lastEnd = MemberService.MembershipEnd(member);
            var start = lastEnd.HasValue && lastEnd.Value >= DateTime.Today ? lastEnd.Value.Date.AddDays(1) : DateTime.Today;
            var sub = new Subscription
            {
                MemberId = memberId, PlanId = planId, StartDate = start, EndDate = start.AddDays(plan.DurationDays - 1),
                PricePaid = plan.Price, Note = note.Trim()
            };
            _db.Subscriptions.Add(sub);
            await _db.SaveChangesAsync();

            var renew = lastEnd.HasValue && lastEnd.Value >= DateTime.Today;
            var payment = new Payment
            {
                InvoiceNo = await PaymentService.NextInvoiceNoAsync(_db), MemberId = memberId, Amount = plan.Price, Method = method,
                Purpose = PaymentPurpose.Subscription, SubscriptionId = sub.Id,
                ReceivedByUserId = Authorization.Is(UserRole.Member) ? null : AppSession.UserId,
                Note = $"{(renew ? "Gia hạn" : "Đăng ký")} {plan.Name} ({sub.StartDate:dd/MM/yyyy} – {sub.EndDate:dd/MM/yyyy})"
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
            return (sub, payment);
        }

        public async Task CancelSubscriptionAsync(int subscriptionId, string reason)
        {
            Authorization.Require(UserRole.Manager, UserRole.Receptionist);
            var s = await _db.Subscriptions.FirstAsync(x => x.Id == subscriptionId);
            s.Status = SubscriptionStatus.Cancelled;
            s.Note = string.IsNullOrWhiteSpace(reason) ? s.Note : $"Hủy: {reason.Trim()}";
            await _db.SaveChangesAsync();
        }
    }

    // =====================================================================
    // THANH TOÁN & HÓA ĐƠN
    // =====================================================================
    public class PaymentService
    {
        private readonly SportsCenterContext _db;
        internal PaymentService(SportsCenterContext db) { _db = db; }

        /// <summary>Số hóa đơn dạng HD261114-0001 (đánh số lại mỗi ngày).</summary>
        internal static async Task<string> NextInvoiceNoAsync(SportsCenterContext db, DateTime? at = null)
        {
            var prefix = $"HD{(at ?? DateTime.Now):yyMMdd}-";
            var last = await db.Payments.Where(p => p.InvoiceNo.StartsWith(prefix)).OrderByDescending(p => p.InvoiceNo).Select(p => p.InvoiceNo).FirstOrDefaultAsync();
            var local = db.Payments.Local.Where(p => p.InvoiceNo.StartsWith(prefix)).Select(p => p.InvoiceNo).DefaultIfEmpty("").Max();
            if (string.CompareOrdinal(local, last) > 0) last = local;
            var next = last == null || last.Length == 0 ? 1 : int.Parse(last[prefix.Length..]) + 1;
            return prefix + next.ToString("D4");
        }

        public async Task<List<Payment>> ListAsync(DateTime? from = null, DateTime? to = null, int? memberId = null)
        {
            if (memberId == null) Authorization.Require(UserRole.Manager, UserRole.Receptionist);
            else await EnsureOwnAsync(memberId.Value);
            return await _db.Payments.AsNoTracking().Include(p => p.Member).ThenInclude(m => m!.User).Include(p => p.ReceivedBy)
                .Where(p => (from == null || p.PaidAt >= from) && (to == null || p.PaidAt < to) && (memberId == null || p.MemberId == memberId))
                .OrderByDescending(p => p.PaidAt).ToListAsync();
        }

        /// <summary>Ghi nhận khoản thu khác tại quầy (phí dịch vụ, PT, đồ uống...).</summary>
        public async Task<Payment> RecordAsync(int memberId, decimal amount, PaymentMethod method, string note)
        {
            Authorization.Require(UserRole.Receptionist, UserRole.Manager);
            if (amount <= 0) throw new ValidationException("Số tiền phải lớn hơn 0.", "Amount");
            var p = new Payment
            {
                InvoiceNo = await NextInvoiceNoAsync(_db), MemberId = memberId, Amount = amount, Method = method,
                Purpose = PaymentPurpose.Other, ReceivedByUserId = AppSession.UserId, Note = note.Trim()
            };
            _db.Payments.Add(p);
            await _db.SaveChangesAsync();
            return p;
        }

        public async Task<Payment> GetAsync(int id)
        {
            var p = await _db.Payments.AsNoTracking().Include(x => x.Member).ThenInclude(m => m!.User).Include(x => x.ReceivedBy).FirstAsync(x => x.Id == id);
            await EnsureOwnAsync(p.MemberId);
            return p;
        }

        /// <summary>Thành viên chỉ xem được hóa đơn của chính mình; nhân viên xem được tất cả.</summary>
        private async Task EnsureOwnAsync(int memberId)
        {
            if (!Authorization.Is(UserRole.Member)) return;
            if (!await _db.Members.AnyAsync(m => m.Id == memberId && m.UserId == AppSession.UserId))
                throw new UnauthorizedAccessException("Bạn chỉ được xem hóa đơn của chính mình.");
        }

        public static string MethodName(PaymentMethod m) => m switch
        {
            PaymentMethod.Cash => "Tiền mặt",
            PaymentMethod.Card => "Thẻ ngân hàng",
            PaymentMethod.Transfer => "Chuyển khoản",
            _ => "Thanh toán online"
        };

        public static string PurposeName(PaymentPurpose p) => p switch
        {
            PaymentPurpose.Subscription => "Gói thành viên",
            PaymentPurpose.ClassFee => "Học phí lớp",
            _ => "Khác"
        };

        /// <summary>Hóa đơn dạng HTML (in hoặc lưu file).</summary>
        public static string InvoiceHtml(Payment p, string centerName, string centerAddress)
        {
            string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            return $$"""
<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><title>Hóa đơn {{E(p.InvoiceNo)}}</title>
<style>
 body{font-family:"Segoe UI",Arial,sans-serif;color:#0f172a;max-width:640px;margin:32px auto;padding:0 16px}
 h1{color:#2563eb;margin:0}.muted{color:#64748b;font-size:13px}
 table{width:100%;border-collapse:collapse;margin-top:20px} td,th{padding:10px;border-bottom:1px solid #e2e8f0;text-align:left}
 .total{font-size:20px;font-weight:700;text-align:right;margin-top:16px}.head{display:flex;justify-content:space-between;align-items:flex-start}
</style></head><body>
<div class="head"><div><h1>{{E(centerName)}}</h1><div class="muted">{{E(centerAddress)}}</div></div>
<div style="text-align:right"><b>HÓA ĐƠN THANH TOÁN</b><div class="muted">Số: {{E(p.InvoiceNo)}}<br>Ngày: {{p.PaidAt:dd/MM/yyyy HH:mm}}</div></div></div>
<table>
<tr><th>Khách hàng</th><td>{{E(p.Member?.User?.FullName)}} ({{E(p.Member?.User?.Username)}})</td></tr>
<tr><th>Điện thoại</th><td>{{E(p.Member?.User?.Phone)}}</td></tr>
<tr><th>Nội dung</th><td>{{E(PurposeName(p.Purpose))}} – {{E(p.Note)}}</td></tr>
<tr><th>Hình thức</th><td>{{E(MethodName(p.Method))}}</td></tr>
<tr><th>Thu ngân</th><td>{{E(p.ReceivedBy?.FullName ?? "Thanh toán online")}}</td></tr>
</table>
<div class="total">Tổng cộng: {{p.Amount:N0}} ₫</div>
<p class="muted">Cảm ơn quý khách đã sử dụng dịch vụ!</p>
</body></html>
""";
        }
    }

    // =====================================================================
    // BÁO CÁO (Quản lý)
    // =====================================================================
    public record DashboardStats(int TotalMembers, int ActiveMembers, int NewMembersThisMonth, int OpenClasses, int Coaches,
        decimal RevenueThisMonth, decimal RevenueLastMonth, int CheckInsToday, int ExpiringSoon, int SessionsToday);

    public record MonthValue(DateTime Month, decimal Value, decimal Secondary = 0);

    public record ClassFill(string ClassName, string Sport, string Coach, int Enrolled, int Capacity, ClassStatus Status);

    public class ReportService
    {
        private readonly SportsCenterContext _db;
        internal ReportService(SportsCenterContext db) { _db = db; }

        public async Task<DashboardStats> DashboardAsync()
        {
            Authorization.Require(UserRole.Manager);
            var today = DateTime.Today;
            var month = new DateTime(today.Year, today.Month, 1);
            var prev = month.AddMonths(-1);
            return new DashboardStats(
                TotalMembers: await _db.Members.CountAsync(),
                ActiveMembers: await _db.Members.CountAsync(m => m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.StartDate <= today && s.EndDate >= today)),
                NewMembersThisMonth: await _db.Members.CountAsync(m => m.JoinDate >= month),
                OpenClasses: await _db.Classes.CountAsync(c => c.Status == ClassStatus.Open),
                Coaches: await _db.Coaches.CountAsync(c => c.User!.IsActive),
                RevenueThisMonth: await _db.Payments.Where(p => p.PaidAt >= month).SumAsync(p => (decimal?)p.Amount) ?? 0,
                RevenueLastMonth: await _db.Payments.Where(p => p.PaidAt >= prev && p.PaidAt < month).SumAsync(p => (decimal?)p.Amount) ?? 0,
                CheckInsToday: await _db.CheckIns.CountAsync(c => c.CheckInTime >= today),
                ExpiringSoon: await _db.Members.CountAsync(m => m.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).Max(s => (DateTime?)s.EndDate) >= today
                                                              && m.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).Max(s => (DateTime?)s.EndDate) <= today.AddDays(7)),
                SessionsToday: await _db.Sessions.CountAsync(s => s.StartTime >= today && s.StartTime < today.AddDays(1) && s.Status != SessionStatus.Cancelled));
        }

        /// <summary>Doanh thu theo tháng: Value = gói thành viên, Secondary = học phí lớp + khác.</summary>
        public async Task<List<MonthValue>> RevenueByMonthAsync(int months)
        {
            Authorization.Require(UserRole.Manager);
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
            var rows = await _db.Payments.Where(p => p.PaidAt >= start)
                .GroupBy(p => new { p.PaidAt.Year, p.PaidAt.Month, Sub = p.Purpose == PaymentPurpose.Subscription })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Sub, Total = g.Sum(p => p.Amount) }).ToListAsync();
            return Enumerable.Range(0, months).Select(i => start.AddMonths(i)).Select(m => new MonthValue(m,
                rows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Sub).Sum(r => r.Total),
                rows.Where(r => r.Year == m.Year && r.Month == m.Month && !r.Sub).Sum(r => r.Total))).ToList();
        }

        /// <summary>Thành viên mới (Value) và lượt đăng ký lớp mới (Secondary) theo tháng.</summary>
        public async Task<List<MonthValue>> MembersByMonthAsync(int months)
        {
            Authorization.Require(UserRole.Manager);
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
            var joins = await _db.Members.Where(m => m.JoinDate >= start).GroupBy(m => new { m.JoinDate.Year, m.JoinDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync();
            var enrolls = await _db.Enrollments.Where(e => e.EnrolledAt >= start).GroupBy(e => new { e.EnrolledAt.Year, e.EnrolledAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync();
            return Enumerable.Range(0, months).Select(i => start.AddMonths(i)).Select(m => new MonthValue(m,
                joins.Where(r => r.Year == m.Year && r.Month == m.Month).Sum(r => r.N),
                enrolls.Where(r => r.Year == m.Year && r.Month == m.Month).Sum(r => r.N))).ToList();
        }

        /// <summary>Tình trạng đăng ký từng lớp.</summary>
        public async Task<List<ClassFill>> ClassFillAsync()
        {
            Authorization.Require(UserRole.Manager);
            return await _db.Classes.AsNoTracking().OrderBy(c => c.Name).Select(c => new ClassFill(
                c.Name, c.Sport!.Name, c.Coach != null ? c.Coach.User!.FullName : "(chưa phân công)",
                c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active), c.Capacity, c.Status)).ToListAsync();
        }
    }

    // =====================================================================
    // TẬP LUYỆN: KẾ HOẠCH, KẾT QUẢ, ĐÁNH GIÁ
    // =====================================================================
    public class TrainingService
    {
        private readonly SportsCenterContext _db;
        internal TrainingService(SportsCenterContext db) { _db = db; }

        private IQueryable<TrainingPlan> PlanQuery() => _db.TrainingPlans.AsNoTracking()
            .Include(p => p.Coach).ThenInclude(c => c!.User)
            .Include(p => p.Member).ThenInclude(m => m!.User)
            .Include(p => p.Class);

        public Task<List<TrainingPlan>> PlansOfCoachAsync(int coachId) =>
            PlanQuery().Where(p => p.CoachId == coachId).OrderByDescending(p => p.CreatedAt).ToListAsync();

        /// <summary>Kế hoạch cá nhân của thành viên + kế hoạch chung của các lớp đang học.</summary>
        public Task<List<TrainingPlan>> PlansOfMemberAsync(int memberId) =>
            PlanQuery().Where(p => p.MemberId == memberId
                                   || (p.ClassId != null && _db.Enrollments.Any(e => e.ClassId == p.ClassId && e.MemberId == memberId && e.Status == EnrollmentStatus.Active)))
                .OrderByDescending(p => p.StartDate).ToListAsync();

        public async Task SavePlanAsync(TrainingPlan input)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            if (input.MemberId == null && input.ClassId == null)
                throw new ValidationException("Chọn thành viên hoặc lớp áp dụng kế hoạch.", "Target");
            var plan = input.Id == 0 ? _db.TrainingPlans.Add(new TrainingPlan { CoachId = input.CoachId }).Entity
                                     : await _db.TrainingPlans.Include(p => p.Coach).FirstAsync(p => p.Id == input.Id);
            if (input.Id != 0 && Authorization.Is(UserRole.Coach) && plan.Coach!.UserId != AppSession.UserId)
                throw new UnauthorizedAccessException("Bạn chỉ được sửa kế hoạch do mình tạo.");
            plan.MemberId = input.MemberId;
            plan.ClassId = input.ClassId;
            plan.Title = input.Title.Trim();
            plan.Goal = input.Goal.Trim();
            plan.Content = input.Content.Trim();
            plan.StartDate = input.StartDate.Date;
            plan.EndDate = input.EndDate?.Date;
            await _db.SaveChangesAsync();
        }

        public async Task DeletePlanAsync(int id)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            _db.TrainingPlans.Remove(await _db.TrainingPlans.FirstAsync(p => p.Id == id));
            await _db.SaveChangesAsync();
        }

        public Task<List<WorkoutLog>> LogsAsync(int? memberId = null, int? coachId = null) =>
            _db.WorkoutLogs.AsNoTracking()
                .Include(w => w.Member).ThenInclude(m => m!.User)
                .Include(w => w.Coach).ThenInclude(c => c!.User)
                .Include(w => w.Session).ThenInclude(s => s!.Class)
                .Where(w => (memberId == null || w.MemberId == memberId) && (coachId == null || w.CoachId == coachId))
                .OrderByDescending(w => w.Date).ToListAsync();

        public async Task SaveLogAsync(WorkoutLog input)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            var log = input.Id == 0 ? _db.WorkoutLogs.Add(new WorkoutLog { CoachId = input.CoachId }).Entity
                                    : await _db.WorkoutLogs.FirstAsync(w => w.Id == input.Id);
            log.MemberId = input.MemberId;
            log.SessionId = input.SessionId;
            log.Date = input.Date.Date;
            log.Exercises = input.Exercises.Trim();
            log.DurationMinutes = input.DurationMinutes;
            log.WeightKg = input.WeightKg;
            log.Notes = input.Notes.Trim();
            await _db.SaveChangesAsync();
        }

        public async Task DeleteLogAsync(int id)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            _db.WorkoutLogs.Remove(await _db.WorkoutLogs.FirstAsync(w => w.Id == id));
            await _db.SaveChangesAsync();
        }

        public Task<List<ProgressReview>> ReviewsAsync(int? memberId = null, int? coachId = null) =>
            _db.ProgressReviews.AsNoTracking()
                .Include(r => r.Member).ThenInclude(m => m!.User)
                .Include(r => r.Coach).ThenInclude(c => c!.User)
                .Where(r => (memberId == null || r.MemberId == memberId) && (coachId == null || r.CoachId == coachId))
                .OrderByDescending(r => r.Date).ToListAsync();

        public async Task SaveReviewAsync(ProgressReview input)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            if (input.Score is < 1 or > 10) throw new ValidationException("Điểm tiến độ từ 1 đến 10.", "Score");
            var r = input.Id == 0 ? _db.ProgressReviews.Add(new ProgressReview { CoachId = input.CoachId }).Entity
                                  : await _db.ProgressReviews.FirstAsync(x => x.Id == input.Id);
            r.MemberId = input.MemberId;
            r.Date = input.Date.Date;
            r.Score = input.Score;
            r.Comment = input.Comment.Trim();
            if (input.Id == 0)
            {
                var userId = await _db.Members.Where(m => m.Id == input.MemberId).Select(m => m.UserId).FirstAsync();
                new NotificationService(_db).Queue(new[] { userId }, "Nhận xét mới từ huấn luyện viên",
                    $"Điểm tiến độ: {input.Score}/10. {input.Comment}", NotificationType.General);
            }
            await _db.SaveChangesAsync();
        }

        public async Task DeleteReviewAsync(int id)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            _db.ProgressReviews.Remove(await _db.ProgressReviews.FirstAsync(r => r.Id == id));
            await _db.SaveChangesAsync();
        }
    }

    // =====================================================================
    // YÊU CẦU HỖ TRỢ & AUDIT LOG
    // =====================================================================
    public class SupportService
    {
        private readonly SportsCenterContext _db;
        internal SupportService(SportsCenterContext db) { _db = db; }

        public Task<List<SupportRequest>> ListAsync()
        {
            Authorization.Require(UserRole.Receptionist, UserRole.Manager);
            return _db.SupportRequests.AsNoTracking().Include(s => s.Member).ThenInclude(m => m!.User).Include(s => s.CreatedBy)
                .OrderBy(s => s.Status).ThenByDescending(s => s.CreatedAt).ToListAsync();
        }

        public async Task SaveAsync(SupportRequest input)
        {
            Authorization.Require(UserRole.Receptionist, UserRole.Manager);
            var s = input.Id == 0 ? _db.SupportRequests.Add(new SupportRequest { CreatedByUserId = AppSession.UserId ?? input.CreatedByUserId }).Entity
                                  : await _db.SupportRequests.FirstAsync(x => x.Id == input.Id);
            s.MemberId = input.MemberId;
            s.Subject = input.Subject.Trim();
            s.Description = input.Description.Trim();
            s.Response = input.Response.Trim();
            if (s.Status != SupportStatus.Resolved && input.Status == SupportStatus.Resolved) s.ResolvedAt = DateTime.Now;
            if (input.Status != SupportStatus.Resolved) s.ResolvedAt = null;
            s.Status = input.Status;
            await _db.SaveChangesAsync();
        }
    }

    public class AuditService
    {
        private readonly SportsCenterContext _db;
        internal AuditService(SportsCenterContext db) { _db = db; }

        public Task<List<AuditLog>> ListAsync(DateTime from, DateTime to)
        {
            Authorization.Require(UserRole.Manager);
            return _db.AuditLogs.AsNoTracking().Where(a => a.Timestamp >= from && a.Timestamp < to)
                .OrderByDescending(a => a.Timestamp).Take(5000).ToListAsync();
        }
    }
}
