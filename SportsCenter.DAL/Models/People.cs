namespace SportsCenter.DAL.Models
{
    /// <summary>Tài khoản đăng nhập (mọi vai trò).</summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        /// <summary>Băm PBKDF2 kèm salt (xem PasswordHasher).</summary>
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }

        public Member? Member { get; set; }
        public Coach? Coach { get; set; }
    }

    /// <summary>Hội viên của trung tâm.</summary>
    public class Member
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public Gender Gender { get; set; } = Gender.Other;
        public DateTime? DateOfBirth { get; set; }
        public double? HeightCm { get; set; }
        public double? WeightKg { get; set; }
        public FitnessLevel FitnessLevel { get; set; } = FitnessLevel.Beginner;
        /// <summary>Mục tiêu tập luyện (giảm cân, tăng cơ, sức bền...).</summary>
        public string Goals { get; set; } = string.Empty;
        /// <summary>Lưu ý sức khỏe/chấn thương.</summary>
        public string HealthNotes { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; } = DateTime.Today;

        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }

    /// <summary>Huấn luyện viên.</summary>
    public class Coach
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        /// <summary>Chuyên môn chính (Gym, Yoga, Boxing...).</summary>
        public string Specialty { get; set; } = string.Empty;
        public string Certifications { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public int YearsExperience { get; set; }
        public DateTime HireDate { get; set; } = DateTime.Today;
        public bool IsAvailable { get; set; } = true;

        public ICollection<TrainingClass> Classes { get; set; } = new List<TrainingClass>();
    }
}
