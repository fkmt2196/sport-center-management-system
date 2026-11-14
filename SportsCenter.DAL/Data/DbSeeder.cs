using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsCenter.DAL.Models;

namespace SportsCenter.DAL.Data
{
    /// <summary>
    /// Dữ liệu mẫu tiếng Việt cho trung tâm thể thao (môi trường Development). Chỉ chạy khi chưa có tài khoản nào.
    /// Mật khẩu mọi tài khoản mẫu: 123456. Danh sách tài khoản: ACCOUNTS.md.
    /// </summary>
    internal static class DbSeeder
    {
        public const string DefaultPassword = "123456";
        private const int MemberCount = 60;
        private static readonly Random Rng = new(20261114);

        #region Dữ liệu nguồn

        private static readonly string[] Surnames = { "Nguyễn", "Nguyễn", "Nguyễn", "Trần", "Trần", "Lê", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
        private static readonly string[] MaleMiddle = { "Văn", "Đức", "Minh", "Quốc", "Hoàng", "Thành", "Gia", "Hữu", "Tuấn" };
        private static readonly string[] FemaleMiddle = { "Thị", "Ngọc", "Thu", "Thanh", "Mai", "Phương", "Bảo", "Khánh", "Minh" };
        private static readonly string[] MaleNames = { "An", "Bình", "Dũng", "Hùng", "Khánh", "Long", "Nam", "Phúc", "Quân", "Sơn", "Tuấn", "Việt", "Huy", "Đạt", "Trung", "Hiếu", "Thắng", "Kiên", "Lâm", "Tài" };
        private static readonly string[] FemaleNames = { "Anh", "Châu", "Diệp", "Hà", "Hương", "Lan", "Linh", "Ngân", "Nhung", "Oanh", "Trang", "Vy", "Yến", "Thảo", "Hằng", "My", "Quyên", "Trâm", "Uyên", "Giang" };
        private static readonly string[] EmailDomains = { "gmail.com", "gmail.com", "gmail.com", "yahoo.com.vn", "outlook.com" };

        private static readonly (string Goal, string Level)[] MemberGoals =
        {
            ("Giảm 5 kg mỡ trong 3 tháng, săn chắc vòng eo", "Beginner"),
            ("Tăng cơ, tăng 3 kg cân nặng nạc", "Intermediate"),
            ("Cải thiện sức bền, chạy được 10 km", "Intermediate"),
            ("Giảm stress, tăng độ dẻo dai và cải thiện tư thế", "Beginner"),
            ("Học bơi thành thạo, bơi liên tục 400 m", "Beginner"),
            ("Học võ tự vệ, nâng cao thể lực", "Beginner"),
            ("Giữ dáng, duy trì sức khỏe sau sinh", "Beginner"),
            ("Tăng sức mạnh: squat 100 kg, deadlift 140 kg", "Advanced"),
            ("Giảm mỡ bụng, cải thiện chỉ số mỡ máu", "Beginner"),
            ("Chuẩn bị thi đấu boxing nghiệp dư", "Advanced"),
        };
        private static readonly string[] HealthNotes = { "", "", "", "", "", "Đau lưng dưới nhẹ, tránh tạ nặng phần lưng", "Từng chấn thương đầu gối phải", "Huyết áp hơi cao, cần theo dõi", "Đau vai trái khi đẩy qua đầu", "Đang mang thai tháng thứ 4" };

        #endregion

        /// <param name="hashPassword">Hàm băm mật khẩu do BLL cung cấp (DAL không chứa nghiệp vụ bảo mật).</param>
        public static void Seed(SportsCenterContext db, Func<string, string> hashPassword)
        {
            if (db.Users.Any()) return;
            db.AuditEnabled = false;
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            var today = DateTime.Today;
            var now = DateTime.Now;
            var phones = new HashSet<string>();

            User NewUser(string username, string fullName, UserRole role, DateTime created, string? emailDomain = null)
            {
                var (given, surname) = (fullName.Split(' ')[^1], fullName.Split(' ')[0]);
                return new User
                {
                    Username = username, PasswordHash = hashPassword(DefaultPassword), FullName = fullName, Role = role,
                    Email = $"{Slug(given)}.{Slug(surname)}.{username}@{emailDomain ?? EmailDomains[Rng.Next(EmailDomains.Length)]}",
                    Phone = UniquePhone(phones), CreatedAt = created,
                    LastLogin = Rng.Next(3) == 0 ? null : now.AddDays(-Rng.Next(0, 20)).AddHours(-Rng.Next(0, 10))
                };
            }

            // ---------------- Nhân sự ----------------
            var manager = NewUser("manager", "Nguyễn Hoàng Minh", UserRole.Manager, today.AddYears(-2), "fitzone.vn");
            var reception1 = NewUser("letan", "Trần Thị Thu Hà", UserRole.Receptionist, today.AddYears(-1), "fitzone.vn");
            var reception2 = NewUser("letan2", "Lê Quốc Bảo", UserRole.Receptionist, today.AddMonths(-8), "fitzone.vn");
            db.Users.AddRange(manager, reception1, reception2);

            // ---------------- Bộ môn & phòng ----------------
            var sports = new Dictionary<string, Sport>();
            foreach (var (name, desc) in new[]
            {
                ("Gym & Thể hình", "Tập tạ, máy và bài tập sức mạnh trong phòng gym."),
                ("Yoga", "Yoga Hatha/Vinyasa giúp dẻo dai, giảm căng thẳng."),
                ("Boxing", "Quyền anh: kỹ thuật đấm, di chuyển, thể lực."),
                ("Bơi lội", "Bơi ếch, bơi sải tại hồ bơi 25 m trong nhà."),
                ("Pilates", "Tăng sức mạnh vùng lõi và cải thiện tư thế."),
                ("Zumba & Aerobic", "Nhảy theo nhạc, đốt calo vui nhộn."),
                ("Muay Thái", "Võ thuật Thái Lan, tự vệ và thể lực."),
                ("Cầu lông", "Cầu lông phong trào, kỹ thuật cơ bản và đánh đôi."),
            })
                sports[name] = new Sport { Name = name, Description = desc };
            db.Sports.AddRange(sports.Values);

            var rooms = new Dictionary<string, Room>();
            foreach (var (name, cap, loc, eq) in new[]
            {
                ("Phòng Gym", 60, "Tầng 1", "Máy chạy, xe đạp, giàn tạ, máy kéo cáp, tạ đơn 2–40 kg"),
                ("Phòng Yoga A", 25, "Tầng 2", "Thảm yoga, gạch, dây đai, gương"),
                ("Phòng Yoga B", 15, "Tầng 2", "Thảm, bóng yoga, gối hỗ trợ bầu"),
                ("Phòng Pilates", 12, "Tầng 2", "Thảm, vòng Pilates, bóng, dây kháng lực"),
                ("Phòng đa năng", 30, "Tầng 3", "Loa, gương, sàn gỗ"),
                ("Võ đài", 20, "Tầng 3", "Võ đài, bao cát, găng, đích đấm"),
                ("Hồ bơi 25m", 30, "Tầng hầm", "Hồ 6 làn, phao, ván tập"),
                ("Sân cầu lông", 16, "Tầng 4", "4 sân thảm, lưới, cầu tập"),
            })
                rooms[name] = new Room { Name = name, Capacity = cap, Location = loc, Equipment = eq };
            db.Rooms.AddRange(rooms.Values);

            // ---------------- Gói thành viên ----------------
            var plans = new[]
            {
                new MembershipPlan { Name = "Gói 1 tháng", Price = 600_000, DurationDays = 30, Description = "Tập không giới hạn phòng gym và các lớp nhóm." },
                new MembershipPlan { Name = "Gói 3 tháng", Price = 1_650_000, DurationDays = 90, Description = "Tiết kiệm 8%, kèm 1 buổi đo InBody." },
                new MembershipPlan { Name = "Gói 6 tháng", Price = 3_000_000, DurationDays = 180, Description = "Tiết kiệm 17%, kèm 2 buổi PT 1-1." },
                new MembershipPlan { Name = "Gói 12 tháng", Price = 5_400_000, DurationDays = 365, Description = "Tiết kiệm 25%, kèm 4 buổi PT 1-1 và tủ đồ riêng." },
                new MembershipPlan { Name = "Gói Gym cơ bản", Price = 400_000, DurationDays = 30, IncludesGroupClasses = false, Description = "Chỉ sử dụng phòng gym, không gồm lớp nhóm." },
                new MembershipPlan { Name = "Gói Sinh viên 3 tháng", Price = 1_200_000, DurationDays = 90, Description = "Dành cho HSSV có thẻ, gồm lớp nhóm." },
                new MembershipPlan { Name = "Gói Hè 2026", Price = 900_000, DurationDays = 60, IsActive = false, Description = "Khuyến mãi hè (đã ngừng bán)." },
            };
            db.Plans.AddRange(plans);

            // ---------------- Huấn luyện viên ----------------
            var coachData = new (string Name, string Specialty, string Certs, int Years, string Bio)[]
            {
                ("Võ Quốc Kiên", "Gym & Thể hình", "Chứng chỉ PT quốc tế ISSA, NASM-CPT", 8, "Chuyên giảm mỡ, tăng cơ và phục hồi chức năng."),
                ("Phạm Ngọc Diệp", "Yoga", "Yoga Alliance RYT-500", 10, "Hatha, Vinyasa và yoga trị liệu cột sống."),
                ("Hồ Văn Lâm", "Boxing", "HLV Boxing cấp quốc gia, cựu VĐV đội tuyển TP.HCM", 12, "Huấn luyện boxing phong trào và thi đấu."),
                ("Trần Bảo Nhung", "Bơi lội", "Chứng chỉ cứu hộ, HLV bơi cấp 2", 7, "Dạy bơi cho người lớn sợ nước."),
                ("Lê Thanh Trâm", "Pilates & Yoga bầu", "STOTT Pilates Mat, Prenatal Yoga", 6, "Pilates phục hồi, yoga cho mẹ bầu."),
                ("Đặng Mai Linh", "Zumba & Aerobic", "Zumba ZIN, Aerobic Fitness Level 2", 5, "Các lớp vũ đạo đốt mỡ năng động."),
                ("Ngô Đức Thắng", "Muay Thái", "Kru Muay Thai (WMC), đai đen Kickboxing", 9, "Muay Thái tự vệ và thể lực."),
                ("Bùi Gia Huy", "Cầu lông", "HLV cầu lông cấp cơ sở", 4, "Kỹ thuật cơ bản, đánh đôi phong trào."),
            };
            var coaches = coachData.Select((c, i) =>
            {
                var hire = today.AddMonths(-Rng.Next(10, 30));
                return new Coach
                {
                    User = NewUser($"hlv{i + 1:00}", c.Name, UserRole.Coach, hire, "fitzone.vn"),
                    Specialty = c.Specialty, Certifications = c.Certs, YearsExperience = c.Years, Bio = c.Bio, HireDate = hire, IsAvailable = true
                };
            }).ToList();
            db.Coaches.AddRange(coaches);
            db.SaveChanges();

            // ---------------- Thành viên ----------------
            var members = new List<Member>();
            for (int i = 0; i < MemberCount; i++)
            {
                var male = Rng.Next(2) == 0;
                var name = $"{Pick(Surnames)} {(male ? Pick(MaleMiddle) : Pick(FemaleMiddle))} {(male ? Pick(MaleNames) : Pick(FemaleNames))}";
                var join = today.AddDays(-Rng.Next(5, 240));
                var goal = Pick(MemberGoals);
                var height = male ? Rng.Next(162, 182) : Rng.Next(150, 168);
                var bmi = 19 + Rng.NextDouble() * 9;
                var health = Pick(HealthNotes);
                if (male && health.Contains("mang thai")) health = "";
                members.Add(new Member
                {
                    User = NewUser($"hv{i + 1:000}", name, UserRole.Member, join),
                    Gender = male ? Gender.Male : Gender.Female,
                    DateOfBirth = new DateTime(today.Year - Rng.Next(18, 52), Rng.Next(1, 13), Rng.Next(1, 28)),
                    HeightCm = height, WeightKg = Math.Round(bmi * height * height / 10000, 1),
                    FitnessLevel = Enum.Parse<FitnessLevel>(goal.Level), Goals = goal.Goal, HealthNotes = health, JoinDate = join
                });
            }
            db.Members.AddRange(members);
            db.SaveChanges();

            // ---------------- Gói đã mua + phiếu thu ----------------
            var payments = new List<Payment>();
            var invoiceSeq = new Dictionary<string, int>();
            string Invoice(DateTime at)
            {
                var prefix = $"HD{at:yyMMdd}-";
                invoiceSeq[prefix] = invoiceSeq.GetValueOrDefault(prefix) + 1;
                return prefix + invoiceSeq[prefix].ToString("D4");
            }
            PaymentMethod RandomMethod() => Rng.Next(100) switch { < 45 => PaymentMethod.Cash, < 75 => PaymentMethod.Transfer, < 90 => PaymentMethod.Card, _ => PaymentMethod.Online };
            int? Cashier(PaymentMethod m) => m == PaymentMethod.Online ? null : (Rng.Next(3) == 0 ? reception2.Id : reception1.Id);

            var subs = new List<Subscription>();
            var gymOnly = new HashSet<int>();
            foreach (var (m, idx) in members.Select((m, i) => (m, i)))
            {
                var start = m.JoinDate;
                // 3 thành viên đầu: gói sắp hết hạn trong tuần (để thử nhắc hết hạn)
                var forceExpiring = idx < 3;
                while (start <= today)
                {
                    var plan = forceExpiring ? plans[0] : PickWeighted(plans[..6], new[] { 30, 30, 12, 6, 12, 10 });
                    if (plan == plans[4]) gymOnly.Add(m.Id);
                    var end = start.AddDays(plan.DurationDays - 1);
                    if (forceExpiring) { end = today.AddDays(idx + 2); start = end.AddDays(-plan.DurationDays + 1); }
                    var paidAt = start.AddHours(Rng.Next(7, 21)).AddMinutes(Rng.Next(60));
                    if (paidAt > now) paidAt = now.AddMinutes(-Rng.Next(30, 300));
                    var sub = new Subscription { MemberId = m.Id, PlanId = plan.Id, StartDate = start, EndDate = end, PricePaid = plan.Price, CreatedAt = paidAt };
                    subs.Add(sub);
                    var method = RandomMethod();
                    payments.Add(new Payment
                    {
                        InvoiceNo = Invoice(paidAt), MemberId = m.Id, Amount = plan.Price, Method = method, Purpose = PaymentPurpose.Subscription,
                        Subscription = sub, PaidAt = paidAt, ReceivedByUserId = Cashier(method),
                        Note = $"{(subs.Count(s => s.MemberId == m.Id) > 1 ? "Gia hạn" : "Đăng ký")} {plan.Name} ({start:dd/MM/yyyy} – {end:dd/MM/yyyy})"
                    });
                    if (forceExpiring) break;
                    // ~25% không gia hạn (tạo thành viên hết hạn); còn lại gia hạn nối tiếp
                    if (Rng.Next(100) < 25 && end < today) break;
                    start = end.AddDays(1 + (Rng.Next(100) < 20 ? Rng.Next(3, 20) : 0));
                }
            }
            db.Subscriptions.AddRange(subs);
            db.SaveChanges();

            bool CoveredOn(int memberId, DateTime d) => subs.Any(s => s.MemberId == memberId && s.StartDate <= d && s.EndDate >= d);

            // ---------------- Lớp & ca học ----------------
            var C = coaches;
            var classSpecs = new (string Name, string Sport, FitnessLevel Level, int Cap, decimal Fee, Coach? Coach, DayOfWeek[] Days, TimeSpan Start, TimeSpan End, string Room, int StartOffset, int EndOffset, string Desc)[]
            {
                ("Gym nhập môn", "Gym & Thể hình", FitnessLevel.Beginner, 20, 0, C[0], new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, T(6, 0), T(7, 0), "Phòng Gym", -170, 60, "Làm quen máy tập, kỹ thuật cơ bản an toàn."),
                ("Thể hình tăng cơ", "Gym & Thể hình", FitnessLevel.Intermediate, 15, 0, C[0], new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday }, T(18, 0), T(19, 30), "Phòng Gym", -150, 90, "Giáo án chia nhóm cơ, tăng tải tiến bộ."),
                ("Yoga cơ bản", "Yoga", FitnessLevel.Beginner, 20, 0, C[1], new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, T(18, 0), T(19, 0), "Phòng Yoga A", -120, 45, "Hatha yoga cho người mới, hơi thở và tư thế."),
                ("Yoga nâng cao", "Yoga", FitnessLevel.Advanced, 15, 0, C[1], new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, T(6, 30), T(7, 30), "Phòng Yoga A", -180, -30, "Vinyasa flow, tư thế đảo ngược."),
                ("Boxing cơ bản", "Boxing", FitnessLevel.Beginner, 16, 300_000, C[2], new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, T(19, 0), T(20, 30), "Võ đài", -100, 30, "Thế đứng, cú đấm cơ bản, di chuyển."),
                ("Bơi ếch cho người mới", "Bơi lội", FitnessLevel.Beginner, 12, 800_000, C[3], new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, T(8, 0), T(9, 0), "Hồ bơi 25m", -160, -60, "Làm quen nước, thở, bơi ếch 25 m."),
                ("Bơi sải nâng cao", "Bơi lội", FitnessLevel.Intermediate, 10, 900_000, C[3], new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, T(17, 30), T(18, 30), "Hồ bơi 25m", -60, 60, "Kỹ thuật sải, quay vòng, bơi bền."),
                ("Pilates trên thảm", "Pilates", FitnessLevel.Beginner, 12, 0, C[4], new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, T(9, 0), T(10, 0), "Phòng Pilates", -90, 30, "Core, tư thế, phục hồi lưng."),
                ("Zumba giảm cân", "Zumba & Aerobic", FitnessLevel.Beginner, 25, 0, C[5], new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, T(19, 0), T(20, 0), "Phòng đa năng", -170, 75, "Vũ đạo Latin đốt 500–700 kcal/buổi."),
                ("Aerobic buổi sáng", "Zumba & Aerobic", FitnessLevel.Beginner, 25, 0, C[5], new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday }, T(6, 0), T(7, 0), "Phòng đa năng", -140, -10, "Aerobic nhịp nhanh khởi động ngày mới."),
                ("Muay Thái tự vệ", "Muay Thái", FitnessLevel.Intermediate, 16, 400_000, C[6], new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, T(19, 30), T(21, 0), "Võ đài", -80, 40, "Đòn chân, gối, chỏ; bài tập ôm ghì."),
                ("Cầu lông phong trào", "Cầu lông", FitnessLevel.Beginner, 16, 250_000, C[7], new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, T(16, 0), T(17, 30), "Sân cầu lông", -45, 75, "Kỹ thuật cầm vợt, phát cầu, đánh đôi."),
                ("Yoga bầu", "Yoga", FitnessLevel.Beginner, 10, 500_000, C[4], new[] { DayOfWeek.Saturday }, T(9, 0), T(10, 0), "Phòng Yoga B", -30, 60, "Yoga an toàn cho mẹ bầu từ tháng thứ 4."),
                ("Boxing thi đấu", "Boxing", FitnessLevel.Advanced, 10, 600_000, C[2], new[] { DayOfWeek.Saturday }, T(14, 0), T(16, 0), "Võ đài", -20, 100, "Đối kháng, chiến thuật, chuẩn bị giải nghiệp dư."),
                ("Kickboxing cho nữ", "Muay Thái", FitnessLevel.Beginner, 15, 350_000, null, new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, T(9, 0), T(10, 0), "Võ đài", 20, 110, "Lớp mới – chờ phân công huấn luyện viên."),
            };

