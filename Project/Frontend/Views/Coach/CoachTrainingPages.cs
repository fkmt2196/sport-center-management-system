using SportsCenter.BLL;
using System.Windows;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using Project.Frontend.Views.Shared;
using SportsCenter.BLL.AI;
using SportsCenter.DAL.Models;

namespace Project.Frontend.Views.Coaching
{
    // ------------------------------------------------------------------ Dòng hiển thị (dùng chung HLV & thành viên)
    public class PlanRow
    {
        public required TrainingPlan Entity { get; init; }
        public int Id => Entity.Id;
        public string Title => Entity.Title;
        public string Goal => Entity.Goal;
        public string Target => Entity.Member != null ? $"Cá nhân: {Entity.Member.User?.FullName}" : $"Cả lớp: {Entity.Class?.Name}";
        public string Scope => Entity.MemberId != null ? "Cá nhân" : "Cả lớp";
        public string CoachName => Entity.Coach?.User?.FullName ?? "";
        public DateTime StartDate => Entity.StartDate;
        public string PeriodText => Entity.EndDate.HasValue ? $"{Entity.StartDate:dd/MM/yyyy} – {Entity.EndDate:dd/MM/yyyy}" : $"Từ {Entity.StartDate:dd/MM/yyyy}";
        public string Content => Entity.Content;
    }

    public class LogRow
    {
        public required WorkoutLog Entity { get; init; }
        public int Id => Entity.Id;
        public DateTime Date => Entity.Date;
        public string DateText => Entity.Date.ToString("dd/MM/yyyy");
        public string MemberName => Entity.Member?.User?.FullName ?? "";
        public string Context => Entity.Session?.Class?.Name ?? "Tập tự do";
        public string CoachName => Entity.Coach?.User?.FullName ?? "";
        public string Exercises => Entity.Exercises;
        public string Duration => Entity.DurationMinutes is int m ? $"{m} phút" : "—";
        public string Weight => Entity.WeightKg is double w ? $"{w:0.#} kg" : "—";
        public string Notes => Entity.Notes;
    }

    public class ReviewRow
    {
        public required ProgressReview Entity { get; init; }
        public int Id => Entity.Id;
        public DateTime Date => Entity.Date;
        public string DateText => Entity.Date.ToString("dd/MM/yyyy");
        public string MemberName => Entity.Member?.User?.FullName ?? "";
        public string CoachName => Entity.Coach?.User?.FullName ?? "";
        public int Score => Entity.Score;
        public string ScoreText => $"{Entity.Score}/10";
        public string Comment => Entity.Comment;
    }

    public class MyMemberRow
    {
        public required Member Entity { get; init; }
        public int Id => Entity.Id;
        public string FullName => Entity.User!.FullName;
        public string Phone => Entity.User!.Phone;
        public string Classes { get; init; } = "";
        public string Level => Entity.FitnessLevel.ToString();
        public string Goals => Entity.Goals;
        public string Health => string.IsNullOrWhiteSpace(Entity.HealthNotes) ? "Không có" : Entity.HealthNotes;
        public string Body => $"{Entity.HeightCm?.ToString("0") ?? "?"} cm · {Entity.WeightKg?.ToString("0.#") ?? "?"} kg";
    }

    public static partial class CoachPages
    {
        private static int CoachId => Me.CoachId ?? throw new InvalidOperationException("Tài khoản chưa có hồ sơ HLV.");

