using SportCenter.BLL.DTOs.Members;
using SportCenter.Domain.Models.Members;

namespace SportCenter.BLL.Interfaces;

public interface IMemberDashboardService
{
    Task<MemberDashboardData> GetDashboardAsync(int memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
