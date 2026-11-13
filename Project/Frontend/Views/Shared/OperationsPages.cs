using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using SportsCenter.Core.Models;
using SportsCenter.Core.Services;

namespace Project.Frontend.Views.Shared
{
    public class EnrollmentRow
    {
        public required Enrollment Entity { get; init; }
        public int Id => Entity.Id;
        public string ClassName => Entity.Class?.Name ?? "";
        public string SportName => Entity.Class?.Sport?.Name ?? "";
        public string CoachName => Entity.Class?.Coach?.User?.FullName ?? "(chưa phân công)";
        public string Fee => Entity.Class?.Fee is decimal f && f > 0 ? Formats.Money(f) : "Gồm trong gói";
        public DateTime EnrolledAt => Entity.EnrolledAt;
        public string EnrolledText => Entity.EnrolledAt.ToString("dd/MM/yyyy");
        public string Status => Entity.Status.ToString();
        public bool IsActive => Entity.Status == EnrollmentStatus.Active;
    }

    public class SupportRow
    {
        public required SupportRequest Entity { get; init; }
        public string Subject => Entity.Subject;
        public string Description => Entity.Description;
        public string MemberName => Entity.Member?.User?.FullName ?? "Khách vãng lai";
        public string CreatedBy => Entity.CreatedBy?.FullName ?? "";
        public DateTime CreatedAt => Entity.CreatedAt;
        public string CreatedText => Entity.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        public string Status => Entity.Status.ToString();
        public string Response => Entity.Response;
    }

    /// <summary>Trang vận hành dùng chung cho Quản lý và Lễ tân.</summary>
    public static class OperationsPages
    {
        // ------------------------------------------------------------------ THÀNH VIÊN
        public static FrameworkElement Members(bool canRegister = true) => new TablePage<MemberRow>(new TableSpec<MemberRow>
        {
            Title = "Thành viên",
            Unit = "thành viên",
            EmptyIcon = Icons.People,
            Load = async () => (await UiScope.Run(s => s.Members.ListAsync())).Select(m => MemberRow.From(m)).ToList(),
            Search = r => $"{r.FullName} {r.Username} {r.Phone} {r.Email}",
            SearchPlaceholder = "Tìm theo tên, tên đăng nhập, số điện thoại, email...",
            DefaultSort = nameof(MemberRow.FullName),
            Columns =
            {
                new Col("Thành viên", nameof(MemberRow.FullName), ColKind.Avatar) { Path2 = nameof(MemberRow.Username), Width = Col.Star(2), MinWidth = 210 },
                new Col("Liên hệ", nameof(MemberRow.Phone), ColKind.TwoLine) { Path2 = nameof(MemberRow.Email), Width = Col.Star(1.6), MinWidth = 180, HideBelow = 900 },
                new Col("Trình độ", nameof(MemberRow.Level), ColKind.Badge) { BadgeKind = "level", Width = Col.Px(130), HideBelow = 1060 },
                new Col("Gói hiện tại", nameof(MemberRow.PlanName), ColKind.TwoLine) { Path2 = nameof(MemberRow.EndText), SortPath = nameof(MemberRow.MembershipEnd), Width = Col.Star(1.3), MinWidth = 150, HideBelow = 760 },
                new Col("Trạng thái gói", nameof(MemberRow.MembershipState), ColKind.Badge) { BadgeKind = "membership", Width = Col.Px(140) },
            },
            Filters =
            {
                new Filter<MemberRow>("Mọi trạng thái gói", (r, v) => r.MembershipState == (string)v!)
                {
                    Options = new[] { "Active", "Expiring", "Expired", "Upcoming", "None" }.Select(s => (StatusBadge.Describe("membership", s).Label, (object)s)), Width = 170
                },
                new Filter<MemberRow>("Mọi trình độ", (r, v) => r.Level == v!.ToString()) { Options = Rules.Enum<FitnessLevel>("level"), Width = 150 },
            },
            Actions = canRegister ? new() { new("Đăng ký thành viên", Icons.Add, () => PeopleForms.MemberAsync(null)) } : new(),
            RowActions =
            {
                new RowAction<MemberRow>("", "Xem chi tiết", r => MemberDetailAsync(r.Id)),
                new RowAction<MemberRow>(Icons.Edit, "Sửa hồ sơ", r => PeopleForms.MemberAsync(r.Entity)),
                new RowAction<MemberRow>(Icons.Money, "Đăng ký / gia hạn gói", async r => await SalesForms.SubscribeAsync(r.Id) != null),
                new RowAction<MemberRow>(Icons.Library, "Lớp học của thành viên", r => MemberClassesAsync(r.Id, r.FullName)),
            },
            Open = r => MemberDetailAsync(r.Id),
        });

