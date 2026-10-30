using SportCenter.DAL.Interfaces;
using SportCenter.Domain.Models.Members;

namespace SportCenter.DAL.Repositories;

/// <summary>Deterministic, read-only sample data. Replace at the composition root when SQL Server is ready.</summary>
public sealed class DemoMemberReadRepository : IMemberReadRepository
{
    private readonly DateOnly _today;
    private readonly TimeSpan _offset;

    public DemoMemberReadRepository(TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetLocalNow();
        _today = DateOnly.FromDateTime(now.DateTime);
        _offset = now.Offset;
    }

    public Task<MemberProfile?> GetProfileAsync(int memberId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MemberProfile? profile = memberId switch
        {
            1 => new(1, "Phạm Tùng Dương", "duong@example.com", "0900 000 001", _today.AddMonths(-3)),
            2 => new(2, "Học viên mẫu 02", "member02@example.com", "0900 000 002", _today.AddMonths(-1)),
            _ => null
        };
        return Task.FromResult(profile);
    }

    public Task<MemberMembership?> GetMembershipAsync(int memberId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MemberMembership? membership = memberId switch
        {
            1 => new(1, "Active Plus", _today.AddDays(-20), _today.AddDays(40), 24, 8,
                "Tập luyện đa bộ môn · tối đa 24 buổi trong kỳ"),
            2 => new(2, "Starter", _today.AddDays(-40), _today.AddDays(-10), 8, 8,
                "Gói trải nghiệm 8 buổi"),
            _ => null
        };
        return Task.FromResult(membership);
    }

    public Task<IReadOnlyList<MemberAttendance>> GetAttendanceAsync(int memberId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MemberAttendance> records = memberId == 1
            ? new MemberAttendance[]
            {
                new(1, "Yoga phục hồi", "Nguyễn Minh Anh", At(_today.AddDays(-1), 7), 60,
                    AttendanceStatus.Present, "Giữ nhịp thở tốt, tiếp tục cải thiện độ dẻo."),
                new(1, "Strength Foundation", "Trần Quốc Bảo", At(_today.AddDays(-3), 18), 60,
                    AttendanceStatus.Present, "Tư thế squat ổn định hơn so với buổi trước."),
                new(1, "Cardio Flow", "Lê Khánh Linh", At(_today.AddDays(-5), 17), 45,
                    AttendanceStatus.Absent, "Vắng buổi tập."),
                new(1, "Yoga buổi sáng", "Nguyễn Minh Anh", At(_today.AddDays(-7), 7), 60,
                    AttendanceStatus.Present, "Hoàn thành đầy đủ bài tập."),
                new(1, "Functional Training", "Trần Quốc Bảo", At(_today.AddDays(-9), 18), 60,
                    AttendanceStatus.Excused, "Nghỉ có phép."),
                new(1, "Cardio Flow", "Lê Khánh Linh", At(_today.AddDays(-12), 17), 45,
                    AttendanceStatus.Present, "Sức bền tăng, giữ nhịp độ tốt."),
                new(1, "Strength Foundation", "Trần Quốc Bảo", At(_today.AddDays(-14), 18), 60,
                    AttendanceStatus.Present, "Hoàn thành mục tiêu buổi tập."),
                new(1, "Yoga phục hồi", "Nguyễn Minh Anh", At(_today.AddDays(-16), 7), 60,
                    AttendanceStatus.Present, "Duy trì tập đều đặn.")
            }
            : Array.Empty<MemberAttendance>();
        return Task.FromResult(records);
    }

    public Task<IReadOnlyList<MemberProgress>> GetProgressAsync(int memberId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MemberProgress> records = memberId == 1
            ? new MemberProgress[]
            {
                new(1, "Chạy liên tục", "phút", 10, 22, 30, _today.AddDays(-1), "Tăng dần 2 phút mỗi tuần, giữ nhịp thở ổn định."),
                new(1, "Giữ plank", "giây", 30, 90, 120, _today.AddDays(-3), "Giữ lưng thẳng và siết cơ bụng khi tập."),
                new(1, "Cân nặng", "kg", 71, 69.2m, 68, _today.AddDays(-3), "Duy trì chế độ ăn và phục hồi theo hướng dẫn của Coach.")
            }
            : Array.Empty<MemberProgress>();
        return Task.FromResult(records);
    }

    public Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (memberId is not (1 or 2))
        {
            return Task.FromResult<IReadOnlyList<MemberClassSession>>(Array.Empty<MemberClassSession>());
        }

        var monday = _today.AddDays(-(((int)_today.DayOfWeek + 6) % 7));
        var yogaCoach = new CoachSummary(10, "Nguyễn Minh Anh");
        var strengthCoach = new CoachSummary(11, "Trần Quốc Bảo");
        var cardioCoach = new CoachSummary(12, "Lê Khánh Linh");
        var studio = new RoomSummary(1, "Studio A");
        var gym = new RoomSummary(2, "Gym B");
        var court = new RoomSummary(3, "Sân C");
        var sessions = new List<MemberClassSession>();
        // Fixed three-week sample window: navigating further deliberately shows an empty state.
        for (var week = -1; week <= 1; week++)
        {
            var start = monday.AddDays(week * 7);
            var id = (week + 2) * 100;
            sessions.Add(new(id + 1, "Yoga buổi sáng", "Yoga", yogaCoach, studio,
                At(start, 7), At(start, 8), 20, 12, memberId == 1));
            sessions.Add(new(id + 2, "Strength Foundation", "Gym", strengthCoach, gym,
                At(start.AddDays(1), 18), At(start.AddDays(1), 19), 16, 10, false));
            sessions.Add(new(id + 3, "Cardio Flow", "Cardio", cardioCoach, gym,
                At(start.AddDays(2), 17), At(start.AddDays(2), 18), 18, 18, false));
            sessions.Add(new(id + 4, "Yoga phục hồi", "Yoga", yogaCoach, studio,
                At(start.AddDays(3), 7), At(start.AddDays(3), 8), 20, 14, memberId == 1));
            sessions.Add(new(id + 5, "Functional Training", "Gym", strengthCoach, gym,
                At(start.AddDays(4), 18), At(start.AddDays(4), 19), 12, 11, false));
            sessions.Add(new(id + 6, "Cầu lông cơ bản", "Cầu lông", cardioCoach, court,
                At(start.AddDays(5), 9), At(start.AddDays(5), 10), 12, 8, memberId == 2));
        }

        IReadOnlyList<MemberClassSession> result = sessions
            .Where(session => DateOnly.FromDateTime(session.StartsAt.DateTime) >= from
                && DateOnly.FromDateTime(session.StartsAt.DateTime) <= to)
            .OrderBy(session => session.StartsAt).ToArray();
        return Task.FromResult(result);
    }

    private DateTimeOffset At(DateOnly date, int hour) => new(date.ToDateTime(new TimeOnly(hour, 0)), _offset);
}
