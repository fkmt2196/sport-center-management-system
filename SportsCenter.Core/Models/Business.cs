namespace SportsCenter.Core.Models
{
    // ===================== GÓI THÀNH VIÊN & THANH TOÁN =====================

    /// <summary>Gói thành viên (vd. Gói 3 tháng).</summary>
    public class MembershipPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        /// <summary>Gói có bao gồm các lớp nhóm (lớp có học phí = 0).</summary>
        public bool IncludesGroupClasses { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Một lần đăng ký / gia hạn gói của thành viên.</summary>
    public class Subscription
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public int PlanId { get; set; }
        public MembershipPlan? Plan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PricePaid { get; set; }
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Note { get; set; } = string.Empty;

        /// <summary>Còn hiệu lực vào ngày chỉ định.</summary>
        public bool IsValidOn(DateTime date) => Status == SubscriptionStatus.Active && StartDate.Date <= date.Date && EndDate.Date >= date.Date;
    }

    /// <summary>Phiếu thu / hóa đơn.</summary>
    public class Payment
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Subscription;
        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }
        public int? EnrollmentId { get; set; }
        public Enrollment? Enrollment { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.Now;
        public int? ReceivedByUserId { get; set; }
        public User? ReceivedBy { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    // ===================== TẬP LUYỆN =====================

    /// <summary>Kế hoạch tập: cho một thành viên (MemberId) hoặc cả lớp (ClassId).</summary>
    public class TrainingPlan
    {
        public int Id { get; set; }
        public int CoachId { get; set; }
        public Coach? Coach { get; set; }
        public int? MemberId { get; set; }
        public Member? Member { get; set; }
        public int? ClassId { get; set; }
        public TrainingClass? Class { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        /// <summary>Nội dung chi tiết (bài tập, số hiệp, số lần...).</summary>
        public string Content { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Kết quả tập sau một buổi.</summary>
    public class WorkoutLog
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public int? CoachId { get; set; }
        public Coach? Coach { get; set; }
        public int? SessionId { get; set; }
        public ClassSession? Session { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public string Exercises { get; set; } = string.Empty;
        public int? DurationMinutes { get; set; }
        public double? WeightKg { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>Đánh giá tiến độ và nhận xét của HLV.</summary>
    public class ProgressReview
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public int CoachId { get; set; }
        public Coach? Coach { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        /// <summary>Điểm tiến độ 1–10.</summary>
        public int Score { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    // ===================== GIAO TIẾP & HỆ THỐNG =====================

    /// <summary>Thông báo gửi tới một tài khoản (lịch học, đổi lịch, bài tập, hết hạn gói...).</summary>
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? SenderUserId { get; set; }
        public User? Sender { get; set; }
        public NotificationType Type { get; set; } = NotificationType.General;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
    }

    /// <summary>Yêu cầu hỗ trợ ghi nhận tại quầy.</summary>
    public class SupportRequest
    {
        public int Id { get; set; }
        public int? MemberId { get; set; }
        public Member? Member { get; set; }
        public int CreatedByUserId { get; set; }
        public User? CreatedBy { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SupportStatus Status { get; set; } = SupportStatus.Open;
        public string Response { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>Lịch sử thao tác (ghi tự động khi lưu dữ liệu + các sự kiện đăng nhập).</summary>
    public class AuditLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