        /// <summary>Hồ sơ đầy đủ của thành viên (gói, lớp, mục tiêu).</summary>
        public static async Task<bool> MemberDetailAsync(int memberId)
        {
            var m = await UiScope.Run(s => s.Members.GetAsync(memberId));
            if (m == null) return false;
            var (state, end, plan) = Labels.Membership(m);
            var sections = new List<InfoSection>
            {
                new("Thông tin", new List<(string, string)>
                {
                    ("Tên đăng nhập", m.User!.Username), ("Điện thoại", m.User.Phone), ("Email", m.User.Email),
                    ("Giới tính / tuổi", $"{Labels.Gender(m.Gender)} {Labels.Age(m.DateOfBirth)}"),
                    ("Chiều cao / cân nặng", $"{m.HeightCm?.ToString("0") ?? "?"} cm / {m.WeightKg?.ToString("0.#") ?? "?"} kg"),
                    ("Trình độ", Labels.Level(m.FitnessLevel)), ("Ngày tham gia", m.JoinDate.ToString("dd/MM/yyyy")),
                    ("Tài khoản", m.User.IsActive ? "Hoạt động" : "Đã khóa"),
                }),
                new("Gói thành viên", new List<(string, string)> { ("Trạng thái", $"{StatusBadge.Describe("membership", state).Label}{(end.HasValue ? $" – hạn {end:dd/MM/yyyy}" : "")}"), ("Gói", plan) }
                    .Concat(m.Subscriptions.OrderByDescending(s => s.StartDate).Take(5).Select(s => ($"{s.StartDate:dd/MM/yy}–{s.EndDate:dd/MM/yy}", $"{s.Plan?.Name} · {s.PricePaid:N0} ₫{(s.Status == SubscriptionStatus.Cancelled ? " (đã hủy)" : "")}"))).ToList()),
                new("Lớp đang học", m.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)
                    .Select(e => (e.Class!.Name, $"{e.Class.Sport?.Name} · từ {e.EnrolledAt:dd/MM/yyyy}")).DefaultIfEmpty(("—", "Chưa đăng ký lớp nào")).ToList()),
                new("Mục tiêu tập luyện", Body: m.Goals),
                new("Lưu ý sức khỏe", Body: m.HealthNotes),
            };
            await InfoDialog.ShowAsync(m.User.FullName, $"Thành viên từ {m.JoinDate:dd/MM/yyyy}", Icons.People, sections);
            return false;
        }

        /// <summary>Danh sách lớp của một thành viên: đăng ký hộ / hủy lớp.</summary>
        public static async Task<bool> MemberClassesAsync(int memberId, string name)
        {
            var page = new TablePage<EnrollmentRow>(new TableSpec<EnrollmentRow>
            {
                Title = "Lớp đã đăng ký",
                Unit = "lớp",
                EmptyIcon = Icons.Library,
                Load = async () => (await UiScope.Run(s => s.Classes.EnrollmentsOfMemberAsync(memberId))).Select(e => new EnrollmentRow { Entity = e }).ToList(),
                Search = r => r.ClassName + " " + r.CoachName,
                DefaultSort = nameof(EnrollmentRow.EnrolledAt),
                SortDirection = System.ComponentModel.ListSortDirection.Descending,
                Columns =
                {
                    new Col("Lớp", nameof(EnrollmentRow.ClassName), ColKind.TwoLine) { Path2 = nameof(EnrollmentRow.SportName), Bold = true, Width = Col.Star(2), MinWidth = 180 },
                    new Col("Huấn luyện viên", nameof(EnrollmentRow.CoachName)) { Width = Col.Star(1.4), MinWidth = 130, HideBelow = 700 },
                    new Col("Học phí", nameof(EnrollmentRow.Fee)) { Width = Col.Px(120), HideBelow = 820 },
                    new Col("Ngày đăng ký", nameof(EnrollmentRow.EnrolledText)) { SortPath = nameof(EnrollmentRow.EnrolledAt), Width = Col.Px(120) },
                    new Col("Trạng thái", nameof(EnrollmentRow.Status), ColKind.Badge) { BadgeKind = "enroll" },
                },
                Actions = { new("Đăng ký lớp", Icons.Add, () => SalesForms.EnrollAsync(memberId, null)) },
                RowActions =
                {
                    new RowAction<EnrollmentRow>(Icons.Close, "Hủy đăng ký lớp", async r =>
                    {
                        if (!await UiHost.ConfirmAsync("Hủy đăng ký lớp?", $"Hủy đăng ký lớp \"{r.ClassName}\" cho {name}? Học phí đã thu (nếu có) xử lý theo quy định hoàn phí.", "Hủy đăng ký")) return false;
                        await UiScope.Run(s => s.Classes.CancelEnrollmentAsync(r.Id));
                        UiHost.Toast($"Đã hủy đăng ký lớp \"{r.ClassName}\".");
                        return true;
                    }) { Danger = true, Visible = r => r.IsActive },
                },
            });
            await HostDialog.ShowAsync($"Lớp học – {name}", "Đăng ký hoặc hủy lớp cho thành viên", Icons.Library, page, 960, 520);
            return true;
        }