            var classes = new List<TrainingClass>();
            var sessions = new List<ClassSession>();
            var specByClass = new Dictionary<TrainingClass, (DayOfWeek[] Days, TimeSpan Start, TimeSpan End, Room Room)>();
            foreach (var s in classSpecs)
            {
                var start = today.AddDays(s.StartOffset);
                var end = today.AddDays(s.EndOffset);
                var cls = new TrainingClass
                {
                    Name = s.Name, SportId = sports[s.Sport].Id, CoachId = s.Coach?.Id, Level = s.Level, Capacity = s.Cap, Fee = s.Fee,
                    StartDate = start, EndDate = end, Description = s.Desc,
                    Status = end < today ? ClassStatus.Completed : ClassStatus.Open
                };
                classes.Add(cls);
                specByClass[cls] = (s.Days, s.Start, s.End, rooms[s.Room]);
                if (s.Coach == null) continue; // lớp mới chưa phân công: chưa xếp lịch
                var last = end < today.AddDays(28) ? end : today.AddDays(28);
                for (var d = start; d <= last; d = d.AddDays(1))
                {
                    if (!s.Days.Contains(d.DayOfWeek)) continue;
                    var st = d.Add(s.Start);
                    var past = d.Add(s.End) < now;
                    var cancelled = Rng.Next(100) < 2;
                    sessions.Add(new ClassSession
                    {
                        Class = cls, RoomId = rooms[s.Room].Id, StartTime = st, EndTime = d.Add(s.End),
                        Status = cancelled ? SessionStatus.Cancelled : past ? SessionStatus.Completed : SessionStatus.Scheduled,
                        Note = cancelled ? Pick(new[] { "HLV ốm, nghỉ buổi", "Bảo trì thiết bị", "Nghỉ lễ" }) : ""
                    });
                }
            }
            db.Classes.AddRange(classes);
            db.Sessions.AddRange(sessions);
            db.SaveChanges();

