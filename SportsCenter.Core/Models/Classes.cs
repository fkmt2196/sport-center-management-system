namespace SportsCenter.Core.Models
{
    /// <summary>Bộ môn (Gym, Yoga, Boxing, Bơi lội...).</summary>
    public class Sport
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public ICollection<TrainingClass> Classes { get; set; } = new List<TrainingClass>();
    }

    /// <summary>Phòng tập / khu vực tập.</summary>
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Lớp tập theo nhóm (vd. Yoga cơ bản tối 2-4-6).</summary>
    public class TrainingClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SportId { get; set; }
        public Sport? Sport { get; set; }
        /// <summary>Huấn luyện viên được phân công (có thể chưa phân công).</summary>
        public int? CoachId { get; set; }
        public Coach? Coach { get; set; }
        public FitnessLevel Level { get; set; } = FitnessLevel.Beginner;
        public string Description { get; set; } = string.Empty;
        public int Capacity { get; set; }
        /// <summary>Học phí riêng của lớp (VNĐ); 0 = đã bao gồm trong gói thành viên.</summary>
        public decimal Fee { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ClassStatus Status { get; set; } = ClassStatus.Open;

        public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }

    /// <summary>Ca học / buổi tập cụ thể của một lớp.</summary>
    public class ClassSession
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public TrainingClass? Class { get; set; }
        public int RoomId { get; set; }
        public Room? Room { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
        public string Note { get; set; } = string.Empty;

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }

    /// <summary>Thành viên đăng ký vào lớp.</summary>
    public class Enrollment
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public int ClassId { get; set; }
        public TrainingClass? Class { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.Now;
        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
        public DateTime? CancelledAt { get; set; }
    }

    /// <summary>Điểm danh thành viên trong một ca học.</summary>
    public class Attendance
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public ClassSession? Session { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
        public DateTime? CheckInTime { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>Lượt check-in tại quầy khi thành viên đến trung tâm.</summary>
    public class CheckIn
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member? Member { get; set; }
        public DateTime CheckInTime { get; set; } = DateTime.Now;
        public int? ByUserId { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