        // ------------------------------------------------------------------ HỌC VIÊN
        public static FrameworkElement Members() => new TablePage<MyMemberRow>(new TableSpec<MyMemberRow>
        {
            Title = "Học viên",
            Subtitle = "Học viên đang học các lớp bạn phụ trách",
            Unit = "học viên",
            EmptyIcon = Icons.People,
            Load = async () => (await ServiceScope.Run(s => s.Coaches.MyMembersAsync(CoachId)))
                .Select(x => new MyMemberRow { Entity = x.Member, Classes = string.Join(", ", x.Classes) }).ToList(),
            Search = r => $"{r.FullName} {r.Classes} {r.Goals}",
            SearchPlaceholder = "Tìm theo tên, lớp, mục tiêu...",
            DefaultSort = nameof(MyMemberRow.FullName),
            Columns =
            {
                new Col("Học viên", nameof(MyMemberRow.FullName), ColKind.Avatar) { Path2 = nameof(MyMemberRow.Classes), Width = Col.Star(1.7), MinWidth = 210 },
                new Col("Trình độ", nameof(MyMemberRow.Level), ColKind.Badge) { BadgeKind = "level", HideBelow = 900 },
                new Col("Mục tiêu", nameof(MyMemberRow.Goals), ColKind.TwoLine) { Path2 = nameof(MyMemberRow.Body), Width = Col.Star(2.2), MinWidth = 200, HideBelow = 640 },
                new Col("Lưu ý sức khỏe", nameof(MyMemberRow.Health)) { Width = Col.Star(1.3), MinWidth = 140, HideBelow = 1180 },
            },
            Filters =
            {
                new Filter<MyMemberRow>("Mọi lớp", (r, v) => r.Classes.Contains((string)v!)) { OptionsFrom = rows => rows.SelectMany(r => r.Classes.Split(", ")).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 },
                new Filter<MyMemberRow>("Mọi trình độ", (r, v) => r.Level == v!.ToString()) { Options = Rules.Enum<FitnessLevel>("level"), Width = 150 },
            },
            RowActions =
            {
                new RowAction<MyMemberRow>("", "Hồ sơ & tiến độ", r => MemberProgressAsync(r.Id, r.FullName)),
                new RowAction<MyMemberRow>("", "AI gợi ý bài tập", r => AiSuggestAsync(r.Id)),
                new RowAction<MyMemberRow>(Icons.Edit, "Tạo kế hoạch tập", r => TrainingForms.PlanAsync(CoachId, null, r.Id, goal: r.Goals)),
                new RowAction<MyMemberRow>(Icons.Checklist, "Ghi kết quả tập", r => TrainingForms.LogAsync(CoachId, null, r.Id)),
                new RowAction<MyMemberRow>(Icons.Success, "Đánh giá tiến độ", r => TrainingForms.ReviewAsync(CoachId, null, r.Id)),
                new RowAction<MyMemberRow>("", "Gửi bài tập / thông báo", r => TrainingForms.SendNoticeAsync(CoachId, presetMemberId: r.Id)),
            },
            Open = r => MemberProgressAsync(r.Id, r.FullName),
        });