            // ---------------- Đăng ký lớp ----------------
            var enrollments = new List<Enrollment>();
            foreach (var cls in classes.Where(c => c.CoachId != null))
            {
                var target = (int)Math.Round(cls.Capacity * (0.45 + Rng.NextDouble() * 0.4));
                var lastDay = cls.EndDate < today ? cls.EndDate!.Value : today;
                var candidates = members.Where(m => m.JoinDate <= lastDay && (cls.Fee > 0 || !gymOnly.Contains(m.Id)))
                    .OrderBy(_ => Rng.Next()).ToList();
                foreach (var m in candidates)
                {
                    if (enrollments.Count(e => e.ClassId == cls.Id) >= target) break;
                    var from = cls.StartDate > m.JoinDate ? cls.StartDate : m.JoinDate;
                    var at = from.AddDays(Rng.Next(0, 10)).AddHours(Rng.Next(8, 21));
                    if (at > now || (cls.EndDate.HasValue && at.Date > cls.EndDate.Value) || !CoveredOn(m.Id, at.Date)) continue;
                    var cancelled = Rng.Next(100) < 6;
                    var cancelAt = cancelled ? at.AddDays(Rng.Next(7, 40)) : (DateTime?)null;
                    if (cancelAt > now) cancelAt = now.AddDays(-1);
                    enrollments.Add(new Enrollment
                    {
                        MemberId = m.Id, ClassId = cls.Id, EnrolledAt = at,
                        Status = cancelled ? EnrollmentStatus.Cancelled : EnrollmentStatus.Active, CancelledAt = cancelAt
                    });
                }
            }
            db.Enrollments.AddRange(enrollments);
            db.SaveChanges();
            foreach (var e in enrollments)
            {
                var cls = classes.First(c => c.Id == e.ClassId);
                if (cls.Fee <= 0) continue;
                var method = RandomMethod();
                payments.Add(new Payment
                {
                    InvoiceNo = Invoice(e.EnrolledAt), MemberId = e.MemberId, Amount = cls.Fee, Method = method, Purpose = PaymentPurpose.ClassFee,
                    EnrollmentId = e.Id, PaidAt = e.EnrolledAt, ReceivedByUserId = Cashier(method), Note = $"Học phí lớp {cls.Name}"
                });
            }
            db.Payments.AddRange(payments);
            db.SaveChanges();

