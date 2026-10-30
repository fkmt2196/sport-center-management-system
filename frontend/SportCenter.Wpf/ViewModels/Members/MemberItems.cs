using System.Globalization;
using SportCenter.Domain.Models.Members;

namespace SportCenter.Wpf.ViewModels.Members;

public sealed class ClassCardViewModel(MemberClassSession session, DateTimeOffset now)
{
    public MemberClassSession Session { get; } = session;
    public string DayLabel => Session.StartsAt.ToString("dddd, dd/MM", CultureInfo.GetCultureInfo("vi-VN"));
    public string TimeLabel => $"{Session.StartsAt:HH:mm} – {Session.EndsAt:HH:mm}";
    public int AvailableSlots => Math.Max(0, Session.Capacity - Session.BookedCount);
    public string CapacityLabel => $"{AvailableSlots}/{Session.Capacity} chỗ còn trống";
    public bool IsPast => Session.EndsAt <= now;
    public string StatusLabel => IsPast ? "Đã kết thúc" : Session.IsRegistered ? "Đã đăng ký"
        : AvailableSlots == 0 ? "Đã đủ chỗ" : "Còn chỗ";
}

public sealed class AttendanceItemViewModel(MemberAttendance record)
{
    public MemberAttendance Record { get; } = record;
    public string DateLabel => Record.StartsAt.ToString("dd/MM/yyyy HH:mm");
    public string DurationLabel => $"{Record.DurationMinutes} phút";
    public string StatusLabel => Record.Status switch
    {
        AttendanceStatus.Present => "Có mặt",
        AttendanceStatus.Absent => "Vắng mặt",
        _ => "Có phép"
    };
}

public sealed class ProgressItemViewModel(MemberProgress record)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public MemberProgress Record { get; } = record;
    public double CompletionPercent => Record.Target == Record.Baseline
        ? (Record.Current == Record.Target ? 100 : 0)
        : (double)Math.Clamp((Record.Current - Record.Baseline) / (Record.Target - Record.Baseline) * 100, 0, 100);
    public string CurrentLabel => $"{Record.Current.ToString("0.#", Vietnamese)} {Record.Unit}";
    public string GoalLabel => $"Mục tiêu: {Record.Target.ToString("0.#", Vietnamese)} {Record.Unit}";
    public string BaselineLabel => $"Ban đầu: {Record.Baseline.ToString("0.#", Vietnamese)} {Record.Unit}";
    public string UpdatedLabel => $"Cập nhật {Record.RecordedOn:dd/MM/yyyy}";
    public string PercentLabel => $"{CompletionPercent:0}% mục tiêu";
}
