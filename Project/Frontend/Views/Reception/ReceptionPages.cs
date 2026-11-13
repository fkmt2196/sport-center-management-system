using System.Windows;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using Project.Frontend.Views.Shared;

namespace Project.Frontend.Views.Reception
{
    public static class ReceptionPages
    {
        /// <summary>
        /// Check-in tại quầy: tìm thành viên, xem trạng thái/thời hạn gói và check-in.
        /// Nếu thành viên có buổi lớp sắp diễn ra, hệ thống tự điểm danh buổi đó.
        /// </summary>
        public static FrameworkElement CheckIn() => new TablePage<MemberRow>(new TableSpec<MemberRow>
        {
            Title = "Check-in",
            Subtitle = "Tìm thành viên (tên, SĐT, mã) rồi bấm Check-in",
            Unit = "thành viên",
            EmptyIcon = Icons.People,
            Load = async () => (await UiScope.Run(s => s.Members.ListAsync())).Select(m => MemberRow.From(m)).ToList(),
            Search = r => $"{r.FullName} {r.Username} {r.Phone}",
            SearchPlaceholder = "Nhập tên, số điện thoại hoặc tên đăng nhập của thành viên...",
            DefaultSort = nameof(MemberRow.FullName),
            Columns =
            {
                new Col("Thành viên", nameof(MemberRow.FullName), ColKind.Avatar) { Path2 = nameof(MemberRow.Username), Width = Col.Star(1.8), MinWidth = 210 },
                new Col("Điện thoại", nameof(MemberRow.Phone)) { Width = Col.Px(130), HideBelow = 760 },
                new Col("Gói", nameof(MemberRow.PlanName), ColKind.TwoLine) { Path2 = nameof(MemberRow.EndText), SortPath = nameof(MemberRow.MembershipEnd), Width = Col.Star(1.3), MinWidth = 150, HideBelow = 640 },
                new Col("Trạng thái gói", nameof(MemberRow.MembershipState), ColKind.Badge) { BadgeKind = "membership" },
            },
            Filters =
            {
                new Filter<MemberRow>("Mọi trạng thái gói", (r, v) => r.MembershipState == (string)v!)
                {
                    Options = new[] { "Active", "Expiring", "Expired", "None" }.Select(s => (StatusBadge.Describe("membership", s).Label, (object)s)), Width = 170
                },
            },
            Actions =
            {
                new("Lượt vào hôm nay", Icons.Clock, async () =>
                {
                    var list = await UiScope.Run(s => s.Attendance.CheckInsAsync(DateTime.Today));
                    await InfoDialog.ShowAsync("Lượt check-in hôm nay", $"{list.Count} lượt · {DateTime.Today:dd/MM/yyyy}", Icons.Checklist, new[]
                    {
                        new InfoSection("Danh sách", list.Select(c => (c.CheckInTime.ToString("HH:mm"), $"{c.Member?.User?.FullName} ({c.Member?.User?.Username})"))
                            .DefaultIfEmpty(("—", "Chưa có lượt check-in nào")).ToList())
                    });
                    return false;
                }, Primary: false),
                new("Đăng ký thành viên mới", Icons.Add, () => PeopleForms.MemberAsync(null)),
            },
            RowActions =
            {
                new RowAction<MemberRow>(Icons.Success, "Check-in", async r =>
                {
                    var message = await UiScope.Run(s => s.Attendance.CheckInAsync(r.Id));
                    UiHost.Toast(message, r.MembershipState == "Expiring" ? ToastKind.Warning : ToastKind.Success);
                    if (r.MembershipState == "Expiring") UiHost.Toast($"Gói của {r.FullName} sắp hết hạn ({r.EndText}) – nhắc thành viên gia hạn.", ToastKind.Info);
                    return false;
                }) { Visible = r => r.MembershipState is "Active" or "Expiring" },
                new RowAction<MemberRow>(Icons.Money, "Đăng ký / gia hạn gói", async r => await SalesForms.SubscribeAsync(r.Id) != null),
                new RowAction<MemberRow>(Icons.Library, "Đăng ký / hủy lớp hộ", r => OperationsPages.MemberClassesAsync(r.Id, r.FullName)),
                new RowAction<MemberRow>("", "Xem hồ sơ", r => OperationsPages.MemberDetailAsync(r.Id)),
            },
            Open = r => OperationsPages.MemberDetailAsync(r.Id),
        });
    }
}