            // ---------------- Điểm danh & check-in ----------------
            var attendances = new List<Attendance>();
            var checkIns = new List<CheckIn>();
            var byClass = enrollments.GroupBy(e => e.ClassId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var s in sessions.Where(s => s.Status == SessionStatus.Completed))
            {
                if (!byClass.TryGetValue(s.ClassId, out var list)) continue;
                foreach (var e in list.Where(e => e.EnrolledAt <= s.StartTime && (e.CancelledAt == null || e.CancelledAt > s.StartTime)))
                {
                    var roll = Rng.Next(100);
                    var status = roll < 76 ? AttendanceStatus.Present : roll < 84 ? AttendanceStatus.Late : roll < 95 ? AttendanceStatus.Absent : AttendanceStatus.Excused;
                    DateTime? checkIn = status switch
                    {
                        AttendanceStatus.Present => s.StartTime.AddMinutes(-Rng.Next(2, 20)),
                        AttendanceStatus.Late => s.StartTime.AddMinutes(Rng.Next(11, 25)),
                        _ => null
                    };
                    attendances.Add(new Attendance
                    {
                        SessionId = s.Id, MemberId = e.MemberId, Status = status, CheckInTime = checkIn,
                        Notes = status switch { AttendanceStatus.Late => "Đến muộn", AttendanceStatus.Excused => "Có xin phép", _ => "" }
                    });
                    if (checkIn.HasValue) checkIns.Add(new CheckIn { MemberId = e.MemberId, CheckInTime = checkIn.Value, ByUserId = reception1.Id });
                }
            }
            // Lượt tập gym tự do (không theo lớp) của các thành viên còn hạn gói
            foreach (var m in members)
                for (int k = 0; k < Rng.Next(0, 25); k++)
                {
                    var d = today.AddDays(-Rng.Next(0, 120)).AddHours(Rng.Next(6, 21)).AddMinutes(Rng.Next(60));
                    if (d > now || !CoveredOn(m.Id, d.Date)) continue;
                    checkIns.Add(new CheckIn { MemberId = m.Id, CheckInTime = d, ByUserId = Rng.Next(2) == 0 ? reception1.Id : reception2.Id });
                }
            db.Attendances.AddRange(attendances);
            db.CheckIns.AddRange(checkIns);
            db.SaveChanges();