        /// <summary>Hồ sơ, mục tiêu, kết quả tập gần đây và nhận xét của một học viên.</summary>
        public static async Task<bool> MemberProgressAsync(int memberId, string name)
        {
            var (m, logs, reviews, att) = await ServiceScope.Run(async s => (await s.Members.GetAsync(memberId), await s.Training.LogsAsync(memberId), await s.Training.ReviewsAsync(memberId), await s.Attendance.MemberHistoryAsync(memberId)));
            if (m == null) return false;
            var since = DateTime.Today.AddDays(-30);
            var recent = att.Where(a => a.Session!.StartTime >= since).ToList();
            var present = recent.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            await InfoDialog.ShowAsync(name, $"{Labels.Gender(m.Gender)} {Labels.Age(m.DateOfBirth)} · {Labels.Level(m.FitnessLevel)}", Icons.People, new[]
            {
                new InfoSection("Thông tin", new List<(string, string)>
                {
                    ("Điện thoại", m.User!.Phone), ("Chiều cao / cân nặng", $"{m.HeightCm?.ToString("0") ?? "?"} cm / {m.WeightKg?.ToString("0.#") ?? "?"} kg"),
                    ("Chuyên cần 30 ngày", recent.Count == 0 ? "Chưa có buổi" : $"{present}/{recent.Count} buổi ({100.0 * present / recent.Count:0}%)"),
                    ("Lớp đang học", string.Join(", ", m.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).Select(e => e.Class!.Name))),
                }),
                new InfoSection("Mục tiêu tập luyện", Body: m.Goals),
                new InfoSection("Lưu ý sức khỏe", Body: m.HealthNotes),
                new InfoSection("Kết quả tập gần đây", logs.Take(5).Select(l => (l.Date.ToString("dd/MM/yyyy"), $"{l.Exercises}{(l.WeightKg.HasValue ? $" · {l.WeightKg:0.#} kg" : "")}{(l.Notes.Length > 0 ? $" · {l.Notes}" : "")}"))
                    .DefaultIfEmpty(("—", "Chưa có kết quả")).ToList()),
                new InfoSection("Đánh giá tiến độ", reviews.Take(4).Select(r => ($"{r.Date:dd/MM/yyyy} · {r.Score}/10", r.Comment)).DefaultIfEmpty(("—", "Chưa có đánh giá")).ToList()),
            }, ("AI gợi ý bài tập", "", async () => { await AiSuggestAsync(memberId); return false; }));
            return false;
        }

        /// <summary>Gọi trợ lý AI gợi ý bài tập cho học viên; có thể lưu ngay thành kế hoạch tập.</summary>
        public static async Task<bool> AiSuggestAsync(int memberId)
        {
            var ai = AiAssistantFactory.Create();
            var profile = await ServiceScope.Run(s => s.AiContext.BuildProfileAsync(memberId));
            UiHost.Toast($"Đang tạo gợi ý cho {profile.Name}...", ToastKind.Info);
            var suggestion = await ai.SuggestWorkoutAsync(profile, null);
            var saved = false;
            await InfoDialog.ShowAsync($"Gợi ý bài tập – {profile.Name}", ai.ProviderName, "",
                new[] { new InfoSection($"Mục tiêu: {profile.Goals}", Body: suggestion) },
                ("Lưu thành kế hoạch tập", Icons.Edit, async () =>
                {
                    UiHost.CloseDialog(false);
                    saved = await TrainingForms.PlanAsync(CoachId, null, memberId, suggestion, profile.Goals);
                    return false;
                }));
            return saved;
        }

        // ------------------------------------------------------------------ KẾ HOẠCH TẬP
        public static FrameworkElement Plans() => new TablePage<PlanRow>(new TableSpec<PlanRow>
        {
            Title = "Kế hoạch tập",
            Subtitle = "Kế hoạch cá nhân và kế hoạch chung cho lớp",
            Unit = "kế hoạch",
            EmptyIcon = Icons.Edit,
            Load = async () => (await ServiceScope.Run(s => s.Training.PlansOfCoachAsync(CoachId))).Select(p => new PlanRow { Entity = p }).ToList(),
            Search = r => $"{r.Title} {r.Goal} {r.Target} {r.Content}",
            SearchPlaceholder = "Tìm theo tiêu đề, học viên, lớp, nội dung...",
            DefaultSort = nameof(PlanRow.StartDate),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Kế hoạch", nameof(PlanRow.Title), ColKind.TwoLine) { Path2 = nameof(PlanRow.Goal), Bold = true, Width = Col.Star(2.2), MinWidth = 220 },
                new Col("Áp dụng cho", nameof(PlanRow.Target)) { Width = Col.Star(1.5), MinWidth = 160, HideBelow = 700 },
                new Col("Thời gian", nameof(PlanRow.PeriodText)) { SortPath = nameof(PlanRow.StartDate), Width = Col.Px(200), HideBelow = 900 },
            },
            Filters = { new Filter<PlanRow>("Mọi loại", (r, v) => r.Scope == (string)v!) { Options = new[] { ("Cá nhân", (object)"Cá nhân"), ("Cả lớp", "Cả lớp") }, Width = 140 } },
            Actions = { new("Tạo kế hoạch", Icons.Add, () => TrainingForms.PlanAsync(CoachId, null)) },
            RowActions =
            {
                new RowAction<PlanRow>("", "Xem", ShowPlanAsync),
                new RowAction<PlanRow>(Icons.Edit, "Sửa", r => TrainingForms.PlanAsync(CoachId, r.Entity)),
                new RowAction<PlanRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa kế hoạch?", $"Xóa kế hoạch \"{r.Title}\"?")) return false;
                    await ServiceScope.Run(s => s.Training.DeletePlanAsync(r.Id));
                    UiHost.Toast("Đã xóa kế hoạch.");
                    return true;
                }) { Danger = true },
            },
            Open = ShowPlanAsync,
        });

        public static async Task<bool> ShowPlanAsync(PlanRow r)
        {
            await InfoDialog.ShowAsync(r.Title, $"{r.Target} · {r.PeriodText} · HLV {r.CoachName}", Icons.Edit, new[]
            {
                new InfoSection("Mục tiêu", Body: r.Goal),
                new InfoSection("Nội dung kế hoạch", Body: r.Content),
            });
            return false;
        }

        // ------------------------------------------------------------------ KẾT QUẢ TẬP
        public static FrameworkElement Logs() => new TablePage<LogRow>(new TableSpec<LogRow>
        {
            Title = "Kết quả tập",
            Subtitle = "Kết quả ghi sau mỗi buổi tập",
            Unit = "kết quả",
            EmptyIcon = Icons.Checklist,
            Load = async () => (await ServiceScope.Run(s => s.Training.LogsAsync(coachId: CoachId))).Select(l => new LogRow { Entity = l }).ToList(),
            Search = r => $"{r.MemberName} {r.Exercises} {r.Notes} {r.Context}",
            SearchPlaceholder = "Tìm theo học viên, bài tập, ghi chú...",
            DefaultSort = nameof(LogRow.Date),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Ngày", nameof(LogRow.DateText), ColKind.TwoLine) { Path2 = nameof(LogRow.Context), SortPath = nameof(LogRow.Date), Bold = true, Width = Col.Px(150) },
                new Col("Học viên", nameof(LogRow.MemberName), ColKind.Avatar) { Width = Col.Star(1.3), MinWidth = 180 },
                new Col("Bài tập & kết quả", nameof(LogRow.Exercises), ColKind.TwoLine) { Path2 = nameof(LogRow.Notes), Width = Col.Star(2.5), MinWidth = 220, HideBelow = 700 },
                new Col("Thời lượng", nameof(LogRow.Duration)) { Width = Col.Px(100), HideBelow = 1000 },
                new Col("Cân nặng", nameof(LogRow.Weight)) { Width = Col.Px(100), HideBelow = 900 },
            },
            Filters = { new Filter<LogRow>("Mọi học viên", (r, v) => r.MemberName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.MemberName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 } },
            Actions = { new("Ghi kết quả", Icons.Add, () => TrainingForms.LogAsync(CoachId, null)) },
            RowActions =
            {
                new RowAction<LogRow>(Icons.Edit, "Sửa", r => TrainingForms.LogAsync(CoachId, r.Entity)),
                new RowAction<LogRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa kết quả?", $"Xóa kết quả buổi tập ngày {r.DateText} của {r.MemberName}?")) return false;
                    await ServiceScope.Run(s => s.Training.DeleteLogAsync(r.Id));
                    UiHost.Toast("Đã xóa kết quả.");
                    return true;
                }) { Danger = true },
            },
            Open = r => TrainingForms.LogAsync(CoachId, r.Entity),
        });

        // ------------------------------------------------------------------ ĐÁNH GIÁ TIẾN ĐỘ
        public static FrameworkElement Reviews() => new TablePage<ReviewRow>(new TableSpec<ReviewRow>
        {
            Title = "Đánh giá tiến độ",
            Subtitle = "Điểm tiến độ (1–10) và nhận xét cho học viên",
            Unit = "đánh giá",
            EmptyIcon = Icons.Success,
            Load = async () => (await ServiceScope.Run(s => s.Training.ReviewsAsync(coachId: CoachId))).Select(r => new ReviewRow { Entity = r }).ToList(),
            Search = r => $"{r.MemberName} {r.Comment}",
            DefaultSort = nameof(ReviewRow.Date),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Ngày", nameof(ReviewRow.DateText)) { SortPath = nameof(ReviewRow.Date), Width = Col.Px(120), Bold = true },
                new Col("Học viên", nameof(ReviewRow.MemberName), ColKind.Avatar) { Width = Col.Star(1.3), MinWidth = 180 },
                new Col("Điểm", nameof(ReviewRow.ScoreText)) { SortPath = nameof(ReviewRow.Score), Width = Col.Px(80), Bold = true },
                new Col("Nhận xét", nameof(ReviewRow.Comment)) { Width = Col.Star(3), MinWidth = 220, HideBelow = 680 },
            },
            Filters = { new Filter<ReviewRow>("Mọi học viên", (r, v) => r.MemberName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.MemberName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 } },
            Actions = { new("Đánh giá mới", Icons.Add, () => TrainingForms.ReviewAsync(CoachId, null)) },
            RowActions =
            {
                new RowAction<ReviewRow>(Icons.Edit, "Sửa", r => TrainingForms.ReviewAsync(CoachId, r.Entity)),
                new RowAction<ReviewRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa đánh giá?", $"Xóa đánh giá ngày {r.DateText} của {r.MemberName}?")) return false;
                    await ServiceScope.Run(s => s.Training.DeleteReviewAsync(r.Id));
                    UiHost.Toast("Đã xóa đánh giá.");
                    return true;
                }) { Danger = true },
            },
            Open = r => TrainingForms.ReviewAsync(CoachId, r.Entity),
        });
    }
}
