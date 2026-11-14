using SportsCenter.BLL;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using Project.Frontend.Views.Coaching;
using Project.Frontend.Views.Shared;
using SportsCenter.DAL.Models;
using SportsCenter.BLL.Services;

namespace Project.Frontend.Views.MemberArea
{
    public class AttendanceRow
    {
        public required Attendance Entity { get; init; }
        public DateTime Start => Entity.Session!.StartTime;
        public string DateText => $"{Formats.DayOfWeekVi(Start.DayOfWeek)}, {Start:dd/MM/yyyy}";
        public string TimeText => $"{Start:HH:mm} – {Entity.Session!.EndTime:HH:mm}";
        public string ClassName => Entity.Session!.Class?.Name ?? "";
        public string CoachName => Entity.Session!.Class?.Coach?.User?.FullName ?? "";
        public string RoomName => Entity.Session!.Room?.Name ?? "";
        public string Status => Entity.Status.ToString();
        public string Notes => Entity.Notes;
    }

    public class ProgressItem
    {
        public DateTime Date { get; init; }
        public string DateText => Date.ToString("dd/MM/yyyy");
        public string Kind { get; init; } = "";
        public string Title { get; init; } = "";
        public string Details { get; init; } = "";
        public string CoachName { get; init; } = "";
        public string Extra { get; init; } = "";
    }

    public static class MemberPages
    {
        private static int MemberId => Me.MemberId ?? throw new InvalidOperationException("Tài khoản chưa có hồ sơ thành viên.");

        // ------------------------------------------------------------------ LỊCH TẬP CỦA TÔI
        public static FrameworkElement Schedule()
        {
            var today = DateTime.Today;
            return new TablePage<SessionRow>(new TableSpec<SessionRow>
            {
                Title = "Lịch tập của tôi",
                Subtitle = "Các buổi tập của những lớp bạn đã đăng ký",
                Unit = "buổi",
                EmptyIcon = Icons.Calendar,
                EmptyText = "Chưa có buổi tập nào",
                Load = async () => (await ServiceScope.Run(s => s.Classes.SessionsAsync(today.AddDays(-14), today.AddDays(45), memberId: MemberId)))
                    .Select(x => new SessionRow { Entity = x.Session, Enrolled = x.Enrolled, Present = x.Present }).ToList(),
                Search = r => $"{r.ClassName} {r.CoachName} {r.RoomName}",
                SearchPlaceholder = "Tìm theo lớp, huấn luyện viên, phòng...",
                DefaultSort = nameof(SessionRow.Start),
                Columns =
                {
                    new Col("Ngày", nameof(SessionRow.DateText), ColKind.TwoLine) { Path2 = nameof(SessionRow.TimeText), Bold = true, SortPath = nameof(SessionRow.Start), Width = Col.Px(160) },
                    new Col("Lớp", nameof(SessionRow.ClassName), ColKind.TwoLine) { Path2 = nameof(SessionRow.SportName), Width = Col.Star(1.6), MinWidth = 170 },
                    new Col("Huấn luyện viên", nameof(SessionRow.CoachName), ColKind.TwoLine) { Path2 = nameof(SessionRow.CoachPhone), Width = Col.Star(1.3), MinWidth = 150, HideBelow = 760 },
                    new Col("Phòng", nameof(SessionRow.RoomName)) { Width = Col.Px(130), HideBelow = 900 },
                    new Col("Trạng thái", nameof(SessionRow.Status), ColKind.Badge) { BadgeKind = "session" },
                },
                Filters =
                {
                    new Filter<SessionRow>("Tất cả", (r, v) => (string)v! switch
                    {
                        "upcoming" => r.Start >= DateTime.Now.AddHours(-2),
                        "week" => r.Start.Date >= today && r.Start.Date < today.AddDays(7),
                        "past" => r.Start < DateTime.Now,
                        _ => true
                    }) { Options = new[] { ("Sắp tới", (object)"upcoming"), ("7 ngày tới", "week"), ("Đã qua (14 ngày)", "past") }, Default = "upcoming", Width = 160 },
                    new Filter<SessionRow>("Mọi lớp", (r, v) => r.ClassName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.ClassName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 },
                },
                RowActions = { new RowAction<SessionRow>(Icons.Education, "Thông tin huấn luyện viên", CoachInfoAsync) },
                Open = CoachInfoAsync,
            });
        }