            // ---------------- Kế hoạch tập, kết quả, đánh giá ----------------
            var plansTp = new List<TrainingPlan>();
            var logs = new List<WorkoutLog>();
            var reviews = new List<ProgressReview>();
            foreach (var cls in classes.Where(c => c.CoachId != null && c.Status == ClassStatus.Open))
            {
                plansTp.Add(new TrainingPlan
                {
                    CoachId = cls.CoachId!.Value, ClassId = cls.Id, Title = $"Giáo án lớp {cls.Name} – tháng {today.Month}",
                    Goal = cls.Description, StartDate = new DateTime(today.Year, today.Month, 1), EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
                    Content = ClassPlanContent(cls.Name), CreatedAt = new DateTime(today.Year, today.Month, 1).AddHours(9)
                });
            }
            var active = enrollments.Where(e => e.Status == EnrollmentStatus.Active).ToList();
            foreach (var e in active.OrderBy(_ => Rng.Next()).Take(45))
            {
                var cls = classes.First(c => c.Id == e.ClassId);
                var m = members.First(x => x.Id == e.MemberId);
                if (Rng.Next(100) < 45 && !plansTp.Any(p => p.MemberId == m.Id))
                    plansTp.Add(new TrainingPlan
                    {
                        CoachId = cls.CoachId!.Value, MemberId = m.Id, Title = $"Kế hoạch cá nhân – {m.User!.FullName}",
                        Goal = m.Goals, StartDate = today.AddDays(-Rng.Next(5, 40)), EndDate = today.AddDays(Rng.Next(20, 60)),
                        Content = PersonalPlanContent(m), CreatedAt = today.AddDays(-Rng.Next(5, 40))
                    });

                // Kết quả tập các buổi gần đây + đánh giá hằng tháng
                var attended = attendances.Where(a => a.MemberId == m.Id && a.Status is AttendanceStatus.Present or AttendanceStatus.Late)
                    .Select(a => sessions.First(s => s.Id == a.SessionId)).Where(s => s.ClassId == cls.Id)
                    .OrderByDescending(s => s.StartTime).Take(6).ToList();
                var weight = m.WeightKg ?? 65;
                foreach (var s in attended.OrderBy(s => s.StartTime))
                {
                    weight = Math.Round(weight - 0.1 - Rng.NextDouble() * 0.3, 1);
                    logs.Add(new WorkoutLog
                    {
                        MemberId = m.Id, CoachId = cls.CoachId, SessionId = s.Id, Date = s.StartTime.Date,
                        Exercises = SessionResult(cls.Name), DurationMinutes = (int)(s.EndTime - s.StartTime).TotalMinutes,
                        WeightKg = weight, Notes = Pick(new[] { "Hoàn thành tốt", "Cần chú ý kỹ thuật", "Tăng tải được", "Hơi mệt, giữ cường độ", "Tiến bộ rõ" })
                    });
                }
                var months = Math.Min(4, Math.Max(1, (today - e.EnrolledAt).Days / 30));
                for (int k = months; k >= 1; k--)
                    reviews.Add(new ProgressReview
                    {
                        MemberId = m.Id, CoachId = cls.CoachId!.Value, Date = today.AddDays(-30 * k + Rng.Next(0, 5)),
                        Score = Math.Clamp(5 + (months - k) + Rng.Next(-1, 2), 3, 10),
                        Comment = Pick(new[]
                        {
                            "Kỹ thuật cải thiện rõ, cần duy trì đều 3 buổi/tuần.",
                            "Sức bền tăng, nên bổ sung bài tập core.",
                            "Tư thế còn sai ở bài squat, chú ý giữ lưng thẳng.",
                            "Chăm chỉ, đạt mục tiêu tháng. Tăng nhẹ cường độ.",
                            "Cần ngủ đủ và ăn đủ đạm để phục hồi tốt hơn.",
                        })
                    });
            }
            db.TrainingPlans.AddRange(plansTp);
            db.WorkoutLogs.AddRange(logs);
            db.ProgressReviews.AddRange(reviews);