        // ------------------------------------------------------------------ ĐĂNG KÝ GÓI
        public static FrameworkElement Subscriptions() => new TablePage<SubscriptionRow>(new TableSpec<SubscriptionRow>
        {
            Title = "Đăng ký gói",
            Subtitle = "Lịch sử đăng ký / gia hạn gói thành viên",
            Unit = "lượt",
            EmptyIcon = Icons.Money,
            Load = async () => (await UiScope.Run(s => s.Memberships.SubscriptionsAsync())).Select(x => new SubscriptionRow { Entity = x }).ToList(),
            Search = r => $"{r.MemberName} {r.Username} {r.PlanName}",
            SearchPlaceholder = "Tìm theo thành viên, gói...",
            DefaultSort = nameof(SubscriptionRow.StartDate),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Thành viên", nameof(SubscriptionRow.MemberName), ColKind.Avatar) { Path2 = nameof(SubscriptionRow.Username), Width = Col.Star(1.8), MinWidth = 200 },
                new Col("Gói", nameof(SubscriptionRow.PlanName)) { Width = Col.Star(1.3), MinWidth = 130 },
                new Col("Thời hạn", nameof(SubscriptionRow.PeriodText)) { SortPath = nameof(SubscriptionRow.StartDate), Width = Col.Px(200), HideBelow = 760 },
                new Col("Giá", nameof(SubscriptionRow.PriceText)) { SortPath = nameof(SubscriptionRow.Price), Width = Col.Px(120), HideBelow = 900 },
                new Col("Trạng thái", nameof(SubscriptionRow.State), ColKind.Badge) { BadgeKind = "membership", Width = Col.Px(140) },
            },
            Filters =
            {
                new Filter<SubscriptionRow>("Mọi trạng thái", (r, v) => r.State == (string)v!)
                {
                    Options = new[] { "Active", "Expiring", "Upcoming", "Expired", "Cancelled" }.Select(s => (StatusBadge.Describe("membership", s).Label, (object)s)), Width = 160
                },
                new Filter<SubscriptionRow>("Mọi gói", (r, v) => r.PlanName == (string)v!)
                {
                    OptionsFrom = rows => rows.Select(r => r.PlanName).Distinct().OrderBy(x => x).Select(x => (x, (object)x)), Width = 180
                },
            },
            Actions = { new("Đăng ký / gia hạn gói", Icons.Add, async () => await SalesForms.SubscribeAsync(null) != null) },
            RowActions =
            {
                new RowAction<SubscriptionRow>(Icons.Refresh, "Gia hạn cho thành viên này", async r => await SalesForms.SubscribeAsync(r.Entity.MemberId, planId: r.Entity.PlanId) != null),
                new RowAction<SubscriptionRow>(Icons.Close, "Hủy gói", async r =>
                {
                    if (!await UiHost.ConfirmAsync("Hủy gói thành viên?", $"Hủy {r.PlanName} ({r.PeriodText}) của {r.MemberName}? Thành viên sẽ không check-in được bằng gói này.", "Hủy gói")) return false;
                    await UiScope.Run(s => s.Memberships.CancelSubscriptionAsync(r.Id, "Hủy tại quầy"));
                    UiHost.Toast("Đã hủy gói.");
                    return true;
                }) { Danger = true, Visible = r => r.State is "Active" or "Expiring" or "Upcoming" },
            },
        });

