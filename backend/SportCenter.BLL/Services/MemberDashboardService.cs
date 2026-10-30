using SportCenter.BLL.DTOs.Members;
using SportCenter.BLL.Interfaces;
using SportCenter.DAL.Interfaces;
using SportCenter.Domain.Models.Members;

namespace SportCenter.BLL.Services;

public sealed class MemberDashboardService(IMemberReadRepository repository) : IMemberDashboardService
{
    private readonly IMemberReadRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<MemberDashboardData> GetDashboardAsync(int memberId, CancellationToken cancellationToken = default)
    {
        ValidateMemberId(memberId);
        var profile = await _repository.GetProfileAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ học viên.");
        // Sequential calls also support a future scoped EF DbContext, which is not thread safe.
        var membership = await _repository.GetMembershipAsync(memberId, cancellationToken);
        var attendance = await _repository.GetAttendanceAsync(memberId, cancellationToken);
        var progress = await _repository.GetProgressAsync(memberId, cancellationToken);

        if (profile.Id != memberId || (membership is not null && membership.MemberId != memberId)
            || attendance.Any(item => item.MemberId != memberId) || progress.Any(item => item.MemberId != memberId))
        {
            throw new InvalidOperationException("Dữ liệu trả về không thuộc học viên hiện tại.");
        }

        return new(profile, membership, attendance.OrderByDescending(item => item.StartsAt).ToArray(),
            progress.OrderByDescending(item => item.RecordedOn).ToArray());
    }

    public async Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ValidateMemberId(memberId);
        if (from > to)
        {
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        }

        _ = await _repository.GetProfileAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ học viên.");
        var sessions = await _repository.GetScheduleAsync(memberId, from, to, cancellationToken);
        return sessions.Where(item => DateOnly.FromDateTime(item.StartsAt.DateTime) >= from
                && DateOnly.FromDateTime(item.StartsAt.DateTime) <= to)
            .OrderBy(item => item.StartsAt).ToArray();
    }

    private static void ValidateMemberId(int memberId)
    {
        if (memberId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memberId), "Mã học viên không hợp lệ.");
        }
    }
}