            // ---------------- Thông báo ----------------
            var notifications = new List<Notification>();
            foreach (var m in members)
                notifications.Add(new Notification
                {
                    UserId = m.UserId, Title = $"Chào mừng đến {CenterName}!", Type = NotificationType.General, CreatedAt = m.JoinDate.AddHours(10), IsRead = true,
                    Body = "Cảm ơn bạn đã tham gia. Hãy cập nhật mục tiêu tập luyện trong Hồ sơ để HLV hỗ trợ tốt hơn."
                });
            foreach (var s in sessions.Where(s => s.Status == SessionStatus.Cancelled && byClass.ContainsKey(s.ClassId)))
                foreach (var e in byClass[s.ClassId].Where(e => e.EnrolledAt < s.StartTime))
                    notifications.Add(new Notification
                    {
                        UserId = members.First(m => m.Id == e.MemberId).UserId, SenderUserId = manager.Id, Type = NotificationType.ScheduleChange,
                        Title = $"Hủy buổi tập: {classes.First(c => c.Id == s.ClassId).Name} {s.StartTime:dd/MM}", CreatedAt = s.StartTime.AddDays(-1),
                        IsRead = s.StartTime < today.AddDays(-7), Body = $"Buổi tập lúc {s.StartTime:HH:mm} ngày {s.StartTime:dd/MM/yyyy} bị hủy. Lý do: {s.Note}."
                    });
            foreach (var cls in classes.Where(c => c.CoachId != null && c.Status == ClassStatus.Open))
            {
                var coachUser = coaches.First(c => c.Id == cls.CoachId).UserId;
                foreach (var e in active.Where(e => e.ClassId == cls.Id))
                    notifications.Add(new Notification
                    {
                        UserId = members.First(m => m.Id == e.MemberId).UserId, SenderUserId = coachUser, Type = NotificationType.Homework,
                        Title = $"Bài tập về nhà – {cls.Name}", CreatedAt = today.AddDays(-Rng.Next(1, 6)).AddHours(20), IsRead = Rng.Next(2) == 0,
                        Body = HomeworkFor(cls.Name)
                    });
            }
            db.Notifications.AddRange(notifications);