        // ------------------------------------------------------------------ THANH TOÁN
        public static FrameworkElement Payments(string title = "Thanh toán & hóa đơn")
        {
            var today = DateTime.Today;
            var month = new DateTime(today.Year, today.Month, 1);
            return new TablePage<PaymentRow>(new TableSpec<PaymentRow>
            {
                Title = title,
                Unit = "hóa đơn",
                EmptyIcon = Icons.Money,
                Load = async () => (await UiScope.Run(s => s.Payments.ListAsync(today.AddMonths(-12)))).Select(p => new PaymentRow { Entity = p }).ToList(),
                Search = r => $"{r.InvoiceNo} {r.MemberName} {r.Username} {r.Note}",
                SearchPlaceholder = "Tìm theo số hóa đơn, thành viên, nội dung...",
                DefaultSort = nameof(PaymentRow.PaidAt),
                SortDirection = System.ComponentModel.ListSortDirection.Descending,
                Columns =
                {
                    new Col("Hóa đơn", nameof(PaymentRow.InvoiceNo), ColKind.TwoLine) { Path2 = nameof(PaymentRow.PaidText), Bold = true, SortPath = nameof(PaymentRow.PaidAt), Width = Col.Px(160) },
                    new Col("Thành viên", nameof(PaymentRow.MemberName), ColKind.Avatar) { Path2 = nameof(PaymentRow.Username), Width = Col.Star(1.6), MinWidth = 190 },
                    new Col("Nội dung", nameof(PaymentRow.Note)) { Width = Col.Star(2), MinWidth = 160, HideBelow = 900 },
                    new Col("Loại", nameof(PaymentRow.Purpose), ColKind.Badge) { BadgeKind = "purpose", HideBelow = 1100 },
                    new Col("Số tiền", nameof(PaymentRow.AmountText)) { SortPath = nameof(PaymentRow.Amount), Width = Col.Px(120), Bold = true },
                    new Col("Hình thức", nameof(PaymentRow.Method), ColKind.Badge) { BadgeKind = "payment", HideBelow = 760 },
                },
                Filters =
                {
                    new Filter<PaymentRow>("Mọi thời gian (12 tháng)", (r, v) => (string)v! switch
                    {
                        "today" => r.PaidAt >= today,
                        "7d" => r.PaidAt >= today.AddDays(-6),
                        "month" => r.PaidAt >= month,
                        "lastmonth" => r.PaidAt >= month.AddMonths(-1) && r.PaidAt < month,
                        _ => true
                    })
                    { Options = new[] { ("Hôm nay", (object)"today"), ("7 ngày qua", "7d"), ("Tháng này", "month"), ("Tháng trước", "lastmonth") }, Width = 190 },
                    new Filter<PaymentRow>("Mọi hình thức", (r, v) => r.Method == v!.ToString()) { Options = Rules.Methods(true), Width = 160 },
                    new Filter<PaymentRow>("Mọi loại thu", (r, v) => r.Purpose == v!.ToString()) { Options = Rules.Enum<PaymentPurpose>("purpose"), Width = 160 },
                },
                Actions = { new("Ghi nhận thanh toán", Icons.Add, async () => await SalesForms.PaymentAsync(null) != null) },
                RowActions =
                {
                    new RowAction<PaymentRow>("", "In hóa đơn", async r => { await InvoicePrinter.PrintAsync(r.Id); return false; }),
                    new RowAction<PaymentRow>("", "Xuất hóa đơn (HTML)", async r => { await InvoicePrinter.ExportAsync(r.Id); return false; }),
                },
            });
        }

