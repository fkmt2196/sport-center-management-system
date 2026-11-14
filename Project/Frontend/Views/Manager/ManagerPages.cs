using SportsCenter.BLL;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using Project.Frontend.Views.Shared;
using SportsCenter.DAL.Models;
using SportsCenter.BLL.Services;

namespace Project.Frontend.Views.Manager
{
    internal static class M
    {
        public static T R<T>(string k) => (T)Application.Current.FindResource(k);

        public static Border Card(UIElement content, string title, string? caption = null)
        {
            var panel = new DockPanel();
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            head.Children.Add(new TextBlock { Text = title, Style = R<Style>("Text.H3") });
            if (caption != null) head.Children.Add(new TextBlock { Text = caption, Style = R<Style>("Text.Caption"), Margin = new Thickness(0, 4, 0, 0) });
            DockPanel.SetDock(head, Dock.Top);
            panel.Children.Add(head);
            panel.Children.Add(content);
            return new Border { Style = R<Style>("Card"), Margin = new Thickness(8), Child = panel };
        }

        public static string MonthLabel(DateTime m) => $"T{m.Month}/{m:yy}";
    }

    // =====================================================================
    // TỔNG QUAN
    // =====================================================================
    public class DashboardPage : UserControl
    {
        private readonly UniformGrid _stats = new() { Columns = 4 };
        private readonly StatCard _members = new(Icons.People, "Primary", "Thành viên");
        private readonly StatCard _revenue = new(Icons.Money, "Success", "Doanh thu tháng này");
        private readonly StatCard _classes = new(Icons.Library, "Violet", "Lớp đang mở");
        private readonly StatCard _today = new(Icons.Checklist, "Warning", "Check-in hôm nay");
        private readonly BarChart _revenueChart = new() { Height = 240 };
        private readonly BarChart _memberChart = new() { Height = 240 };
        private readonly StackPanel _todayList = new();
        private readonly Grid _charts = new();
        private readonly TextBlock _updated = new();

        public DashboardPage()
        {
            foreach (var c in new[] { _members, _revenue, _classes, _today }) _stats.Children.Add(c);
            var page = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(8, 0, 8, 12) };
            var refresh = TablePage<object>.IconTextButton("Làm mới", Icons.Refresh, "Btn.Secondary");
            refresh.Click += async (_, _) => await LoadAsync();
            DockPanel.SetDock(refresh, Dock.Right);
            header.Children.Add(refresh);
            var t = new StackPanel();
            t.Children.Add(new TextBlock { Text = "Tổng quan", Style = M.R<Style>("Text.H1") });
            _updated.Style = M.R<Style>("Text.Secondary"); _updated.Margin = new Thickness(0, 4, 0, 0);
            t.Children.Add(_updated);
            header.Children.Add(t);
            page.Children.Add(header);
            page.Children.Add(_stats);

            _charts.ColumnDefinitions.Add(new ColumnDefinition());
            _charts.ColumnDefinitions.Add(new ColumnDefinition());
            _charts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _charts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var c1 = M.Card(_revenueChart, "Doanh thu 6 tháng gần nhất", "Đơn vị: triệu đồng · gồm gói thành viên, học phí lớp và thu khác");
            var c2 = M.Card(_memberChart, "Thành viên mới theo tháng", "Số tài khoản thành viên đăng ký mới");
            Grid.SetColumn(c2, 1);
            _charts.Children.Add(c1); _charts.Children.Add(c2);
            page.Children.Add(_charts);
            page.Children.Add(M.Card(_todayList, "Lịch hoạt động hôm nay", DateTime.Today.ToString("dddd, dd/MM/yyyy")));
            Content = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(-8, 0, -8, 0) };

            SizeChanged += (_, _) =>
            {
                _stats.Columns = ActualWidth >= 900 ? 4 : ActualWidth >= 520 ? 2 : 1;
                var stacked = ActualWidth < 900;
                _charts.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
                Grid.SetColumn(c2, stacked ? 0 : 1); Grid.SetRow(c2, stacked ? 1 : 0);
            };
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var (stats, revenue, members, sessions) = await ServiceScope.Run(async s => (
                    await s.Reports.DashboardAsync(), await s.Reports.RevenueByMonthAsync(6), await s.Reports.MembersByMonthAsync(6),
                    await s.Classes.SessionsAsync(DateTime.Today, DateTime.Today.AddDays(1))));
                _members.SetValue(stats.TotalMembers.ToString("N0"), $"{stats.ActiveMembers} còn hạn gói · +{stats.NewMembersThisMonth} mới tháng này", stats.NewMembersThisMonth > 0 ? true : null);
                if (stats.RevenueLastMonth > 0)
                {
                    var pct = (stats.RevenueThisMonth - stats.RevenueLastMonth) / stats.RevenueLastMonth * 100;
                    _revenue.SetValue(Formats.MoneyShort(stats.RevenueThisMonth), $"{(pct >= 0 ? "▲" : "▼")} {Math.Abs(pct):0.#}% so với tháng trước ({Formats.MoneyShort(stats.RevenueLastMonth)})", pct >= 0);
                }
                else _revenue.SetValue(Formats.MoneyShort(stats.RevenueThisMonth), "Chưa có doanh thu tháng trước");
                _classes.SetValue(stats.OpenClasses.ToString(), $"{stats.Coaches} huấn luyện viên đang làm việc");
                _today.SetValue(stats.CheckInsToday.ToString(), $"{stats.SessionsToday} ca học hôm nay · {stats.ExpiringSoon} gói sắp hết hạn", stats.ExpiringSoon > 0 ? false : null);

