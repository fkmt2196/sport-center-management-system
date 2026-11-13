using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Data
{
    public class SportsCenterContext : DbContext
    {
        public SportsCenterContext(DbContextOptions<SportsCenterContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Member> Members => Set<Member>();
        public DbSet<Coach> Coaches => Set<Coach>();
        public DbSet<Sport> Sports => Set<Sport>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<TrainingClass> Classes => Set<TrainingClass>();
        public DbSet<ClassSession> Sessions => Set<ClassSession>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<CheckIn> CheckIns => Set<CheckIn>();
        public DbSet<MembershipPlan> Plans => Set<MembershipPlan>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
        public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
        public DbSet<ProgressReview> ProgressReviews => Set<ProgressReview>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        /// <summary>Tắt khi seed dữ liệu để không sinh hàng nghìn dòng audit.</summary>
        public bool AuditEnabled { get; set; } = true;

        protected override void ConfigureConventions(ModelConfigurationBuilder b)
        {
            // Enum lưu dạng chuỗi cho dễ đọc khi tra CSDL; tiền VNĐ không có phần lẻ.
            b.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<Gender>().HaveConversion<string>().HaveMaxLength(10);
            b.Properties<FitnessLevel>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<ClassStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<SessionStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<EnrollmentStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<AttendanceStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<SubscriptionStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<PaymentPurpose>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<SupportStatus>().HaveConversion<string>().HaveMaxLength(20);
            b.Properties<decimal>().HavePrecision(18, 0);
        }

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<User>(e =>
            {
                e.Property(x => x.Username).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.Username).IsUnique();
                e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
                e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
                e.Property(x => x.Email).HasMaxLength(150);
                e.Property(x => x.Phone).HasMaxLength(20);
            });
            m.Entity<Member>(e =>
            {
                e.HasOne(x => x.User).WithOne(u => u.Member).HasForeignKey<Member>(x => x.UserId);
                e.Property(x => x.Goals).HasMaxLength(500);
                e.Property(x => x.HealthNotes).HasMaxLength(500);
            });
            m.Entity<Coach>(e =>
            {
                e.HasOne(x => x.User).WithOne(u => u.Coach).HasForeignKey<Coach>(x => x.UserId);
                e.Property(x => x.Specialty).HasMaxLength(150);
                e.Property(x => x.Certifications).HasMaxLength(500);
                e.Property(x => x.Bio).HasMaxLength(1000);
            });
            m.Entity<Sport>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Description).HasMaxLength(500);
            });
            m.Entity<Room>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Location).HasMaxLength(200);
                e.Property(x => x.Equipment).HasMaxLength(500);
            });
            m.Entity<TrainingClass>(e =>
            {
                e.ToTable("Classes");
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.HasOne(x => x.Sport).WithMany(s => s.Classes).HasForeignKey(x => x.SportId);
                e.HasOne(x => x.Coach).WithMany(c => c.Classes).HasForeignKey(x => x.CoachId);
            });
            m.Entity<ClassSession>(e =>
            {
                e.ToTable("ClassSessions");
                e.HasOne(x => x.Class).WithMany(c => c.Sessions).HasForeignKey(x => x.ClassId);
                e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId);
                e.Property(x => x.Note).HasMaxLength(300);
                e.HasIndex(x => x.StartTime);
            });
            m.Entity<Enrollment>(e =>
            {
                e.HasOne(x => x.Member).WithMany(mb => mb.Enrollments).HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Class).WithMany(c => c.Enrollments).HasForeignKey(x => x.ClassId);
                e.HasIndex(x => new { x.MemberId, x.ClassId }).IsUnique();
            });
            m.Entity<Attendance>(e =>
            {
                e.HasOne(x => x.Session).WithMany(s => s.Attendances).HasForeignKey(x => x.SessionId);
                e.HasOne(x => x.Member).WithMany(mb => mb.Attendances).HasForeignKey(x => x.MemberId);
                e.HasIndex(x => new { x.SessionId, x.MemberId }).IsUnique();
                e.Property(x => x.Notes).HasMaxLength(500);
            });
            m.Entity<CheckIn>(e =>
            {
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.Property(x => x.Note).HasMaxLength(300);
                e.HasIndex(x => x.CheckInTime);
            });
            m.Entity<MembershipPlan>(e =>
            {
                e.ToTable("MembershipPlans");
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
            });
            m.Entity<Subscription>(e =>
            {
                e.HasOne(x => x.Member).WithMany(mb => mb.Subscriptions).HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
                e.Property(x => x.Note).HasMaxLength(300);
            });
            m.Entity<Payment>(e =>
            {
                e.Property(x => x.InvoiceNo).HasMaxLength(30).IsRequired();
                e.HasIndex(x => x.InvoiceNo).IsUnique();
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId);
                e.HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => x.EnrollmentId);
                e.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedByUserId);
                e.Property(x => x.Note).HasMaxLength(300);
                e.HasIndex(x => x.PaidAt);
            });
            m.Entity<TrainingPlan>(e =>
            {
                e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId);
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Goal).HasMaxLength(300);
                e.Property(x => x.Content).HasMaxLength(4000);
            });
            m.Entity<WorkoutLog>(e =>
            {
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId);
                e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
                e.Property(x => x.Exercises).HasMaxLength(2000);
                e.Property(x => x.Notes).HasMaxLength(500);
            });
            m.Entity<ProgressReview>(e =>
            {
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId);
                e.Property(x => x.Comment).HasMaxLength(1000);
            });
            m.Entity<Notification>(e =>
            {
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
                e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderUserId);
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Body).HasMaxLength(2000);
                e.HasIndex(x => new { x.UserId, x.IsRead });
            });
            m.Entity<SupportRequest>(e =>
            {
                e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId);
                e.Property(x => x.Subject).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(2000);
                e.Property(x => x.Response).HasMaxLength(1000);
            });
            m.Entity<AuditLog>(e =>
            {
                e.Property(x => x.Username).HasMaxLength(50);
                e.Property(x => x.Action).HasMaxLength(50);
                e.Property(x => x.EntityName).HasMaxLength(50);
                e.Property(x => x.EntityId).HasMaxLength(50);
                e.Property(x => x.Details).HasMaxLength(2000);
                e.HasIndex(x => x.Timestamp);
            });

            // Mặc định KHÔNG xóa dây chuyền (tránh mất dữ liệu lịch sử và lỗi "multiple cascade paths"
            // của SQL Server). Chỉ các quan hệ "thuộc về" thật sự mới xóa theo.
            var cascade = new HashSet<(Type Dependent, Type Principal)>
            {
                (typeof(Member), typeof(User)),
                (typeof(Coach), typeof(User)),
                (typeof(ClassSession), typeof(TrainingClass)),
                (typeof(Attendance), typeof(ClassSession)),
                (typeof(Enrollment), typeof(TrainingClass)),
                (typeof(Notification), typeof(User)),
            };
            foreach (var fk in m.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            {
                var key = (fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType);
                fk.DeleteBehavior = cascade.Contains(key) && !(key == (typeof(Notification), typeof(User)) && fk.Properties[0].Name == "SenderUserId")
                    ? DeleteBehavior.Cascade
                    : DeleteBehavior.Restrict;
            }
        }

        #region Audit log tự động

        private static readonly HashSet<string> IgnoredProps = new() { nameof(User.LastLogin) };

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var pending = CollectAudit();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            if (pending.Count > 0) { AuditLogs.AddRange(pending.Select(p => p.ToLog())); base.SaveChanges(acceptAllChangesOnSuccess); }
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            var pending = CollectAudit();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (pending.Count > 0) { AuditLogs.AddRange(pending.Select(p => p.ToLog())); await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
            return result;
        }

        private sealed class PendingAudit
        {
            public required EntityEntry Entry;
            public required string Action;
            public required string Details;
            public string? KeyBefore;

            public AuditLog ToLog() => new()
            {
                UserId = AppSession.UserId,
                Username = AppSession.Username,
                Action = Action,
                EntityName = Entry.Metadata.ClrType.Name,
                EntityId = KeyBefore ?? KeyOf(Entry),
                Details = Details.Length > 2000 ? Details[..1997] + "..." : Details,
                Timestamp = DateTime.Now
            };
        }

        private List<PendingAudit> CollectAudit()
        {
            var list = new List<PendingAudit>();
            if (!AuditEnabled) return list;
            ChangeTracker.DetectChanges();
            foreach (var e in ChangeTracker.Entries())
            {
                if (e.Entity is AuditLog) continue;
                switch (e.State)
                {
                    case EntityState.Added:
                        list.Add(new PendingAudit { Entry = e, Action = "Thêm", Details = Summary(e, current: true) });
                        break;
                    case EntityState.Deleted:
                        list.Add(new PendingAudit { Entry = e, Action = "Xóa", Details = Summary(e, current: false), KeyBefore = KeyOf(e) });
                        break;
                    case EntityState.Modified:
                        if (e.Entity is Notification) continue; // đánh dấu đã đọc: không cần lưu vết
                        var changes = e.Properties
                            .Where(p => p.IsModified && !IgnoredProps.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue))
                            .Select(p => p.Metadata.Name == nameof(User.PasswordHash)
                                ? "Mật khẩu: (đã thay đổi)"
                                : $"{p.Metadata.Name}: {Fmt(p.OriginalValue)} → {Fmt(p.CurrentValue)}")
                            .ToList();
                        if (changes.Count == 0) continue;
                        list.Add(new PendingAudit { Entry = e, Action = "Cập nhật", Details = Summary(e, current: true) + " | " + string.Join("; ", changes) });
                        break;
                }
            }
            return list;
        }

        private static string KeyOf(EntityEntry e) =>
            string.Join(",", e.Metadata.FindPrimaryKey()!.Properties.Select(p => e.Property(p.Name).CurrentValue?.ToString() ?? ""));

        /// <summary>Mô tả ngắn bản ghi: dùng các trường "tên" phổ biến nếu có.</summary>
        private static string Summary(EntityEntry e, bool current)
        {
            foreach (var name in new[] { "Username", "FullName", "Name", "Title", "Subject", "InvoiceNo" })
            {
                var prop = e.Metadata.FindProperty(name);
                if (prop == null) continue;
                var v = current ? e.Property(name).CurrentValue : e.Property(name).OriginalValue;
                if (v is string s && s.Length > 0) return s;
            }
            var ids = e.Metadata.GetProperties().Where(p => p.IsForeignKey())
                .Select(p => $"{p.Name}={(current ? e.Property(p.Name).CurrentValue : e.Property(p.Name).OriginalValue)}");
            return string.Join(", ", ids);
        }

        private static string Fmt(object? v) => v switch
        {
            null => "(trống)",
            DateTime d => d.ToString("dd/MM/yyyy HH:mm"),
            string s when s.Length > 60 => s[..57] + "...",
            _ => v.ToString() ?? ""
        };

        #endregion
    }
}
