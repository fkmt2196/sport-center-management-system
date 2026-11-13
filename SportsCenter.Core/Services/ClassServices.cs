using Microsoft.EntityFrameworkCore;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Services
{
    // =====================================================================
    // BỘ MÔN & PHÒNG TẬP (Quản lý)
    // =====================================================================
    public class FacilityService
    {
        private readonly SportsCenterContext _db;
        public FacilityService(SportsCenterContext db) { _db = db; }

        public Task<List<Sport>> SportsAsync() =>
            _db.Sports.AsNoTracking().Include(s => s.Classes).OrderBy(s => s.Name).ToListAsync();

        public Task<List<Room>> RoomsAsync() =>
            _db.Rooms.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

        public async Task SaveSportAsync(Sport input)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Sports.AnyAsync(s => s.Name == input.Name.Trim() && s.Id != input.Id))
                throw new ValidationException("Bộ môn này đã tồn tại.", "Name");
            var sport = input.Id == 0 ? _db.Sports.Add(new Sport()).Entity : await _db.Sports.FirstAsync(s => s.Id == input.Id);
            sport.Name = input.Name.Trim();
            sport.Description = input.Description.Trim();
            sport.IsActive = input.IsActive;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteSportAsync(int id)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Classes.AnyAsync(c => c.SportId == id))
                throw new ValidationException("Bộ môn đang có lớp học. Hãy ngừng hoạt động bộ môn thay vì xóa.");
            _db.Sports.Remove(await _db.Sports.FirstAsync(s => s.Id == id));
            await _db.SaveChangesAsync();
        }

        public async Task SaveRoomAsync(Room input)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Rooms.AnyAsync(r => r.Name == input.Name.Trim() && r.Id != input.Id))
                throw new ValidationException("Tên phòng đã tồn tại.", "Name");
            var room = input.Id == 0 ? _db.Rooms.Add(new Room()).Entity : await _db.Rooms.FirstAsync(r => r.Id == input.Id);
            room.Name = input.Name.Trim();
            room.Capacity = input.Capacity;
            room.Location = input.Location.Trim();
            room.Equipment = input.Equipment.Trim();
            room.IsActive = input.IsActive;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteRoomAsync(int id)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Sessions.AnyAsync(s => s.RoomId == id))
                throw new ValidationException("Phòng đã được xếp lịch. Hãy ngừng sử dụng phòng thay vì xóa.");
            _db.Rooms.Remove(await _db.Rooms.FirstAsync(r => r.Id == id));
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Lớp kèm số liệu tổng hợp để hiển thị.</summary>
    public record ClassInfo(TrainingClass Class, int Enrolled, int SessionCount, DateTime? NextSession);

    /// <summary>Ca học kèm sĩ số đăng ký của lớp và số người đã điểm danh có mặt.</summary>
    public record SessionInfo(ClassSession Session, int Enrolled, int Present);

    // =====================================================================
    // LỚP, CA HỌC, ĐĂNG KÝ LỚP
    // =====================================================================
    public class ClassService
    {
        private readonly SportsCenterContext _db;
        private readonly NotificationService _notify;
        public ClassService(SportsCenterContext db) { _db = db; _notify = new NotificationService(db); }

        // ---------------- Lớp ----------------

        public async Task<List<ClassInfo>> ListAsync(int? coachId = null)
        {
            var now = DateTime.Now;
            var q = _db.Classes.AsNoTracking().Include(c => c.Sport).Include(c => c.Coach).ThenInclude(c => c!.User).AsQueryable();
            if (coachId.HasValue) q = q.Where(c => c.CoachId == coachId);
            var rows = await q.Select(c => new
            {
                Class = c,
                Enrolled = c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active),
                Sessions = c.Sessions.Count(s => s.Status != SessionStatus.Cancelled),
                Next = c.Sessions.Where(s => s.Status == SessionStatus.Scheduled && s.StartTime >= now).Min(s => (DateTime?)s.StartTime)
            }).ToListAsync();
            return rows.Select(r => new ClassInfo(r.Class, r.Enrolled, r.Sessions, r.Next)).OrderBy(r => r.Class.Name).ToList();
        }

        public async Task<TrainingClass> SaveAsync(TrainingClass input)
        {
            Authorization.Require(UserRole.Manager);
            if (input.EndDate.HasValue && input.EndDate.Value.Date < input.StartDate.Date)
                throw new ValidationException("Ngày kết thúc phải sau ngày bắt đầu.", "EndDate");

            TrainingClass cls;
            if (input.Id == 0) cls = _db.Classes.Add(new TrainingClass()).Entity;
            else
            {
                cls = await _db.Classes.FirstAsync(c => c.Id == input.Id);
                var enrolled = await _db.Enrollments.CountAsync(e => e.ClassId == cls.Id && e.Status == EnrollmentStatus.Active);
                if (input.Capacity < enrolled)
                    throw new ValidationException($"Sĩ số tối đa không được nhỏ hơn số thành viên đã đăng ký ({enrolled}).", "Capacity");
            }
            var coachChanged = cls.CoachId != input.CoachId;
            cls.Name = input.Name.Trim();
            cls.SportId = input.SportId;
            cls.CoachId = input.CoachId;
            cls.Level = input.Level;
            cls.Description = input.Description.Trim();
            cls.Capacity = input.Capacity;
            cls.Fee = input.Fee;
            cls.StartDate = input.StartDate.Date;
            cls.EndDate = input.EndDate?.Date;
            cls.Status = input.Status;
            if (coachChanged && input.CoachId is int coachId)
            {
                var coachUser = await _db.Coaches.Where(c => c.Id == coachId).Select(c => c.UserId).FirstAsync();
                _notify.Queue(new[] { coachUser }, $"Phân công lớp: {cls.Name}", $"Bạn được phân công phụ trách lớp {cls.Name}.", NotificationType.General);
            }
            await _db.SaveChangesAsync();
            return cls;
        }

        /// <summary>Xóa lớp chưa có dữ liệu điểm danh (xóa luôn ca học và đăng ký); ngược lại phải hủy/kết thúc lớp.</summary>
        public async Task DeleteAsync(int classId)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Attendances.AnyAsync(a => a.Session!.ClassId == classId)
                || await _db.Payments.AnyAsync(p => p.Enrollment!.ClassId == classId)
                || await _db.TrainingPlans.AnyAsync(t => t.ClassId == classId))
                throw new ValidationException("Lớp đã có dữ liệu điểm danh/thanh toán/kế hoạch tập. Hãy chuyển trạng thái sang Đã hủy hoặc Đã kết thúc.");
            _db.Classes.Remove(await _db.Classes.FirstAsync(c => c.Id == classId));
            await _db.SaveChangesAsync();
        }

        // ---------------- Ca học / lịch hoạt động ----------------

        public async Task<List<SessionInfo>> SessionsAsync(DateTime from, DateTime to, int? classId = null, int? coachId = null, int? memberId = null)
        {
            var q = _db.Sessions.AsNoTracking()
                .Include(s => s.Room)
                .Include(s => s.Class).ThenInclude(c => c!.Sport)
                .Include(s => s.Class).ThenInclude(c => c!.Coach).ThenInclude(c => c!.User)
                .Where(s => s.StartTime >= from && s.StartTime < to);
            if (classId.HasValue) q = q.Where(s => s.ClassId == classId);
            if (coachId.HasValue) q = q.Where(s => s.Class!.CoachId == coachId);
            if (memberId.HasValue) q = q.Where(s => s.Class!.Enrollments.Any(e => e.MemberId == memberId && e.Status == EnrollmentStatus.Active));
            var rows = await q.Select(s => new
            {
                Session = s,
                Enrolled = s.Class!.Enrollments.Count(e => e.Status == EnrollmentStatus.Active),
                Present = s.Attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
            }).ToListAsync();
            return rows.Select(r => new SessionInfo(r.Session, r.Enrolled, r.Present)).OrderBy(r => r.Session.StartTime).ToList();
        }

        private async Task<ClassSession?> FindRoomConflictAsync(int roomId, DateTime start, DateTime end, int ignoreId = 0) =>
            await _db.Sessions.Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id != ignoreId && s.RoomId == roomId && s.Status != SessionStatus.Cancelled
                                          && s.StartTime < end && start < s.EndTime);

        /// <summary>Tạo hàng loạt ca học theo các thứ trong tuần. Bỏ qua ngày bị trùng phòng. Trả về (số ca tạo, số ca bỏ qua).</summary>
        public async Task<(int Created, int Skipped)> GenerateSessionsAsync(int classId, int roomId, IReadOnlyCollection<DayOfWeek> days,
            TimeSpan start, TimeSpan end, DateTime from, DateTime to)
        {
            Authorization.Require(UserRole.Manager);
            if (end <= start) throw new ValidationException("Giờ kết thúc phải sau giờ bắt đầu.", "End");
            if (days.Count == 0) throw new ValidationException("Chọn ít nhất một ngày trong tuần.", "Days");
            int created = 0, skipped = 0;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (!days.Contains(d.DayOfWeek)) continue;
                var s = d.Add(start); var e = d.Add(end);
                if (await FindRoomConflictAsync(roomId, s, e) != null
                    || _db.Sessions.Local.Any(x => x.RoomId == roomId && x.StartTime < e && s < x.EndTime)) { skipped++; continue; }
                _db.Sessions.Add(new ClassSession { ClassId = classId, RoomId = roomId, StartTime = s, EndTime = e });
                created++;
            }
            await _db.SaveChangesAsync();
            return (created, skipped);
        }

        /// <summary>Thêm/sửa một ca học. Nếu đổi giờ/phòng của ca sắp tới thì báo cho thành viên.</summary>
        public async Task SaveSessionAsync(ClassSession input)
        {
            Authorization.Require(UserRole.Manager);
            if (input.EndTime <= input.StartTime) throw new ValidationException("Giờ kết thúc phải sau giờ bắt đầu.", "End");
            var conflict = await FindRoomConflictAsync(input.RoomId, input.StartTime, input.EndTime, input.Id);
            if (conflict != null)
                throw new ValidationException($"Phòng đã có lớp \"{conflict.Class?.Name}\" lúc {conflict.StartTime:HH:mm}–{conflict.EndTime:HH:mm} ngày {conflict.StartTime:dd/MM}.", "Room");

            ClassSession session;
            var changed = false;
            if (input.Id == 0) session = _db.Sessions.Add(new ClassSession { ClassId = input.ClassId }).Entity;
            else
            {
                session = await _db.Sessions.Include(s => s.Class).FirstAsync(s => s.Id == input.Id);
                changed = session.StartTime != input.StartTime || session.RoomId != input.RoomId;
            }
            session.RoomId = input.RoomId;
            session.StartTime = input.StartTime;
            session.EndTime = input.EndTime;
            session.Note = input.Note.Trim();
            if (input.Id != 0) session.Status = input.Status;

            if (changed && session.StartTime > DateTime.Now)
            {
                var room = await _db.Rooms.Where(r => r.Id == session.RoomId).Select(r => r.Name).FirstAsync();
                await QueueClassNoticeAsync(session.ClassId, $"Thay đổi lịch: {session.Class!.Name}",
                    $"Buổi tập lớp {session.Class.Name} được đổi sang {session.StartTime:HH:mm} ngày {session.StartTime:dd/MM/yyyy} tại {room}.");
            }
            await _db.SaveChangesAsync();
        }

        /// <summary>Hủy một ca học và thông báo cho thành viên trong lớp.</summary>
        public async Task CancelSessionAsync(int sessionId, string reason)
        {
            Authorization.Require(UserRole.Manager);
            var s = await _db.Sessions.Include(x => x.Class).FirstAsync(x => x.Id == sessionId);
            s.Status = SessionStatus.Cancelled;
            s.Note = reason.Trim();
            await QueueClassNoticeAsync(s.ClassId, $"Hủy buổi tập: {s.Class!.Name} {s.StartTime:dd/MM}",
                $"Buổi tập lớp {s.Class.Name} lúc {s.StartTime:HH:mm} ngày {s.StartTime:dd/MM/yyyy} bị hủy. Lý do: {(reason.Length > 0 ? reason : "không rõ")}.");
            await _db.SaveChangesAsync();
        }

        public async Task DeleteSessionAsync(int sessionId)
        {
            Authorization.Require(UserRole.Manager);
            if (await _db.Attendances.AnyAsync(a => a.SessionId == sessionId) || await _db.WorkoutLogs.AnyAsync(w => w.SessionId == sessionId))
                throw new ValidationException("Ca học đã có điểm danh/kết quả tập. Hãy hủy ca thay vì xóa.");
            _db.Sessions.Remove(await _db.Sessions.FirstAsync(s => s.Id == sessionId));
            await _db.SaveChangesAsync();
        }

        private async Task QueueClassNoticeAsync(int classId, string title, string body)
        {
            var userIds = await _db.Enrollments.Where(e => e.ClassId == classId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.Member!.UserId).ToListAsync();
            _notify.Queue(userIds, title, body, NotificationType.ScheduleChange);
        }

        // ---------------- Đăng ký lớp ----------------

        public Task<List<Enrollment>> EnrollmentsOfMemberAsync(int memberId) =>
            _db.Enrollments.AsNoTracking()
                .Include(e => e.Class).ThenInclude(c => c!.Sport)
                .Include(e => e.Class).ThenInclude(c => c!.Coach).ThenInclude(c => c!.User)
                .Where(e => e.MemberId == memberId).OrderByDescending(e => e.EnrolledAt).ToListAsync();

        public Task<List<Enrollment>> ClassMembersAsync(int classId) =>
            _db.Enrollments.AsNoTracking().Include(e => e.Member).ThenInclude(m => m!.User)
                .Where(e => e.ClassId == classId && e.Status == EnrollmentStatus.Active).ToListAsync();

        /// <summary>
        /// Đăng ký lớp. Điều kiện: lớp đang mở, còn chỗ, thành viên có gói còn hạn.
        /// Lớp có học phí riêng thì cần phương thức thanh toán (tạo phiếu thu).
        /// Thành viên tự đăng ký; Lễ tân/Quản lý đăng ký hộ.
        /// </summary>
        public async Task<Enrollment> EnrollAsync(int memberId, int classId, PaymentMethod? payWith = null)
        {
            Authorization.Require(UserRole.Member, UserRole.Receptionist, UserRole.Manager);
            var member = await _db.Members.Include(m => m.Subscriptions).ThenInclude(s => s.Plan).FirstAsync(m => m.Id == memberId);
            if (Authorization.Is(UserRole.Member) && member.UserId != AppSession.UserId)
                throw new UnauthorizedAccessException("Bạn chỉ được đăng ký lớp cho chính mình.");
            var cls = await _db.Classes.FirstAsync(c => c.Id == classId);

            if (cls.Status != ClassStatus.Open) throw new ValidationException("Lớp không còn mở đăng ký.");
            if (cls.EndDate.HasValue && cls.EndDate.Value < DateTime.Today) throw new ValidationException("Lớp đã kết thúc.");
            var current = MemberService.CurrentSubscription(member);
            if (current == null) throw new ValidationException("Thành viên chưa có gói còn hiệu lực. Vui lòng đăng ký/gia hạn gói trước.");
            if (cls.Fee == 0 && current.Plan is { IncludesGroupClasses: false })
                throw new ValidationException($"Gói \"{current.Plan.Name}\" không bao gồm lớp nhóm. Vui lòng nâng cấp gói.");
            var enrolled = await _db.Enrollments.CountAsync(e => e.ClassId == classId && e.Status == EnrollmentStatus.Active);
            if (enrolled >= cls.Capacity) throw new ValidationException("Lớp đã đủ sĩ số.");
            if (cls.Fee > 0 && payWith == null) throw new ValidationException($"Lớp có học phí {cls.Fee:N0} ₫, cần chọn phương thức thanh toán.");

            var enrollment = await _db.Enrollments.FirstOrDefaultAsync(e => e.MemberId == memberId && e.ClassId == classId);
            if (enrollment?.Status == EnrollmentStatus.Active) throw new ValidationException("Thành viên đã đăng ký lớp này.");
            if (enrollment == null) enrollment = _db.Enrollments.Add(new Enrollment { MemberId = memberId, ClassId = classId }).Entity;
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.EnrolledAt = DateTime.Now;
            enrollment.CancelledAt = null;

            if (cls.Fee > 0)
            {
                await _db.SaveChangesAsync(); // cần Id của đăng ký cho phiếu thu
                _db.Payments.Add(new Payment
                {
                    InvoiceNo = await PaymentService.NextInvoiceNoAsync(_db), MemberId = memberId, Amount = cls.Fee,
                    Method = payWith!.Value, Purpose = PaymentPurpose.ClassFee, EnrollmentId = enrollment.Id,
                    ReceivedByUserId = Authorization.Is(UserRole.Member) ? null : AppSession.UserId,
                    Note = $"Học phí lớp {cls.Name}"
                });
            }
            await _db.SaveChangesAsync();
            return enrollment;
        }

        public async Task CancelEnrollmentAsync(int enrollmentId)
        {
            Authorization.Require(UserRole.Member, UserRole.Receptionist, UserRole.Manager);
            var e = await _db.Enrollments.Include(x => x.Member).FirstAsync(x => x.Id == enrollmentId);
            if (Authorization.Is(UserRole.Member) && e.Member!.UserId != AppSession.UserId)
                throw new UnauthorizedAccessException("Bạn chỉ được hủy lớp của chính mình.");
            if (e.Status == EnrollmentStatus.Cancelled) return;
            e.Status = EnrollmentStatus.Cancelled;
            e.CancelledAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Một dòng trong bảng điểm danh của buổi tập.</summary>
    public record RosterItem(int MemberId, string FullName, AttendanceStatus? Status, string Notes);

    // =====================================================================
    // ĐIỂM DANH & CHECK-IN
    // =====================================================================
    public class AttendanceService
    {
        private readonly SportsCenterContext _db;
        public AttendanceService(SportsCenterContext db) { _db = db; }

        /// <summary>Danh sách thành viên của lớp kèm trạng thái điểm danh hiện có của buổi.</summary>
        public async Task<List<RosterItem>> RosterAsync(int sessionId)
        {
            var session = await _db.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId);
            var members = await _db.Enrollments.AsNoTracking()
                .Where(e => e.ClassId == session.ClassId && e.Status == EnrollmentStatus.Active)
                .Select(e => new { e.MemberId, e.Member!.User!.FullName }).ToListAsync();
            var existing = await _db.Attendances.AsNoTracking().Where(a => a.SessionId == sessionId).ToListAsync();
            var ids = members.Select(m => m.MemberId).ToHashSet();
            // Người đã điểm danh nhưng sau đó hủy lớp vẫn hiện để giữ lịch sử.
            var extra = existing.Where(a => !ids.Contains(a.MemberId)).Select(a => a.MemberId).ToList();
            var extraNames = await _db.Members.Where(m => extra.Contains(m.Id)).Select(m => new { m.Id, m.User!.FullName }).ToListAsync();

            return members.Select(m => (m.MemberId, m.FullName))
                .Concat(extraNames.Select(x => (x.Id, x.FullName)))
                .Select(m =>
                {
                    var a = existing.FirstOrDefault(x => x.MemberId == m.Item1);
                    return new RosterItem(m.Item1, m.Item2, a?.Status, a?.Notes ?? "");
                })
                .OrderBy(r => r.FullName).ToList();
        }

        /// <summary>HLV phụ trách lớp (hoặc Quản lý) điểm danh cả buổi. Ca đã qua được đánh dấu Hoàn thành.</summary>
        public async Task SaveAttendanceAsync(int sessionId, IEnumerable<(int MemberId, AttendanceStatus Status, string Notes)> items)
        {
            Authorization.Require(UserRole.Coach, UserRole.Manager);
            var session = await _db.Sessions.Include(s => s.Class).ThenInclude(c => c!.Coach).FirstAsync(s => s.Id == sessionId);
            if (Authorization.Is(UserRole.Coach) && session.Class!.Coach?.UserId != AppSession.UserId)
                throw new UnauthorizedAccessException("Bạn chỉ được điểm danh lớp mình phụ trách.");
            if (session.Status == SessionStatus.Cancelled) throw new ValidationException("Ca học đã bị hủy.");
            if (session.StartTime.Date > DateTime.Today) throw new ValidationException("Chưa đến ngày của buổi tập này.");

            var existing = await _db.Attendances.Where(a => a.SessionId == sessionId).ToListAsync();
            foreach (var (memberId, status, notes) in items)
            {
                var a = existing.FirstOrDefault(x => x.MemberId == memberId);
                if (a == null) { a = new Attendance { SessionId = sessionId, MemberId = memberId }; _db.Attendances.Add(a); }
                a.Status = status;
                a.Notes = notes.Trim();
                if (status is AttendanceStatus.Present or AttendanceStatus.Late) a.CheckInTime ??= DateTime.Now;
            }
            if (session.EndTime <= DateTime.Now) session.Status = SessionStatus.Completed;
            await _db.SaveChangesAsync();
        }

        public Task<List<Attendance>> MemberHistoryAsync(int memberId) =>
            _db.Attendances.AsNoTracking()
                .Include(a => a.Session).ThenInclude(s => s!.Class).ThenInclude(c => c!.Coach).ThenInclude(c => c!.User)
                .Include(a => a.Session).ThenInclude(s => s!.Room)
                .Where(a => a.MemberId == memberId).OrderByDescending(a => a.Session!.StartTime).ToListAsync();

        /// <summary>
        /// Check-in tại quầy: kiểm tra gói còn hạn, ghi lượt vào; nếu thành viên có buổi lớp hôm nay
        /// (trong khoảng 60 phút trước giờ bắt đầu đến khi kết thúc) thì điểm danh luôn (Có mặt / Đi muộn).
        /// </summary>
        public async Task<string> CheckInAsync(int memberId)
        {
            Authorization.Require(UserRole.Receptionist, UserRole.Manager);
            var member = await _db.Members.Include(m => m.User).Include(m => m.Subscriptions).FirstAsync(m => m.Id == memberId);
            if (!member.User!.IsActive) throw new ValidationException("Tài khoản thành viên đang bị khóa.");
            var sub = MemberService.CurrentSubscription(member);
            if (sub == null)
            {
                var end = MemberService.MembershipEnd(member);
                throw new ValidationException(end.HasValue
                    ? $"Gói của {member.User.FullName} đã hết hạn ngày {end:dd/MM/yyyy}. Vui lòng gia hạn trước khi vào tập."
                    : $"{member.User.FullName} chưa đăng ký gói thành viên.");
            }

            var now = DateTime.Now;
            _db.CheckIns.Add(new CheckIn { MemberId = memberId, CheckInTime = now, ByUserId = AppSession.UserId });

            var session = await _db.Sessions.Include(s => s.Class)
                .Where(s => s.Status != SessionStatus.Cancelled && s.StartTime <= now.AddMinutes(60) && s.EndTime >= now
                            && s.Class!.Enrollments.Any(e => e.MemberId == memberId && e.Status == EnrollmentStatus.Active))
                .OrderBy(s => s.StartTime).FirstOrDefaultAsync();
            var message = $"Đã check-in {member.User.FullName}. Gói còn hạn đến {sub.EndDate:dd/MM/yyyy} ({(sub.EndDate.Date - DateTime.Today).Days} ngày).";
            if (session != null)
            {
                var a = await _db.Attendances.FirstOrDefaultAsync(x => x.SessionId == session.Id && x.MemberId == memberId);
                if (a == null)
                {
                    var late = now > session.StartTime.AddMinutes(10);
                    _db.Attendances.Add(new Attendance
                    {
                        SessionId = session.Id, MemberId = memberId, CheckInTime = now,
                        Status = late ? AttendanceStatus.Late : AttendanceStatus.Present, Notes = "Check-in tại quầy"
                    });
                    message += $" Đã điểm danh buổi {session.Class!.Name} lúc {session.StartTime:HH:mm}{(late ? " (đi muộn)" : "")}.";
                }
            }
            await _db.SaveChangesAsync();
            return message;
        }

        public Task<List<CheckIn>> CheckInsAsync(DateTime day) =>
            _db.CheckIns.AsNoTracking().Include(c => c.Member).ThenInclude(m => m!.User)
                .Where(c => c.CheckInTime >= day.Date && c.CheckInTime < day.Date.AddDays(1))
                .OrderByDescending(c => c.CheckInTime).ToListAsync();
    }
}