            // ---------------- Yêu cầu hỗ trợ ----------------
            var supportSubjects = new (string Subject, string Desc, SupportStatus Status, string Response)[]
            {
                ("Mất thẻ thành viên", "Thành viên làm mất thẻ, cần cấp lại.", SupportStatus.Resolved, "Đã cấp thẻ mới, phí 50.000 ₫."),
                ("Tủ đồ số 12 bị kẹt khóa", "Không mở được tủ sau buổi tập tối.", SupportStatus.Resolved, "Kỹ thuật đã thay ổ khóa."),
                ("Xin bảo lưu gói 1 tháng", "Đi công tác nước ngoài 3 tuần, xin bảo lưu.", SupportStatus.InProgress, "Đang chờ quản lý duyệt."),
                ("Máy chạy số 3 phát tiếng kêu", "Máy chạy bộ số 3 kêu to khi chạy trên 10 km/h.", SupportStatus.Open, ""),
                ("Đổi lớp Yoga sang buổi sáng", "Muốn chuyển từ Yoga cơ bản buổi tối sang lớp buổi sáng.", SupportStatus.Open, ""),
                ("Xuất hóa đơn VAT công ty", "Cần hóa đơn VAT cho gói 12 tháng.", SupportStatus.Resolved, "Đã gửi hóa đơn điện tử qua email."),
                ("Nước hồ bơi hơi lạnh", "Phản ánh nước hồ bơi buổi sáng lạnh.", SupportStatus.InProgress, "Đã báo bộ phận kỹ thuật kiểm tra máy gia nhiệt."),
                ("Đăng ký PT 1-1", "Muốn thuê PT riêng 12 buổi để giảm cân.", SupportStatus.Open, ""),
            };
            foreach (var (s, i) in supportSubjects.Select((s, i) => (s, i)))
            {
                var created = now.AddDays(-Rng.Next(0, 40)).AddHours(-Rng.Next(0, 8));
                db.SupportRequests.Add(new SupportRequest
                {
                    MemberId = members[Rng.Next(members.Count)].Id, CreatedByUserId = i % 3 == 0 ? reception2.Id : reception1.Id,
                    Subject = s.Subject, Description = s.Desc, Status = s.Status, Response = s.Response, CreatedAt = created,
                    ResolvedAt = s.Status == SupportStatus.Resolved ? created.AddHours(Rng.Next(2, 48)) : null
                });
            }

            // ---------------- Audit log mẫu ----------------
            var staff = new[] { manager, reception1, reception2 }.Concat(coaches.Select(c => c.User!)).ToList();
            for (int i = 0; i < 40; i++)
            {
                var u = staff[Rng.Next(staff.Count)];
                var at = now.AddDays(-Rng.Next(0, 14)).AddMinutes(-Rng.Next(0, 600));
                db.AuditLogs.Add(new AuditLog { UserId = u.Id, Username = u.Username, Action = "Đăng nhập", EntityName = nameof(User), EntityId = u.Id.ToString(), Details = $"Vai trò: {u.Role}", Timestamp = at });
            }
            db.AuditLogs.Add(new AuditLog { UserId = manager.Id, Username = manager.Username, Action = "Cập nhật", EntityName = nameof(MembershipPlan), EntityId = plans[6].Id.ToString(), Details = "Gói Hè 2026 | IsActive: True → False", Timestamp = now.AddDays(-75) });

            db.ChangeTracker.DetectChanges();
            db.SaveChanges();
            db.ChangeTracker.AutoDetectChangesEnabled = true;
            db.AuditEnabled = true;
        }

        private const string CenterName = "FitZone Sports Center";

        #region Nội dung mẫu