        private static async Task<bool> CoachInfoAsync(SessionRow r)
        {
            var c = r.Entity.Class?.Coach;
            await InfoDialog.ShowAsync(r.ClassName, $"{r.DateText} · {r.TimeText} · {r.RoomName}", Icons.Education, new[]
            {
                new InfoSection("Huấn luyện viên", new List<(string, string)>
                {
                    ("Họ tên", c?.User?.FullName ?? "Chưa phân công"), ("Chuyên môn", c?.Specialty ?? ""), ("Kinh nghiệm", c == null ? "" : $"{c.YearsExperience} năm"),
                    ("Chứng chỉ", c?.Certifications ?? ""), ("Liên hệ", $"{c?.User?.Phone} · {c?.User?.Email}"),
                }),
                new InfoSection("Giới thiệu", Body: c?.Bio ?? ""),
                new InfoSection("Ghi chú buổi tập", Body: r.Note),
            });
            return false;
        }

        // ------------------------------------------------------------------ LỚP HỌC (ĐĂNG KÝ / HỦY)
        public static FrameworkElement Classes() => new TablePage<ClassRow>(new TableSpec<ClassRow>
        {
            Title = "Lớp học",
            Subtitle = "Đăng ký lớp cần gói thành viên còn hạn",
            Unit = "lớp",
            EmptyIcon = Icons.Library,
            Load = async () =>
            {
                var (classes, mine) = await ServiceScope.Run(async s => (await s.Classes.ListAsync(), await s.Classes.EnrollmentsOfMemberAsync(MemberId)));
                return classes.Where(c => c.Class.Status == ClassStatus.Open || mine.Any(e => e.ClassId == c.Class.Id && e.Status == EnrollmentStatus.Active))
                    .Select(c =>
                    {
                        var e = mine.FirstOrDefault(x => x.ClassId == c.Class.Id);
                        var state = e?.Status == EnrollmentStatus.Active ? "Active" : c.Enrolled >= c.Class.Capacity ? "Full" : "None";
                        return new ClassRow { Info = c, MyStatus = state, MyEnrollmentId = e?.Id };
                    }).ToList();
            },
            Search = r => $"{r.Name} {r.SportName} {r.CoachName} {r.Description}",
            SearchPlaceholder = "Tìm lớp theo tên, bộ môn, huấn luyện viên...",
            DefaultSort = nameof(ClassRow.Name),
            Columns =
            {
                new Col("Lớp", nameof(ClassRow.Name), ColKind.TwoLine) { Path2 = nameof(ClassRow.Description), Bold = true, Width = Col.Star(2.2), MinWidth = 220 },
                new Col("Bộ môn", nameof(ClassRow.SportName)) { Width = Col.Px(130), HideBelow = 1100 },
                new Col("Huấn luyện viên", nameof(ClassRow.CoachName)) { Width = Col.Star(1.2), MinWidth = 140, HideBelow = 820 },
                new Col("Buổi kế tiếp", nameof(ClassRow.NextText)) { Width = Col.Px(140), HideBelow = 960 },
                new Col("Còn chỗ", nameof(ClassRow.SeatsText), ColKind.Progress) { Path2 = nameof(ClassRow.FillPercent), Width = Col.Px(100), HideBelow = 700 },
                new Col("Học phí", nameof(ClassRow.FeeText)) { SortPath = nameof(ClassRow.Fee), Width = Col.Px(130), HideBelow = 1200 },
                new Col("Của tôi", nameof(ClassRow.MyStatus), ColKind.Badge) { BadgeKind = "enroll" },
            },
            Filters =
            {
                new Filter<ClassRow>("Mọi bộ môn", (r, v) => r.SportName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.SportName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 170 },
                new Filter<ClassRow>("Mọi trình độ", (r, v) => r.Level == v!.ToString()) { Options = Rules.Enum<FitnessLevel>("level"), Width = 150 },
                new Filter<ClassRow>("Tất cả lớp", (r, v) => r.MyStatus == (string)v!) { Options = new[] { ("Lớp của tôi", (object)"Active"), ("Chưa đăng ký", "None") }, Width = 150 },
            },
            RowActions =
            {
                new RowAction<ClassRow>(Icons.Add, "Đăng ký lớp", r => SalesForms.EnrollAsync(MemberId, r.Id, selfService: true))
                    { Visible = r => r.MyStatus == "None" && r.Entity.Status == ClassStatus.Open },
                new RowAction<ClassRow>(Icons.Close, "Hủy đăng ký", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Hủy đăng ký lớp?", $"Bạn sẽ không còn trong danh sách lớp \"{r.Name}\" và không nhận lịch của lớp này.", "Hủy đăng ký")) return false;
                    await ServiceScope.Run(s => s.Classes.CancelEnrollmentAsync(r.MyEnrollmentId!.Value));
                    UiHost.Toast($"Đã hủy đăng ký lớp \"{r.Name}\".");
                    return true;
                }) { Danger = true, Visible = r => r.MyStatus == "Active" },
            },
        });

        // ------------------------------------------------------------------ GÓI THÀNH VIÊN
        public static FrameworkElement Membership() => new MembershipPage();

        private class MembershipPage : UserControl
        {
            private readonly StackPanel _top = new();
            private readonly WrapPanel _plans = new();

            public MembershipPage()
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 360 });
                var top = new StackPanel();
                top.Children.Add(new TextBlock { Text = "Gói thành viên", Style = R<Style>("Text.H1"), Margin = new Thickness(0, 0, 0, 16) });
                top.Children.Add(_top);
                top.Children.Add(new TextBlock { Text = "Chọn gói để đăng ký / gia hạn (thanh toán online)", Style = R<Style>("Text.H3"), Margin = new Thickness(0, 4, 0, 10) });
                top.Children.Add(_plans);
                grid.Children.Add(top);

                var history = OperationsPagesFor.MyPayments();
                history.Margin = new Thickness(0, 16, 0, 0);
                Grid.SetRow(history, 1);
                grid.Children.Add(history);
                Content = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                Loaded += async (_, _) => await LoadAsync();
            }

            private static T R<T>(string k) => (T)Application.Current.FindResource(k);

            private async Task LoadAsync()
            {
                var (member, plans) = await ServiceScope.Run(async s => (await s.Members.GetAsync(MemberId), await s.Memberships.PlansAsync(activeOnly: true)));
                if (member == null) return;
                var (state, end, plan) = Labels.Membership(member);
                _top.Children.Clear();
                var card = new Border { Style = R<Style>("Card"), Margin = new Thickness(0, 0, 0, 16) };
                var dock = new DockPanel();
                var renew = TablePage<object>.IconTextButton(state is "None" or "Expired" ? "Đăng ký gói" : "Gia hạn", Icons.Refresh, "Btn.Primary");
                renew.VerticalAlignment = VerticalAlignment.Center;
                renew.Click += async (_, _) => { if (await SalesForms.SubscribeAsync(MemberId, selfService: true) != null) await RefreshAsync(); };
                DockPanel.SetDock(renew, Dock.Right);
                dock.Children.Add(renew);
                var info = new StackPanel();
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                line.Children.Add(new TextBlock { Text = plan.Length > 0 ? plan : "Chưa có gói", Style = R<Style>("Text.H2"), Margin = new Thickness(0, 0, 12, 0) });
                line.Children.Add(new StatusBadge { Kind = "membership", Status = state });
                info.Children.Add(line);
                var days = end.HasValue ? (end.Value.Date - DateTime.Today).Days : (int?)null;
                info.Children.Add(new TextBlock
                {
                    Style = R<Style>("Text.Secondary"), Margin = new Thickness(0, 6, 0, 0),
                    Text = end == null ? "Bạn chưa đăng ký gói nào. Hãy chọn một gói bên dưới để bắt đầu tập."
                         : days >= 0 ? $"Hạn sử dụng đến {end:dd/MM/yyyy} – còn {days} ngày."
                         : $"Gói đã hết hạn từ {end:dd/MM/yyyy}. Gia hạn để tiếp tục tập và đăng ký lớp."
                });
                dock.Children.Add(info);
                card.Child = dock;
                _top.Children.Add(card);

                _plans.Children.Clear();
                foreach (var p in plans)
                {
                    var pc = new Border { Style = R<Style>("Card"), Width = 250, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(18) };
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = p.Name, Style = R<Style>("Text.H3") });
                    sp.Children.Add(new TextBlock { Text = Formats.Money(p.Price), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = R<Brush>("PrimaryBrush"), Margin = new Thickness(0, 6, 0, 0) });
                    sp.Children.Add(new TextBlock { Text = $"{p.DurationDays} ngày · {(p.IncludesGroupClasses ? "gồm lớp nhóm" : "chỉ phòng gym")}", Style = R<Style>("Text.Caption") });
                    sp.Children.Add(new TextBlock { Text = p.Description, Style = R<Style>("Text.Secondary"), Margin = new Thickness(0, 8, 0, 12), MinHeight = 36 });
                    var buy = new Button { Content = "Chọn gói này", Style = R<Style>("Btn.Secondary") };
                    System.Windows.Automation.AutomationProperties.SetName(buy, $"Chọn {p.Name}");
                    var planId = p.Id;
                    buy.Click += async (_, _) => { if (await SalesForms.SubscribeAsync(MemberId, selfService: true, planId: planId) != null) await RefreshAsync(); };
                    sp.Children.Add(buy);
                    pc.Child = sp;
                    _plans.Children.Add(pc);
                }
            }

            private async Task RefreshAsync()
            {
                await LoadAsync();
                if (((Grid)((ScrollViewer)Content).Content).Children[1] is TablePage<PaymentRow> t) await t.ReloadAsync();
                await UiHost.RefreshNotificationsAsync();
            }
        }

        // ------------------------------------------------------------------ ĐIỂM DANH
        public static FrameworkElement Attendance() => new TablePage<AttendanceRow>(new TableSpec<AttendanceRow>
        {
            Title = "Lịch sử điểm danh",
            Unit = "buổi",
            EmptyIcon = Icons.Checklist,
            Load = async () => (await ServiceScope.Run(s => s.Attendance.MemberHistoryAsync(MemberId))).Select(a => new AttendanceRow { Entity = a }).ToList(),
            Search = r => $"{r.ClassName} {r.CoachName} {r.Notes}",
            DefaultSort = nameof(AttendanceRow.Start),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Buổi tập", nameof(AttendanceRow.DateText), ColKind.TwoLine) { Path2 = nameof(AttendanceRow.TimeText), Bold = true, SortPath = nameof(AttendanceRow.Start), Width = Col.Px(170) },
                new Col("Lớp", nameof(AttendanceRow.ClassName), ColKind.TwoLine) { Path2 = nameof(AttendanceRow.CoachName), Width = Col.Star(1.8), MinWidth = 180 },
                new Col("Phòng", nameof(AttendanceRow.RoomName)) { Width = Col.Px(130), HideBelow = 820 },
                new Col("Ghi chú", nameof(AttendanceRow.Notes)) { Width = Col.Star(1), MinWidth = 120, HideBelow = 1000 },
                new Col("Trạng thái", nameof(AttendanceRow.Status), ColKind.Badge) { BadgeKind = "attendance" },
            },
            Filters =
            {
                new Filter<AttendanceRow>("Mọi trạng thái", (r, v) => r.Status == v!.ToString()) { Options = Rules.Enum<AttendanceStatus>("attendance"), Width = 150 },
                new Filter<AttendanceRow>("Mọi lớp", (r, v) => r.ClassName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.ClassName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 },
            },
        });

        // ------------------------------------------------------------------ KẾT QUẢ & NHẬN XÉT
        public static FrameworkElement Progress() => new TablePage<ProgressItem>(new TableSpec<ProgressItem>
        {
            Title = "Kết quả & nhận xét",
            Subtitle = "Kết quả sau mỗi buổi tập và đánh giá tiến độ của huấn luyện viên",
            Unit = "mục",
            EmptyIcon = Icons.Success,
            Load = async () =>
            {
                var (logs, reviews) = await ServiceScope.Run(async s => (await s.Training.LogsAsync(MemberId), await s.Training.ReviewsAsync(MemberId)));
                return logs.Select(l => new ProgressItem
                {
                    Date = l.Date, Kind = "Log", Title = l.Session?.Class?.Name ?? "Buổi tập", Details = l.Exercises + (l.Notes.Length > 0 ? $" · {l.Notes}" : ""),
                    CoachName = l.Coach?.User?.FullName ?? "", Extra = l.WeightKg.HasValue ? $"{l.WeightKg:0.#} kg" : ""
                }).Concat(reviews.Select(r => new ProgressItem
                {
                    Date = r.Date, Kind = "Review", Title = $"Điểm tiến độ {r.Score}/10", Details = r.Comment, CoachName = r.Coach?.User?.FullName ?? "", Extra = $"{r.Score}/10"
                })).ToList();
            },
            Search = r => $"{r.Title} {r.Details} {r.CoachName}",
            DefaultSort = nameof(ProgressItem.Date),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Ngày", nameof(ProgressItem.DateText)) { SortPath = nameof(ProgressItem.Date), Width = Col.Px(110), Bold = true },
                new Col("Loại", nameof(ProgressItem.Kind), ColKind.Badge) { BadgeKind = "progress" },
                new Col("Nội dung", nameof(ProgressItem.Title), ColKind.TwoLine) { Path2 = nameof(ProgressItem.Details), Bold = true, Width = Col.Star(3), MinWidth = 240 },
                new Col("Huấn luyện viên", nameof(ProgressItem.CoachName)) { Width = Col.Star(1), MinWidth = 140, HideBelow = 820 },
                new Col("Chỉ số", nameof(ProgressItem.Extra)) { Width = Col.Px(90), HideBelow = 700 },
            },
            Filters = { new Filter<ProgressItem>("Tất cả", (r, v) => r.Kind == (string)v!) { Options = new[] { ("Kết quả tập", (object)"Log"), ("Nhận xét HLV", "Review") }, Width = 150 } },
            Open = async r =>
            {
                await InfoDialog.ShowAsync(r.Title, $"{r.DateText} · {r.CoachName}", Icons.Success, new[] { new InfoSection("Chi tiết", Body: r.Details) });
                return false;
            },
        });

        // ------------------------------------------------------------------ KẾ HOẠCH TẬP
        public static FrameworkElement Plans() => new TablePage<PlanRow>(new TableSpec<PlanRow>
        {
            Title = "Kế hoạch tập",
            Subtitle = "Kế hoạch cá nhân và kế hoạch của lớp do huấn luyện viên xây dựng",
            Unit = "kế hoạch",
            EmptyIcon = Icons.Edit,
            Load = async () => (await ServiceScope.Run(s => s.Training.PlansOfMemberAsync(MemberId))).Select(p => new PlanRow { Entity = p }).ToList(),
            Search = r => $"{r.Title} {r.Goal} {r.Content} {r.CoachName}",
            DefaultSort = nameof(PlanRow.StartDate),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Kế hoạch", nameof(PlanRow.Title), ColKind.TwoLine) { Path2 = nameof(PlanRow.Goal), Bold = true, Width = Col.Star(2.4), MinWidth = 220 },
                new Col("Huấn luyện viên", nameof(PlanRow.CoachName)) { Width = Col.Star(1.2), MinWidth = 140, HideBelow = 760 },
                new Col("Áp dụng", nameof(PlanRow.Scope)) { Width = Col.Px(100), HideBelow = 900 },
                new Col("Thời gian", nameof(PlanRow.PeriodText)) { SortPath = nameof(PlanRow.StartDate), Width = Col.Px(200), HideBelow = 1060 },
            },
            RowActions = { new RowAction<PlanRow>("", "Xem kế hoạch", CoachPages.ShowPlanAsync) },
            Open = CoachPages.ShowPlanAsync,
        });
    }

    /// <summary>Lịch sử thanh toán của chính thành viên (dùng trong trang Gói thành viên).</summary>
    internal static class OperationsPagesFor
    {
        public static TablePage<PaymentRow> MyPayments() => new(new TableSpec<PaymentRow>
        {
            Title = "Lịch sử thanh toán",
            Unit = "hóa đơn",
            EmptyIcon = Icons.Money,
            Load = async () => (await ServiceScope.Run(s => s.Payments.ListAsync(memberId: Me.MemberId ?? -1))).Select(p => new PaymentRow { Entity = p }).ToList(),
            Search = r => $"{r.InvoiceNo} {r.Note}",
            DefaultSort = nameof(PaymentRow.PaidAt),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Hóa đơn", nameof(PaymentRow.InvoiceNo), ColKind.TwoLine) { Path2 = nameof(PaymentRow.PaidText), Bold = true, SortPath = nameof(PaymentRow.PaidAt), Width = Col.Px(170) },
                new Col("Nội dung", nameof(PaymentRow.Note)) { Width = Col.Star(2.5), MinWidth = 200 },
                new Col("Số tiền", nameof(PaymentRow.AmountText)) { SortPath = nameof(PaymentRow.Amount), Width = Col.Px(120), Bold = true },
                new Col("Hình thức", nameof(PaymentRow.Method), ColKind.Badge) { BadgeKind = "payment", HideBelow = 760 },
            },
            RowActions = { new RowAction<PaymentRow>("", "Lưu hóa đơn (HTML)", async r => { await InvoicePrinter.ExportAsync(r.Id); return false; }) },
        });
    }
}
