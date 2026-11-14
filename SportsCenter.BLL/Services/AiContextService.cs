using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsCenter.BLL.AI;
using SportsCenter.DAL.Data;
using SportsCenter.DAL.Models;

namespace SportsCenter.BLL.Services
{
    /// <summary>Dựng ngữ cảnh cho trợ lý AI từ dữ liệu trung tâm (hồ sơ, lịch sử tập, gói, giờ mở cửa).</summary>
    public class AiContextService
    {
        public const string OpeningHours = "Giờ mở cửa: 05:30 – 22:00 tất cả các ngày trong tuần (hồ bơi: 06:00 – 20:00).";
        public const string Address = "Địa chỉ: 123 Nguyễn Văn Linh, Quận 7, TP. Hồ Chí Minh – Hotline: 0909 123 456.";

        private readonly SportsCenterContext _db;
        internal AiContextService(SportsCenterContext db) { _db = db; }

        public async Task<AiMemberProfile> BuildProfileAsync(int memberId)
        {
            var m = await _db.Members.AsNoTracking().Include(x => x.User).FirstAsync(x => x.Id == memberId);
            var since = DateTime.Today.AddDays(-30);
            var sessions = await _db.Attendances.CountAsync(a => a.MemberId == memberId
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) && a.Session!.StartTime >= since);
            var logs = await _db.WorkoutLogs.AsNoTracking().Where(w => w.MemberId == memberId).OrderByDescending(w => w.Date).Take(5)
                .Select(w => new { w.Date, w.Exercises, w.WeightKg }).ToListAsync();
            var classes = await _db.Enrollments.Where(e => e.MemberId == memberId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.Class!.Name).ToListAsync();

            int? age = m.DateOfBirth is DateTime dob ? DateTime.Today.Year - dob.Year - (DateTime.Today.DayOfYear < dob.DayOfYear ? 1 : 0) : null;
            return new AiMemberProfile(
                m.User!.FullName,
                m.Gender switch { Gender.Male => "Nam", Gender.Female => "Nữ", _ => "Khác" },
                age, m.HeightCm, m.WeightKg,
                m.FitnessLevel switch { FitnessLevel.Beginner => "Mới bắt đầu", FitnessLevel.Intermediate => "Trung bình", _ => "Nâng cao" },
                m.Goals, m.HealthNotes,
                logs.Select(l => $"{l.Date:dd/MM}: {l.Exercises}{(l.WeightKg.HasValue ? $" (cân nặng {l.WeightKg:0.#} kg)" : "")}").ToList(),
                sessions, classes);
        }

        /// <summary>Thông tin trung tâm: giờ mở cửa, gói đang bán, bộ môn.</summary>
        public async Task<string> CenterInfoAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine(OpeningHours);
            sb.AppendLine(Address);
            foreach (var p in await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.DurationDays).ToListAsync())
                sb.AppendLine($"{(p.Name.StartsWith("Gói") ? p.Name : "Gói " + p.Name)}: {p.Price:N0} ₫ / {p.DurationDays} ngày{(p.IncludesGroupClasses ? ", gồm các lớp nhóm miễn phí" : ", chỉ phòng gym")}.");
            var sports = await _db.Sports.Where(s => s.IsActive).Select(s => s.Name).ToListAsync();
            sb.AppendLine("Bộ môn: " + string.Join(", ", sports) + ".");
            return sb.ToString();
        }
    }
}
