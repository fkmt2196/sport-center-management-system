using SportsCenter.BLL;
using System.Windows;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;

namespace Project.Frontend.Views.Coaching
{
    public static partial class CoachPages
    {
        /// <summary>Ghi kết quả buổi tập: chọn học viên của buổi rồi mở form kết quả.</summary>
        public static async Task<bool> LogForSessionAsync(SessionRow session)
        {
            if (Me.CoachId is not int coachId) return false;
            var roster = await ServiceScope.Run(s => s.Attendance.RosterAsync(session.Id));
            if (roster.Count == 0) { UiHost.Toast("Buổi tập chưa có học viên.", ToastKind.Info); return false; }
            var first = roster.FirstOrDefault(r => r.Status is SportsCenter.DAL.Models.AttendanceStatus.Present or SportsCenter.DAL.Models.AttendanceStatus.Late) ?? roster[0];
            return await TrainingForms.LogAsync(coachId, null, first.MemberId, session.Id, session.Start.Date);
        }
    }
}
