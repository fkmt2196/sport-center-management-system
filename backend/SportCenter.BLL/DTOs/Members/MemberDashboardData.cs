using SportCenter.Domain.Models.Members;

namespace SportCenter.BLL.DTOs.Members;

public sealed record MemberDashboardData(
    MemberProfile Profile,
    MemberMembership? Membership,
    IReadOnlyList<MemberAttendance> Attendance,
    IReadOnlyList<MemberProgress> Progress);