        private static string ClassPlanContent(string cls) => cls switch
        {
            var c when c.Contains("Gym") || c.Contains("Thể hình") =>
                "Tuần 1–2: Squat, Bench press, Lat pulldown 3×12; Plank 3×40s.\nTuần 3–4: Tăng tải 5%, thêm Romanian deadlift 3×10, Shoulder press 3×10.\nCardio kết thúc 10 phút.",
            var c when c.Contains("Yoga") =>
                "Khởi động khớp 10 phút → Chào mặt trời A/B 5 vòng → Chuỗi chiến binh 1-2-3 → Cân bằng (cây, đại bàng) → Gập trước, vặn xoắn → Thư giãn 10 phút.",
            var c when c.Contains("Boxing") || c.Contains("Muay") || c.Contains("Kick") =>
                "Nhảy dây 3×3 phút → Shadow boxing 3 hiệp → Đấm đích combo 1-2, 1-2-3, 1-2-hook → Bao cát 5×2 phút → Thể lực: burpee, plank, gập bụng.",
            var c when c.Contains("Bơi") =>
                "Khởi động cạn 10 phút → Thở nước 5 phút → Đạp chân với ván 4×25 m → Tay sải/ếch 4×25 m → Phối hợp 6×25 m → Thả lỏng 100 m.",
            var c when c.Contains("Pilates") =>
                "The Hundred, Roll up, Single leg circle, Spine stretch, Swimming, Side kick series – mỗi bài 8–10 lần, tập trung hơi thở.",
            var c when c.Contains("Zumba") || c.Contains("Aerobic") =>
                "Khởi động 8 phút → 4 bài Merengue/Salsa/Reggaeton/Cumbia → 2 bài cường độ cao → Giãn cơ 7 phút.",
            _ => "Khởi động → Kỹ thuật cơ bản → Bài tập theo cặp → Đấu tập ngắn → Giãn cơ."
        };

        private static string PersonalPlanContent(Member m) =>
            m.Goals.Contains("Giảm") ? "Thứ 2/4/6: Toàn thân (Squat, Hít đất, Kéo cáp 3×12) + HIIT 15 phút.\nThứ 3/5: Cardio vùng 2 30–40 phút.\nĂn thâm hụt 400 kcal, 1,6 g đạm/kg."
            : m.Goals.Contains("Tăng") ? "Đẩy/Kéo/Chân 4 buổi/tuần, 4×8–10, tăng tải 2,5 kg/tuần khi đạt đủ số lần.\nThặng dư 300 kcal, 2 g đạm/kg."
            : m.Goals.Contains("bơi") ? "3 buổi/tuần: thở nước, đạp chân ván, phối hợp tay chân; mục tiêu 400 m liên tục sau 8 tuần."
            : "3 buổi/tuần: 2 buổi sức mạnh toàn thân + 1 buổi yoga/giãn cơ. Đi bộ 8.000 bước/ngày.";

        private static string SessionResult(string cls) => cls switch
        {
            var c when c.Contains("Gym") || c.Contains("Thể hình") => $"Squat {Rng.Next(30, 90)} kg 4×8; Bench {Rng.Next(25, 70)} kg 4×8; Lat pulldown {Rng.Next(30, 60)} kg 3×10",
            var c when c.Contains("Boxing") || c.Contains("Muay") => $"Bao cát 6×2 phút; combo 1-2-3 {Rng.Next(80, 160)} cú; nhảy dây {Rng.Next(6, 12)} phút",
            var c when c.Contains("Bơi") => $"Bơi {Rng.Next(200, 800)} m; 25 m nhanh nhất {Rng.Next(22, 45)} giây",
            var c when c.Contains("Yoga") || c.Contains("Pilates") => $"Giữ plank {Rng.Next(40, 120)} giây; gập trước chạm sàn: {(Rng.Next(2) == 0 ? "đạt" : "gần đạt")}",
            var c when c.Contains("Zumba") || c.Contains("Aerobic") => $"Đốt khoảng {Rng.Next(400, 700)} kcal; nhịp tim TB {Rng.Next(130, 160)} bpm",
            _ => $"Đánh {Rng.Next(3, 8)} séc đôi; phát cầu chuẩn {Rng.Next(10, 20)}/20"
        };

        private static string HomeworkFor(string cls) => cls switch
        {
            var c when c.Contains("Yoga") || c.Contains("Pilates") => "Tập chuỗi chào mặt trời 5 vòng mỗi sáng và giữ plank 3×30 giây.",
            var c when c.Contains("Bơi") => "Tập thở bóng (thở ra dưới nước) 5 phút/ngày trong bồn rửa mặt hoặc khi tắm.",
            var c when c.Contains("Boxing") || c.Contains("Muay") => "Shadow boxing 3 hiệp × 2 phút và nhảy dây 10 phút, 4 ngày/tuần.",
            var c when c.Contains("Zumba") || c.Contains("Aerobic") => "Ôn lại 2 bài vũ đạo mới (video đã gửi nhóm) và đi bộ nhanh 30 phút.",
            var c when c.Contains("Cầu lông") => "Tập bước chân 4 góc sân 10 phút và cổ tay với vợt 3×20 lần.",
            _ => "Squat bodyweight 3×15, hít đất 3×10, plank 3×30 giây vào ngày nghỉ."
        };

        #endregion

        #region Tiện ích

        private static TimeSpan T(int h, int m) => new(h, m, 0);
        private static T Pick<T>(T[] arr) => arr[Rng.Next(arr.Length)];

        private static T PickWeighted<T>(T[] items, int[] weights)
        {
            var roll = Rng.Next(weights.Sum());
            for (int i = 0; i < items.Length; i++) { if (roll < weights[i]) return items[i]; roll -= weights[i]; }
            return items[^1];
        }

        private static string UniquePhone(HashSet<string> used)
        {
            string p;
            do p = "09" + Rng.Next(0, 100_000_000).ToString("D8"); while (!used.Add(p));
            return p;
        }

        private static string Slug(string text)
        {
            var n = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in n)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        #endregion
    }
}