                var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _revenueChart.SetData(revenue.Select(r => new BarPoint(M.MonthLabel(r.Month), (double)Math.Round((r.Value + r.Secondary) / 1_000_000m, 1),
                    $"{r.Month:MM/yyyy}: {r.Value + r.Secondary:N0} ₫ (gói {r.Value:N0} ₫, lớp & khác {r.Secondary:N0} ₫)", r.Month == month)).ToList());
                _memberChart.SetData(members.Select(r => new BarPoint(M.MonthLabel(r.Month), (double)r.Value,
                    $"{r.Month:MM/yyyy}: {r.Value} thành viên mới · {r.Secondary} lượt đăng ký lớp", r.Month == month)).ToList());

                _todayList.Children.Clear();
                if (sessions.Count == 0) _todayList.Children.Add(new EmptyState(Icons.Calendar, "Không có ca học", "Hôm nay không có lớp nào được xếp lịch."));
                foreach (var s in sessions.Select(x => new SessionRow { Entity = x.Session, Enrolled = x.Enrolled, Present = x.Present }))
                {
                    var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                    var badge = new StatusBadge { Kind = "session", Status = s.Status };
                    DockPanel.SetDock(badge, Dock.Right);
                    row.Children.Add(badge);
                    var time = new Border { Width = 92, Background = M.R<Brush>("PrimarySoftBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(0, 6, 0, 6), Margin = new Thickness(0, 0, 12, 0),
                        Child = new TextBlock { Text = s.TimeText, FontWeight = FontWeights.SemiBold, Foreground = M.R<Brush>("PrimaryPressedBrush"), HorizontalAlignment = HorizontalAlignment.Center } };
                    DockPanel.SetDock(time, Dock.Left);
                    row.Children.Add(time);
                    var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    info.Children.Add(new TextBlock { Text = s.ClassName, FontWeight = FontWeights.SemiBold });
                    info.Children.Add(new TextBlock { Text = $"{s.CoachName} · {s.RoomName} · {s.Enrolled} học viên", Style = M.R<Style>("Text.Caption") });
                    row.Children.Add(info);
                    _todayList.Children.Add(row);
                }
                _updated.Text = $"Số liệu cập nhật lúc {DateTime.Now:HH:mm dd/MM/yyyy}";
            }
            catch (Exception ex) { UiHost.Toast($"Không tải được số liệu: {(ex.InnerException ?? ex).Message}", ToastKind.Error); }
        }
    }

    // =====================================================================
    // BÁO CÁO
    // =====================================================================
    public class ReportsPage : UserControl
    {
        private readonly ComboBox _period = new() { Width = 170 };
        private readonly BarChart _revenue = new() { Height = 260 };
        private readonly BarChart _members = new() { Height = 220 };
        private readonly BarChart _enrolls = new() { Height = 220 };
        private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
        private readonly DataGrid _fill = new() { MaxHeight = 520 };
        private List<MonthValue> _rev = new(), _mem = new();
        private List<ClassFill> _classes = new();

        public ReportsPage()
        {
            var page = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(8, 0, 8, 12) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ListPage.FillFilter(_period, "6 tháng gần nhất", new[] { ("12 tháng gần nhất", (object)12), ("3 tháng gần nhất", 3) });
            _period.SelectionChanged += async (_, _) => await LoadAsync();
            right.Children.Add(_period);
            var export = TablePage<object>.IconTextButton("Xuất CSV", "", "Btn.Secondary");
            export.Margin = new Thickness(10, 0, 0, 0);
            export.Click += (_, _) => ExportCsv();
            right.Children.Add(export);
            DockPanel.SetDock(right, Dock.Right);
            header.Children.Add(right);
            var t = new StackPanel();
            t.Children.Add(new TextBlock { Text = "Báo cáo", Style = M.R<Style>("Text.H1") });
            t.Children.Add(new TextBlock { Text = "Doanh thu theo thời gian, thành viên và tình trạng đăng ký lớp", Style = M.R<Style>("Text.Secondary"), Margin = new Thickness(0, 4, 0, 0) });
            header.Children.Add(t);
            page.Children.Add(header);

            var revPanel = new StackPanel();
            revPanel.Children.Add(_revenue);
            _summary.Style = M.R<Style>("Text.Secondary");
            _summary.Margin = new Thickness(0, 12, 0, 0);
            revPanel.Children.Add(_summary);
            page.Children.Add(M.Card(revPanel, "Doanh thu theo tháng (triệu đồng)", "Tổng tiền đã thu: gói thành viên + học phí lớp + thu khác"));

            var two = new UniformGrid { Columns = 2 };
            two.Children.Add(M.Card(_members, "Thành viên mới"));
            two.Children.Add(M.Card(_enrolls, "Lượt đăng ký lớp mới"));
            page.Children.Add(two);
            SizeChanged += (_, _) => two.Columns = ActualWidth < 860 ? 1 : 2;

            _fill.Columns.Add(new DataGridTextColumn { Header = "Lớp", Binding = new Binding(nameof(ClassFill.ClassName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _fill.Columns.Add(new DataGridTextColumn { Header = "Bộ môn", Binding = new Binding(nameof(ClassFill.Sport)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _fill.Columns.Add(new DataGridTextColumn { Header = "HLV", Binding = new Binding(nameof(ClassFill.Coach)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _fill.Columns.Add(new DataGridTextColumn { Header = "Đã đăng ký", Binding = new Binding("Text"), Width = new DataGridLength(110) });
            _fill.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Lấp đầy", Width = new DataGridLength(150),
                CellTemplate = new DataTemplate { VisualTree = ProgressCell() }
            });
            _fill.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Trạng thái", Width = new DataGridLength(140),
                CellTemplate = new DataTemplate { VisualTree = BadgeCell() }
            });
            page.Children.Add(M.Card(_fill, "Tình trạng đăng ký lớp", "Số thành viên đang học / sĩ số tối đa"));

            Content = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(-8, 0, -8, 0) };
            Loaded += async (_, _) => await LoadAsync();
        }

        private static FrameworkElementFactory ProgressCell()
        {
            var p = new FrameworkElementFactory(typeof(ProgressBar));
            p.SetBinding(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new Binding("Percent") { Mode = BindingMode.OneWay });
            p.SetValue(HeightProperty, 6.0); p.SetValue(WidthProperty, 110.0); p.SetValue(BorderThicknessProperty, new Thickness(0));
            p.SetValue(ForegroundProperty, M.R<Brush>("PrimaryBrush")); p.SetValue(BackgroundProperty, M.R<Brush>("PrimarySoft2Brush"));
            p.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            return p;
        }

        private static FrameworkElementFactory BadgeCell()
        {
            var b = new FrameworkElementFactory(typeof(StatusBadge));
            b.SetValue(StatusBadge.KindProperty, "class");
            b.SetBinding(StatusBadge.StatusProperty, new Binding("Status"));
            return b;
        }

        private async Task LoadAsync()
        {
            try
            {
                var months = ListPage.SelectedValue(_period) as int? ?? 6;
                (_rev, _mem, _classes) = await ServiceScope.Run(async s => (await s.Reports.RevenueByMonthAsync(months), await s.Reports.MembersByMonthAsync(months), await s.Reports.ClassFillAsync()));
                var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _revenue.SetData(_rev.Select(r => new BarPoint(M.MonthLabel(r.Month), (double)Math.Round((r.Value + r.Secondary) / 1_000_000m, 1),
                    $"{r.Month:MM/yyyy}: {r.Value + r.Secondary:N0} ₫ – gói {r.Value:N0} ₫, lớp & khác {r.Secondary:N0} ₫", r.Month == month)).ToList());
                _members.SetData(_mem.Select(r => new BarPoint(M.MonthLabel(r.Month), (double)r.Value, $"{r.Month:MM/yyyy}: {r.Value} thành viên mới", r.Month == month)).ToList());
                _enrolls.SetData(_mem.Select(r => new BarPoint(M.MonthLabel(r.Month), (double)r.Secondary, $"{r.Month:MM/yyyy}: {r.Secondary} lượt đăng ký lớp", r.Month == month)).ToList());
                var total = _rev.Sum(r => r.Value + r.Secondary);
                var best = _rev.OrderByDescending(r => r.Value + r.Secondary).First();
                _summary.Text = $"Tổng {months} tháng: {total:N0} ₫ (gói {_rev.Sum(r => r.Value):N0} ₫ · lớp & khác {_rev.Sum(r => r.Secondary):N0} ₫). "
                              + $"Trung bình {total / months:N0} ₫/tháng. Cao nhất: tháng {best.Month:MM/yyyy} ({best.Value + best.Secondary:N0} ₫).";
                _fill.ItemsSource = _classes.Select(c => new
                {
                    c.ClassName, c.Sport, c.Coach, Text = $"{c.Enrolled}/{c.Capacity}",
                    Percent = c.Capacity > 0 ? 100.0 * c.Enrolled / c.Capacity : 0, Status = c.Status.ToString()
                }).OrderByDescending(x => x.Percent).ToList();
            }
            catch (Exception ex) { UiHost.Toast($"Không tải được báo cáo: {(ex.InnerException ?? ex).Message}", ToastKind.Error); }
        }

        private void ExportCsv()
        {
            var dlg = new SaveFileDialog { FileName = $"BaoCao_{DateTime.Today:yyyyMMdd}.csv", Filter = "CSV (*.csv)|*.csv" };
            if (dlg.ShowDialog() != true) return;
            var sb = new StringBuilder();
            sb.AppendLine("Tháng;Doanh thu gói (VNĐ);Doanh thu lớp & khác (VNĐ);Tổng (VNĐ);Thành viên mới;Đăng ký lớp mới");
            foreach (var (r, m) in _rev.Zip(_mem))
                sb.AppendLine($"{r.Month:MM/yyyy};{r.Value:0};{r.Secondary:0};{r.Value + r.Secondary:0};{m.Value:0};{m.Secondary:0}");
            sb.AppendLine();
            sb.AppendLine("Lớp;Bộ môn;HLV;Đã đăng ký;Sĩ số;Trạng thái");
            foreach (var c in _classes) sb.AppendLine($"{c.ClassName};{c.Sport};{c.Coach};{c.Enrolled};{c.Capacity};{StatusBadge.Describe("class", c.Status.ToString()).Label}");
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            UiHost.Toast($"Đã xuất báo cáo ra {dlg.FileName}.");
        }
    }

    // =====================================================================
    // DANH SÁCH (TablePage)
    // =====================================================================
    public class CoachRowM
    {
        public required Coach Entity { get; init; }
        public string FullName => Entity.User!.FullName;
        public string Username => Entity.User!.Username;
        public string Phone => Entity.User!.Phone;
        public string Email => Entity.User!.Email;
        public string Specialty => Entity.Specialty;
        public string Certifications => Entity.Certifications;
        public int Years => Entity.YearsExperience;
        public string YearsText => $"{Entity.YearsExperience} năm";
        public int ClassCount => Entity.Classes.Count(c => c.Status == ClassStatus.Open);
        public string Available => Entity.IsAvailable.ToString();
    }

    public class UserRowM
    {
        public required User Entity { get; init; }
        public string FullName => Entity.FullName;
        public string Username => Entity.Username;
        public string Role => Entity.Role.ToString();
        public string Phone => Entity.Phone;
        public string Email => Entity.Email;
        public DateTime? LastLogin => Entity.LastLogin;
        public string LastLoginText => Entity.LastLogin?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đăng nhập";
        public string Active => Entity.IsActive.ToString();
    }

    public class AuditRow
    {
        public required AuditLog Entity { get; init; }
        public DateTime Timestamp => Entity.Timestamp;
        public string TimeText => Entity.Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
        public string Username => Entity.Username;
        public string Action => Entity.Action;
        public string EntityName => Names.GetValueOrDefault(Entity.EntityName, Entity.EntityName);
        public string Target => $"{EntityName} #{Entity.EntityId}";
        public string Details => Entity.Details;

        private static readonly Dictionary<string, string> Names = new()
        {
            ["User"] = "Tài khoản", ["Member"] = "Thành viên", ["Coach"] = "Huấn luyện viên", ["Sport"] = "Bộ môn", ["Room"] = "Phòng tập",
            ["TrainingClass"] = "Lớp học", ["ClassSession"] = "Ca học", ["Enrollment"] = "Đăng ký lớp", ["Attendance"] = "Điểm danh",
            ["CheckIn"] = "Check-in", ["MembershipPlan"] = "Gói thành viên", ["Subscription"] = "Đăng ký gói", ["Payment"] = "Thanh toán",
            ["TrainingPlan"] = "Kế hoạch tập", ["WorkoutLog"] = "Kết quả tập", ["ProgressReview"] = "Đánh giá", ["Notification"] = "Thông báo",
            ["SupportRequest"] = "Hỗ trợ",
        };
    }

    public static class ManagerPages
    {
        public static FrameworkElement Coaches() => new TablePage<CoachRowM>(new TableSpec<CoachRowM>
        {
            Title = "Huấn luyện viên",
            Unit = "huấn luyện viên",
            EmptyIcon = Icons.Education,
            Load = async () => (await ServiceScope.Run(s => s.Coaches.ListAsync())).Select(c => new CoachRowM { Entity = c }).ToList(),
            Search = r => $"{r.FullName} {r.Username} {r.Specialty} {r.Phone}",
            SearchPlaceholder = "Tìm theo tên, chuyên môn, số điện thoại...",
            DefaultSort = nameof(CoachRowM.FullName),
            Columns =
            {
                new Col("Huấn luyện viên", nameof(CoachRowM.FullName), ColKind.Avatar) { Path2 = nameof(CoachRowM.Username), Width = Col.Star(1.6), MinWidth = 200 },
                new Col("Chuyên môn", nameof(CoachRowM.Specialty), ColKind.TwoLine) { Path2 = nameof(CoachRowM.Certifications), Width = Col.Star(2), MinWidth = 180, HideBelow = 700 },
                new Col("Kinh nghiệm", nameof(CoachRowM.YearsText)) { SortPath = nameof(CoachRowM.Years), Width = Col.Px(110), HideBelow = 1000 },
                new Col("Lớp đang dạy", nameof(CoachRowM.ClassCount)) { Width = Col.Px(110), HideBelow = 860 },
                new Col("Liên hệ", nameof(CoachRowM.Phone), ColKind.TwoLine) { Path2 = nameof(CoachRowM.Email), Width = Col.Star(1.4), MinWidth = 160, HideBelow = 1150 },
                new Col("Tình trạng", nameof(CoachRowM.Available), ColKind.Badge) { BadgeKind = "coach" },
            },
            Filters = { new Filter<CoachRowM>("Mọi tình trạng", (r, v) => r.Available == v!.ToString()) { Options = new[] { ("Nhận lớp", (object)true), ("Tạm ngưng", false) }, Width = 150 } },
            Actions = { new("Thêm huấn luyện viên", Icons.Add, () => PeopleForms.CoachAsync(null)) },
            RowActions =
            {
                new RowAction<CoachRowM>(Icons.Edit, "Sửa hồ sơ", r => PeopleForms.CoachAsync(r.Entity)),
                new RowAction<CoachRowM>(Icons.Library, "Lớp phụ trách", async r =>
                {
                    var classes = r.Entity.Classes.OrderByDescending(c => c.StartDate)
                        .Select(c => (c.Name, $"{StatusBadge.Describe("class", c.Status.ToString()).Label} · {c.StartDate:dd/MM/yyyy}{(c.EndDate.HasValue ? $" – {c.EndDate:dd/MM/yyyy}" : "")}")).ToList();
                    await InfoDialog.ShowAsync(r.FullName, r.Specialty, Icons.Education, new[]
                    {
                        new InfoSection("Thông tin", new List<(string, string)> { ("Điện thoại", r.Phone), ("Email", r.Email), ("Kinh nghiệm", r.YearsText), ("Chứng chỉ", r.Certifications) }),
                        new InfoSection("Lớp phụ trách", classes.DefaultIfEmpty(("—", "Chưa phụ trách lớp nào")).ToList()),
                        new InfoSection("Giới thiệu", Body: r.Entity.Bio),
                    });
                    return false;
                }),
            },
            Open = r => PeopleForms.CoachAsync(r.Entity),
        });

        public static FrameworkElement Accounts() => new TablePage<UserRowM>(new TableSpec<UserRowM>
        {
            Title = "Tài khoản & phân quyền",
            Subtitle = "Mỗi vai trò chỉ thấy chức năng của mình",
            Unit = "tài khoản",
            EmptyIcon = Icons.Contact,
            Load = async () => (await ServiceScope.Run(s => s.Accounts.ListAsync())).Select(u => new UserRowM { Entity = u }).ToList(),
            Search = r => $"{r.FullName} {r.Username} {r.Phone} {r.Email}",
            SearchPlaceholder = "Tìm theo tên, tên đăng nhập, liên hệ...",
            DefaultSort = nameof(UserRowM.Role),
            Columns =
            {
                new Col("Tài khoản", nameof(UserRowM.FullName), ColKind.Avatar) { Path2 = nameof(UserRowM.Username), Width = Col.Star(1.6), MinWidth = 200 },
                new Col("Vai trò", nameof(UserRowM.Role), ColKind.Badge) { BadgeKind = "role" },
                new Col("Liên hệ", nameof(UserRowM.Phone), ColKind.TwoLine) { Path2 = nameof(UserRowM.Email), Width = Col.Star(1.6), MinWidth = 170, HideBelow = 900 },
                new Col("Đăng nhập gần nhất", nameof(UserRowM.LastLoginText)) { SortPath = nameof(UserRowM.LastLogin), Width = Col.Px(160), HideBelow = 1060 },
                new Col("Trạng thái", nameof(UserRowM.Active), ColKind.Badge) { BadgeKind = "account" },
            },
            Filters =
            {
                new Filter<UserRowM>("Mọi vai trò", (r, v) => r.Role == v!.ToString()) { Options = Rules.Enum<UserRole>("role"), Width = 150 },
                new Filter<UserRowM>("Mọi trạng thái", (r, v) => r.Active == v!.ToString()) { Options = new[] { ("Hoạt động", (object)true), ("Đã khóa", false) }, Width = 150 },
            },
            Actions = { new("Tạo tài khoản", Icons.Add, () => AccountForms.EditAsync(null)) },
            RowActions =
            {
                new RowAction<UserRowM>(Icons.Edit, "Sửa & phân quyền", r => AccountForms.EditAsync(r.Entity)),
                new RowAction<UserRowM>("", "Đặt lại mật khẩu", r => AccountForms.ResetPasswordAsync(r.Entity)),
                new RowAction<UserRowM>("", "Khóa / mở khóa", async r =>
                {
                    var u = r.Entity;
                    if (u.IsActive && !await UiHost.ConfirmAsync("Khóa tài khoản?", $"{u.FullName} sẽ không đăng nhập được cho tới khi mở khóa.", "Khóa")) return false;
                    await ServiceScope.Run(s => s.Accounts.UpdateAsync(u.Id, u.FullName, u.Phone, u.Email, u.Role, !u.IsActive));
                    UiHost.Toast(u.IsActive ? $"Đã khóa tài khoản {u.Username}." : $"Đã mở khóa tài khoản {u.Username}.");
                    return true;
                }) { Visible = r => r.Entity.Id != Me.User.Id },
                new RowAction<UserRowM>(Icons.Delete, "Xóa tài khoản", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa tài khoản?", $"Xóa vĩnh viễn tài khoản {r.Username}. Tài khoản đã có dữ liệu sẽ không xóa được (hãy khóa).")) return false;
                    await ServiceScope.Run(s => s.Accounts.DeleteAsync(r.Entity.Id));
                    UiHost.Toast($"Đã xóa tài khoản {r.Username}.");
                    return true;
                }) { Danger = true, Visible = r => r.Entity.Id != Me.User.Id },
            },
            Open = r => AccountForms.EditAsync(r.Entity),
        });

        public static FrameworkElement Classes(int? coachId = null)
        {
            var manage = coachId == null;
            var spec = new TableSpec<ClassRow>
            {
                Title = manage ? "Lớp học" : "Lớp của tôi",
                Unit = "lớp",
                EmptyIcon = Icons.Library,
                Load = async () => (await ServiceScope.Run(s => s.Classes.ListAsync(coachId))).Select(c => new ClassRow { Info = c }).ToList(),
                Search = r => $"{r.Name} {r.SportName} {r.CoachName} {r.Description}",
                SearchPlaceholder = "Tìm theo tên lớp, bộ môn, HLV...",
                DefaultSort = nameof(ClassRow.Name),
                Columns =
                {
                    new Col("Lớp", nameof(ClassRow.Name), ColKind.TwoLine) { Path2 = nameof(ClassRow.SportName), Bold = true, Width = Col.Star(2), MinWidth = 200 },
                    new Col("Huấn luyện viên", nameof(ClassRow.CoachName)) { Width = Col.Star(1.3), MinWidth = 140, HideBelow = manage ? 880 : 99999 },
                    new Col("Trình độ", nameof(ClassRow.Level), ColKind.Badge) { BadgeKind = "level", HideBelow = 1180 },
                    new Col("Sĩ số", nameof(ClassRow.SeatsText), ColKind.Progress) { Path2 = nameof(ClassRow.FillPercent), SortPath = nameof(ClassRow.FillPercent), Width = Col.Px(110), HideBelow = 680 },
                    new Col("Học phí", nameof(ClassRow.FeeText)) { SortPath = nameof(ClassRow.Fee), Width = Col.Px(130), HideBelow = 1060 },
                    new Col("Ca kế tiếp", nameof(ClassRow.NextText)) { Width = Col.Px(140), HideBelow = 960 },
                    new Col("Trạng thái", nameof(ClassRow.Status), ColKind.Badge) { BadgeKind = "class" },
                },
                Filters =
                {
                    new Filter<ClassRow>("Mọi bộ môn", (r, v) => r.SportName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.SportName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 170 },
                    new Filter<ClassRow>("Mọi trạng thái", (r, v) => r.Status == v!.ToString()) { Options = Rules.Enum<ClassStatus>("class"), Width = 150, Default = ClassStatus.Open },
                },
            };
            if (manage)
            {
                spec.Filters.Insert(1, new Filter<ClassRow>("Mọi HLV", (r, v) => r.CoachName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.CoachName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 180 });
                spec.Actions.Add(new("Thêm lớp", Icons.Add, () => ClassForms.ClassAsync(null)));
                spec.RowActions.AddRange(new[]
                {
                    new RowAction<ClassRow>(Icons.Edit, "Sửa lớp", r => ClassForms.ClassAsync(r.Entity)),
                    new RowAction<ClassRow>(Icons.Education, "Phân công HLV", r => ClassForms.ClassAsync(r.Entity, assignOnly: true)),
                    new RowAction<ClassRow>(Icons.Calendar, "Xếp lịch theo tuần", r => ClassForms.GenerateSessionsAsync(r.Entity)) { Visible = r => r.Entity.Status == ClassStatus.Open },
                });
            }
            spec.RowActions.Add(new RowAction<ClassRow>(Icons.People, "Danh sách học viên", r => ClassMembersAsync(r, manage)));
            if (!manage)
                spec.RowActions.Add(new RowAction<ClassRow>("", "Gửi thông báo cho lớp", r => TrainingForms.SendNoticeAsync(coachId, presetClassId: r.Id)));
            if (manage)
                spec.RowActions.Add(new RowAction<ClassRow>(Icons.Delete, "Xóa lớp", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa lớp?", $"Xóa lớp \"{r.Name}\" cùng các ca học và đăng ký. Lớp đã có điểm danh/thanh toán thì không xóa được (hãy chuyển trạng thái Đã hủy).")) return false;
                    await ServiceScope.Run(s => s.Classes.DeleteAsync(r.Id));
                    UiHost.Toast($"Đã xóa lớp \"{r.Name}\".");
                    return true;
                }) { Danger = true });
            spec = new TableSpec<ClassRow>
            {
                Title = spec.Title, Unit = spec.Unit, EmptyIcon = spec.EmptyIcon, Load = spec.Load, Search = spec.Search, SearchPlaceholder = spec.SearchPlaceholder,
                DefaultSort = spec.DefaultSort, Columns = spec.Columns, Filters = spec.Filters, Actions = spec.Actions, RowActions = spec.RowActions,
                Open = manage ? r => ClassForms.ClassAsync(r.Entity) : r => ClassMembersAsync(r, false),
            };
            return new TablePage<ClassRow>(spec);
        }

        private static async Task<bool> ClassMembersAsync(ClassRow r, bool manage)
        {
            var list = await ServiceScope.Run(s => s.Classes.ClassMembersAsync(r.Id));
            var rows = list.OrderBy(e => e.Member!.User!.FullName)
                .Select(e => (e.Member!.User!.FullName, $"{e.Member.User.Phone} · {Labels.Level(e.Member.FitnessLevel)} · từ {e.EnrolledAt:dd/MM/yyyy}" + (e.Member.Goals.Length > 0 ? $" · Mục tiêu: {e.Member.Goals}" : ""))).ToList();
            await InfoDialog.ShowAsync($"Học viên lớp {r.Name}", $"{list.Count}/{r.Entity.Capacity} học viên · HLV {r.CoachName}", Icons.People,
                new[] { new InfoSection("Danh sách", rows.DefaultIfEmpty(("—", "Chưa có học viên")).ToList()) });
            return false;
        }

        /// <summary>Lịch hoạt động (ca học). coachId: chỉ lịch của HLV đó (không thêm/sửa/hủy).</summary>
        public static FrameworkElement Sessions(int? coachId = null)
        {
            var manage = coachId == null;
            var today = DateTime.Today;
            var spec = new TableSpec<SessionRow>
            {
                Title = manage ? "Lịch hoạt động" : "Lịch dạy",
                Subtitle = manage ? "Ca học của tất cả các lớp" : "Ca học của các lớp bạn phụ trách",
                Unit = "ca học",
                EmptyIcon = Icons.Calendar,
                EmptyText = "Không có ca học",
                Load = async () => (await ServiceScope.Run(s => s.Classes.SessionsAsync(today.AddDays(-60), today.AddDays(61), coachId: coachId)))
                    .Select(x => new SessionRow { Entity = x.Session, Enrolled = x.Enrolled, Present = x.Present }).ToList(),
                Search = r => $"{r.ClassName} {r.SportName} {r.CoachName} {r.RoomName}",
                SearchPlaceholder = "Tìm theo lớp, bộ môn, HLV, phòng...",
                DefaultSort = nameof(SessionRow.Start),
                Columns =
                {
                    new Col("Ngày", nameof(SessionRow.DateText), ColKind.TwoLine) { Path2 = nameof(SessionRow.TimeText), Bold = true, SortPath = nameof(SessionRow.Start), Width = Col.Px(150) },
                    new Col("Lớp", nameof(SessionRow.ClassName), ColKind.TwoLine) { Path2 = nameof(SessionRow.SportName), Width = Col.Star(1.8), MinWidth = 170 },
                    new Col("Huấn luyện viên", nameof(SessionRow.CoachName)) { Width = Col.Star(1.2), MinWidth = 130, HideBelow = manage ? 900 : 99999 },
                    new Col("Phòng", nameof(SessionRow.RoomName)) { Width = Col.Px(130), HideBelow = 700 },
                    new Col("Có mặt", nameof(SessionRow.AttendanceText)) { Width = Col.Px(80), HideBelow = 820 },
                    new Col("Trạng thái", nameof(SessionRow.Status), ColKind.Badge) { BadgeKind = "session" },
                },
                Filters =
                {
                    new Filter<SessionRow>("±60 ngày", (r, v) => (string)v! switch
                    {
                        "today" => r.Start.Date == today,
                        "next7" => r.Start.Date >= today && r.Start.Date < today.AddDays(7),
                        "next30" => r.Start.Date >= today && r.Start.Date < today.AddDays(30),
                        "past7" => r.Start.Date < today && r.Start.Date >= today.AddDays(-7),
                        "past30" => r.Start.Date < today && r.Start.Date >= today.AddDays(-30),
                        _ => true
                    })
                    {
                        Options = new[] { ("Hôm nay", (object)"today"), ("7 ngày tới", "next7"), ("30 ngày tới", "next30"), ("7 ngày qua", "past7"), ("30 ngày qua", "past30") },
                        Default = "next7", Width = 150
                    },
                    new Filter<SessionRow>("Mọi lớp", (r, v) => r.ClassName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.ClassName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 190 },
                    new Filter<SessionRow>("Mọi trạng thái", (r, v) => r.Entity.Status.ToString() == v!.ToString()) { Options = Rules.Enum<SessionStatus>("session"), Width = 150 },
                },
                RowActions =
                {
                    new RowAction<SessionRow>(Icons.Checklist, "Điểm danh", AttendanceSheet.ShowAsync) { Visible = r => r.IsPastOrToday && r.CanEdit },
                },
            };
            if (manage)
            {
                spec.Filters.Insert(2, new Filter<SessionRow>("Mọi phòng", (r, v) => r.RoomName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.RoomName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 150 });
                spec.Actions.Add(new("Thêm ca học", Icons.Add, () => ClassForms.SessionAsync(null)));
                spec.RowActions.Add(new RowAction<SessionRow>(Icons.Edit, "Đổi lịch", r => ClassForms.SessionAsync(r)) { Visible = r => r.Entity.Status == SessionStatus.Scheduled });
                spec.RowActions.Add(new RowAction<SessionRow>(Icons.Close, "Hủy ca", ClassForms.CancelSessionAsync) { Danger = true, Visible = r => r.Entity.Status == SessionStatus.Scheduled });
            }
            else
            {
                spec.RowActions.Add(new RowAction<SessionRow>(Icons.Edit, "Ghi kết quả buổi tập", async r =>
                {
                    await Coaching.CoachPages.LogForSessionAsync(r);
                    return false;
                }) { Visible = r => r.IsPastOrToday && r.CanEdit });
            }
            return new TablePage<SessionRow>(spec);
        }

        public class SportRow
        {
            public required Sport Entity { get; init; }
            public string Name => Entity.Name;
            public string Description => Entity.Description;
            public int ClassCount => Entity.Classes.Count;
            public string Active => Entity.IsActive.ToString();
        }

        public static FrameworkElement Sports() => new TablePage<SportRow>(new TableSpec<SportRow>
        {
            Title = "Bộ môn",
            Unit = "bộ môn",
            EmptyIcon = Icons.Library,
            Load = async () => (await ServiceScope.Run(s => s.Facilities.SportsAsync())).Select(x => new SportRow { Entity = x }).ToList(),
            Search = r => r.Name + " " + r.Description,
            DefaultSort = nameof(SportRow.Name),
            Columns =
            {
                new Col("Bộ môn", nameof(SportRow.Name), ColKind.TwoLine) { Path2 = nameof(SportRow.Description), Bold = true, Width = Col.Star(3), MinWidth = 220 },
                new Col("Số lớp", nameof(SportRow.ClassCount)) { Width = Col.Px(90) },
                new Col("Trạng thái", nameof(SportRow.Active), ColKind.Badge) { BadgeKind = "active" },
            },
            Actions = { new("Thêm bộ môn", Icons.Add, () => CatalogForms.SportAsync(null)) },
            RowActions =
            {
                new RowAction<SportRow>(Icons.Edit, "Sửa", r => CatalogForms.SportAsync(r.Entity)),
                new RowAction<SportRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa bộ môn?", $"Xóa bộ môn \"{r.Name}\"?")) return false;
                    await ServiceScope.Run(s => s.Facilities.DeleteSportAsync(r.Entity.Id));
                    UiHost.Toast($"Đã xóa bộ môn \"{r.Name}\".");
                    return true;
                }) { Danger = true },
            },
            Open = r => CatalogForms.SportAsync(r.Entity),
        });

        public class RoomRow
        {
            public required Room Entity { get; init; }
            public string Name => Entity.Name;
            public string Location => Entity.Location;
            public int Capacity => Entity.Capacity;
            public string CapacityText => $"{Entity.Capacity} người";
            public string Equipment => Entity.Equipment;
            public string Active => Entity.IsActive.ToString();
        }

        public static FrameworkElement Rooms() => new TablePage<RoomRow>(new TableSpec<RoomRow>
        {
            Title = "Phòng tập",
            Unit = "phòng",
            EmptyIcon = Icons.Location,
            Load = async () => (await ServiceScope.Run(s => s.Facilities.RoomsAsync())).Select(x => new RoomRow { Entity = x }).ToList(),
            Search = r => $"{r.Name} {r.Location} {r.Equipment}",
            DefaultSort = nameof(RoomRow.Name),
            Columns =
            {
                new Col("Phòng", nameof(RoomRow.Name), ColKind.TwoLine) { Path2 = nameof(RoomRow.Location), Bold = true, Width = Col.Star(1.3), MinWidth = 160 },
                new Col("Sức chứa", nameof(RoomRow.CapacityText)) { SortPath = nameof(RoomRow.Capacity), Width = Col.Px(110) },
                new Col("Trang thiết bị", nameof(RoomRow.Equipment)) { Width = Col.Star(2.5), MinWidth = 180, HideBelow = 760 },
                new Col("Trạng thái", nameof(RoomRow.Active), ColKind.Badge) { BadgeKind = "active" },
            },
            Actions = { new("Thêm phòng", Icons.Add, () => CatalogForms.RoomAsync(null)) },
            RowActions =
            {
                new RowAction<RoomRow>(Icons.Edit, "Sửa", r => CatalogForms.RoomAsync(r.Entity)),
                new RowAction<RoomRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa phòng?", $"Xóa phòng \"{r.Name}\"?")) return false;
                    await ServiceScope.Run(s => s.Facilities.DeleteRoomAsync(r.Entity.Id));
                    UiHost.Toast($"Đã xóa phòng \"{r.Name}\".");
                    return true;
                }) { Danger = true },
            },
            Open = r => CatalogForms.RoomAsync(r.Entity),
        });

        public class PlanRow
        {
            public required MembershipPlan Entity { get; init; }
            public string Name => Entity.Name;
            public string Description => Entity.Description;
            public decimal Price => Entity.Price;
            public string PriceText => Formats.Money(Entity.Price);
            public int Days => Entity.DurationDays;
            public string DaysText => $"{Entity.DurationDays} ngày";
            public string PerMonth => $"{Entity.Price / Math.Max(1, Entity.DurationDays) * 30:N0} ₫/tháng";
            public string Classes => Entity.IncludesGroupClasses ? "Có" : "Không";
            public string Active => Entity.IsActive.ToString();
        }

        public static FrameworkElement Plans() => new TablePage<PlanRow>(new TableSpec<PlanRow>
        {
            Title = "Gói thành viên",
            Subtitle = "Học phí và thời hạn các gói",
            Unit = "gói",
            EmptyIcon = Icons.Money,
            Load = async () => (await ServiceScope.Run(s => s.Memberships.PlansAsync())).Select(x => new PlanRow { Entity = x }).ToList(),
            Search = r => r.Name + " " + r.Description,
            DefaultSort = nameof(PlanRow.Days),
            Columns =
            {
                new Col("Gói", nameof(PlanRow.Name), ColKind.TwoLine) { Path2 = nameof(PlanRow.Description), Bold = true, Width = Col.Star(2.5), MinWidth = 220 },
                new Col("Giá", nameof(PlanRow.PriceText), ColKind.TwoLine) { Path2 = nameof(PlanRow.PerMonth), SortPath = nameof(PlanRow.Price), Width = Col.Px(150) },
                new Col("Thời hạn", nameof(PlanRow.DaysText)) { SortPath = nameof(PlanRow.Days), Width = Col.Px(100) },
                new Col("Lớp nhóm", nameof(PlanRow.Classes)) { Width = Col.Px(100), HideBelow = 760 },
                new Col("Trạng thái", nameof(PlanRow.Active), ColKind.Badge) { BadgeKind = "plan" },
            },
            Actions = { new("Thêm gói", Icons.Add, () => CatalogForms.PlanAsync(null)) },
            RowActions =
            {
                new RowAction<PlanRow>(Icons.Edit, "Sửa", r => CatalogForms.PlanAsync(r.Entity)),
                new RowAction<PlanRow>(Icons.Delete, "Xóa", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Xóa gói?", $"Xóa gói \"{r.Name}\"? Gói đã có người đăng ký sẽ không xóa được (hãy ngừng bán).")) return false;
                    await ServiceScope.Run(s => s.Memberships.DeletePlanAsync(r.Entity.Id));
                    UiHost.Toast($"Đã xóa gói \"{r.Name}\".");
                    return true;
                }) { Danger = true },
            },
            Open = r => CatalogForms.PlanAsync(r.Entity),
        });

        public static FrameworkElement Audit()
        {
            var today = DateTime.Today;
            return new TablePage<AuditRow>(new TableSpec<AuditRow>
            {
                Title = "Nhật ký thao tác",
                Subtitle = "Ghi tự động mọi thay đổi dữ liệu và đăng nhập/đăng xuất (90 ngày gần nhất)",
                Unit = "thao tác",
                EmptyIcon = "",
                Load = async () => (await ServiceScope.Run(s => s.Audit.ListAsync(today.AddDays(-90), today.AddDays(1)))).Select(a => new AuditRow { Entity = a }).ToList(),
                Search = r => $"{r.Username} {r.Action} {r.EntityName} {r.Details}",
                SearchPlaceholder = "Tìm theo người dùng, hành động, nội dung...",
                DefaultSort = nameof(AuditRow.Timestamp),
                SortDirection = System.ComponentModel.ListSortDirection.Descending,
                Columns =
                {
                    new Col("Thời gian", nameof(AuditRow.TimeText)) { SortPath = nameof(AuditRow.Timestamp), Width = Col.Px(160) },
                    new Col("Người dùng", nameof(AuditRow.Username)) { Width = Col.Px(120) },
                    new Col("Hành động", nameof(AuditRow.Action)) { Width = Col.Px(110), Bold = true },
                    new Col("Đối tượng", nameof(AuditRow.Target)) { SortPath = nameof(AuditRow.EntityName), Width = Col.Px(170), HideBelow = 700 },
                    new Col("Chi tiết", nameof(AuditRow.Details)) { Width = Col.Star(3), MinWidth = 200, HideBelow = 900 },
                },
                Filters =
                {
                    new Filter<AuditRow>("90 ngày", (r, v) => r.Timestamp >= today.AddDays(-(int)v! + 1)) { Options = new[] { ("Hôm nay", (object)1), ("7 ngày", 7), ("30 ngày", 30) }, Width = 120 },
                    new Filter<AuditRow>("Mọi hành động", (r, v) => r.Action == (string)v!) { OptionsFrom = rows => rows.Select(r => r.Action).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 150 },
                    new Filter<AuditRow>("Mọi đối tượng", (r, v) => r.EntityName == (string)v!) { OptionsFrom = rows => rows.Select(r => r.EntityName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 160 },
                    new Filter<AuditRow>("Mọi người dùng", (r, v) => r.Username == (string)v!) { OptionsFrom = rows => rows.Select(r => r.Username).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 150 },
                },
                Open = async r =>
                {
                    await InfoDialog.ShowAsync($"{r.Action} – {r.Target}", $"{r.Username} · {r.TimeText}", "", new[] { new InfoSection("Chi tiết", Body: r.Details) });
                    return false;
                },
            });
        }
    }
}
