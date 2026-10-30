namespace SportCenter.Domain.Models.Members;

public sealed record MemberProfile(int Id, string FullName, string Email, string Phone, DateOnly JoinedOn);

public sealed record MemberMembership(
    int MemberId, string PlanName, DateOnly StartsOn, DateOnly EndsOn,
    int TotalSessions, int UsedSessions, string Description);

public sealed record CoachSummary(int Id, string FullName);

public sealed record RoomSummary(int Id, string Name);

public sealed record MemberClassSession(
    int Id, string Name, string Sport, CoachSummary Coach, RoomSummary Room,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, int BookedCount,
    bool IsRegistered);

public enum AttendanceStatus
{
    Present,
    Absent,
    Excused
}

public sealed record MemberAttendance(
    int MemberId, string ClassName, string CoachName, DateTimeOffset StartsAt,
    int DurationMinutes, AttendanceStatus Status, string CoachComment);

public sealed record MemberProgress(
    int MemberId, string MetricName, string Unit, decimal Baseline, decimal Current,
    decimal Target, DateOnly RecordedOn, string CoachComment);
