using SportCenter.Domain.Models.Members;

namespace SportCenter.DAL.Interfaces;

/// <summary>Read contracts for the current member. SQL/EF implementations must scope every query by memberId.</summary>
public interface IMemberReadRepository
{
    Task<MemberProfile?> GetProfileAsync(int memberId, CancellationToken cancellationToken = default);
    Task<MemberMembership?> GetMembershipAsync(int memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberAttendance>> GetAttendanceAsync(int memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberProgress>> GetProgressAsync(int memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