        // ------------------------------------------------------------------ YÊU CẦU HỖ TRỢ
        public static FrameworkElement Support() => new TablePage<SupportRow>(new TableSpec<SupportRow>
        {
            Title = "Yêu cầu hỗ trợ",
            Unit = "yêu cầu",
            EmptyIcon = Icons.Info,
            Load = async () => (await UiScope.Run(s => s.Support.ListAsync())).Select(x => new SupportRow { Entity = x }).ToList(),
            Search = r => $"{r.Subject} {r.Description} {r.MemberName}",
            SearchPlaceholder = "Tìm theo tiêu đề, nội dung, thành viên...",
            DefaultSort = nameof(SupportRow.CreatedAt),
            SortDirection = System.ComponentModel.ListSortDirection.Descending,
            Columns =
            {
                new Col("Yêu cầu", nameof(SupportRow.Subject), ColKind.TwoLine) { Path2 = nameof(SupportRow.Description), Bold = true, Width = Col.Star(2.5), MinWidth = 220 },
                new Col("Thành viên", nameof(SupportRow.MemberName)) { Width = Col.Star(1.2), MinWidth = 130, HideBelow = 760 },
                new Col("Người ghi nhận", nameof(SupportRow.CreatedBy)) { Width = Col.Star(1), MinWidth = 120, HideBelow = 1000 },
                new Col("Thời gian", nameof(SupportRow.CreatedText)) { SortPath = nameof(SupportRow.CreatedAt), Width = Col.Px(140), HideBelow = 640 },
                new Col("Trạng thái", nameof(SupportRow.Status), ColKind.Badge) { BadgeKind = "support" },
            },
            Filters = { new Filter<SupportRow>("Mọi trạng thái", (r, v) => r.Status == v!.ToString()) { Options = Rules.Enum<SupportStatus>("support"), Width = 160 } },
            Actions = { new("Ghi nhận yêu cầu", Icons.Add, () => SalesForms.SupportAsync(null)) },
            RowActions = { new RowAction<SupportRow>(Icons.Edit, "Xử lý / cập nhật", r => SalesForms.SupportAsync(r.Entity)) },
            Open = r => SalesForms.SupportAsync(r.Entity),
        });
    }

    /// <summary>In hoặc xuất hóa đơn (HTML dùng chung từ PaymentService.InvoiceHtml).</summary>
    public static class InvoicePrinter
    {
        private const string Address = "123 Nguyễn Văn Linh, Quận 7, TP. Hồ Chí Minh – Hotline 0909 123 456";

        public static async Task ExportAsync(int paymentId)
        {
            var p = await UiScope.Run(s => s.Payments.GetAsync(paymentId));
            var dlg = new SaveFileDialog { FileName = $"{p.InvoiceNo}.html", Filter = "Trang HTML (*.html)|*.html", Title = "Xuất hóa đơn" };
            if (dlg.ShowDialog() != true) return;
            await File.WriteAllTextAsync(dlg.FileName, PaymentService.InvoiceHtml(p, AppInfo.CenterName, Address), System.Text.Encoding.UTF8);
            UiHost.Toast($"Đã xuất hóa đơn ra {dlg.FileName}.");
        }

        public static async Task PrintAsync(int paymentId)
        {
            var p = await UiScope.Run(s => s.Payments.GetAsync(paymentId));
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;
            dlg.PrintDocument(((IDocumentPaginatorSource)BuildDocument(p)).DocumentPaginator, $"Hóa đơn {p.InvoiceNo}");
            UiHost.Toast($"Đã gửi hóa đơn {p.InvoiceNo} tới máy in.");
        }

        public static FlowDocument BuildDocument(Payment p)
        {
            var doc = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 13, PagePadding = new Thickness(48), ColumnWidth = 9999 };
            doc.Blocks.Add(new Paragraph(new Run(AppInfo.CenterName)) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.RoyalBlue, Margin = new Thickness(0) });
            doc.Blocks.Add(new Paragraph(new Run(Address)) { Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 16) });
            doc.Blocks.Add(new Paragraph(new Run($"HÓA ĐƠN THANH TOÁN  •  Số {p.InvoiceNo}  •  {p.PaidAt:dd/MM/yyyy HH:mm}")) { FontWeight = FontWeights.SemiBold });
            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(160) });
            table.Columns.Add(new TableColumn());
            var group = new TableRowGroup();
            foreach (var (k, v) in new[]
            {
                ("Khách hàng", $"{p.Member?.User?.FullName} ({p.Member?.User?.Username})"), ("Điện thoại", p.Member?.User?.Phone ?? ""),
                ("Nội dung", $"{PaymentService.PurposeName(p.Purpose)} – {p.Note}"), ("Hình thức", PaymentService.MethodName(p.Method)),
                ("Thu ngân", p.ReceivedBy?.FullName ?? "Thanh toán online"),
            })
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(k)) { Foreground = Brushes.Gray }) { Padding = new Thickness(0, 6, 0, 6), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(0, 6, 0, 6), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);
            doc.Blocks.Add(table);
            doc.Blocks.Add(new Paragraph(new Run($"Tổng cộng: {p.Amount:N0} ₫")) { FontSize = 18, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 16, 0, 0) });
            doc.Blocks.Add(new Paragraph(new Run("Cảm ơn quý khách đã sử dụng dịch vụ!")) { Foreground = Brushes.Gray });
            return doc;
        }
    }
}
