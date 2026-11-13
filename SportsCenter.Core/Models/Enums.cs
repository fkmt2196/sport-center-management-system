namespace SportsCenter.Core.Models
{
    // Lưu trong CSDL dưới dạng chuỗi (xem SportsCenterContext.ConfigureConventions).

    public enum UserRole { Manager, Coach, Member, Receptionist }

    public enum Gender { Male, Female, Other }

    public enum FitnessLevel { Beginner, Intermediate, Advanced }

    public enum ClassStatus { Open, Completed, Cancelled }

    public enum SessionStatus { Scheduled, Completed, Cancelled }

    public enum EnrollmentStatus { Active, Cancelled }

    public enum AttendanceStatus { Present, Late, Absent, Excused }

    public enum SubscriptionStatus { Active, Cancelled }

    public enum PaymentMethod { Cash, Card, Transfer, Online }

    public enum PaymentPurpose { Subscription, ClassFee, Other }

    public enum NotificationType { General, Homework, ScheduleChange, Expiry }

    public enum SupportStatus { Open, InProgress, Resolved }
}
