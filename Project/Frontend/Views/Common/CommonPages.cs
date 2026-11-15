using SportsCenter.BLL;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using SportsCenter.DAL.Models;
using SportsCenter.BLL.Services;

namespace Project.Frontend.Views.Common
{
    internal static class Ui2
    {
        public static T R<T>(string key) => (T)Application.Current.FindResource(key);
    }

    // =====================================================================
    // THÔNG BÁO
    // =====================================================================
    public static class NotificationsPage
    {
        public static FrameworkElement Create()
        {
            var canSend = Me.User.Role is UserRole.Coach or UserRole.Manager;
            async Task<bool> Open(NotificationRow r)
            {
                if (r.Entity.IsRead == false) { await ServiceScope.Run(s => s.Notifications.MarkReadAsync(r.Id)); await UiHost.RefreshNotificationsAsync(); }
                await InfoDialog.ShowAsync(r.Title, $"{StatusBadge.Describe("notify", r.Type).Label} · {r.Sender} · {r.TimeText}", Icons.Info,
                    new[] { new InfoSection("Nội dung", Body: r.Body) });
                return true;
            }
            var actions = new List<HeaderAction>
            {
                new("Đánh dấu đã đọc tất cả", Icons.Success, async () =>
                {
                    await ServiceScope.Run(s => s.Notifications.MarkAllReadAsync(Me.User.Id));
                    await UiHost.RefreshNotificationsAsync();
                    return true;
                }, Primary: !canSend),
            };
            if (canSend) actions.Add(new("Gửi thông báo / bài tập", Icons.Add, () => TrainingForms.SendNoticeAsync(Me.CoachId)));

            return new TablePage<NotificationRow>(new TableSpec<NotificationRow>
            {
                Title = "Thông báo",
                Unit = "thông báo",
                EmptyIcon = Icons.Info,
                EmptyText = "Không có thông báo",
                Load = async () => (await ServiceScope.Run(s => s.Notifications.ForUserAsync(Me.User.Id))).Select(n => new NotificationRow { Entity = n }).ToList(),
                Search = r => r.Title + " " + r.Body + " " + r.Sender,
                SearchPlaceholder = "Tìm theo tiêu đề, nội dung, người gửi...",
                DefaultSort = nameof(NotificationRow.CreatedAt),
                SortDirection = System.ComponentModel.ListSortDirection.Descending,
                Columns =
                {
                    new Col("Loại", nameof(NotificationRow.Type), ColKind.Badge) { BadgeKind = "notify", Width = Col.Px(120) },
                    new Col("Nội dung", nameof(NotificationRow.Title), ColKind.TwoLine) { Path2 = nameof(NotificationRow.Body), Bold = true, Width = Col.Star(3), MinWidth = 240 },
                    new Col("Người gửi", nameof(NotificationRow.Sender)) { Width = Col.Star(1), MinWidth = 120, HideBelow = 760 },
                    new Col("Thời gian", nameof(NotificationRow.TimeText)) { SortPath = nameof(NotificationRow.CreatedAt), Width = Col.Px(140), HideBelow = 620 },
                    new Col("Trạng thái", nameof(NotificationRow.ReadState), ColKind.Badge) { BadgeKind = "read", Width = Col.Px(110) },
                },
                Filters =
                {
                    new Filter<NotificationRow>("Mọi loại", (r, v) => r.Type == v?.ToString()) { Options = Rules.Enum<NotificationType>("notify") , Width = 150 },
                    new Filter<NotificationRow>("Mọi trạng thái", (r, v) => r.ReadState == (string)v!) { Options = new[] { ("Chưa đọc", (object)"Unread"), ("Đã đọc", "Read") }, Width = 150 },
                },
                Actions = actions,
                RowActions = { new RowAction<NotificationRow>("", "Xem", Open) },
                Open = Open,
            });
        }
    }

    // =====================================================================
    // TÀI KHOẢN CỦA TÔI
    // =====================================================================
    public class ProfilePage : UserControl
    {
        private readonly StackPanel _body = new();

        public ProfilePage()
        {
            Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _body.Children.Clear();
            _body.Children.Add(new TextBlock { Text = "Tài khoản của tôi", Style = Ui2.R<Style>("Text.H1"), Margin = new Thickness(0, 0, 0, 16) });
            try
            {
                Member? member = Me.MemberId is int mid ? await ServiceScope.Run(s => s.Members.GetAsync(mid)) : null;
                Coach? coach = Me.CoachId is int ? await ServiceScope.Run(s => s.Coaches.GetByUserIdAsync(Me.User.Id)) : null;
                var user = member?.User ?? coach?.User ?? Me.User;

                // Thẻ thông tin chính
                var head = new DockPanel();
                var avatar = new Border { Style = Ui2.R<Style>("Avatar"), Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Margin = new Thickness(0, 0, 16, 0),
                    Child = new TextBlock { Text = AppInfo.Initials(user.FullName), Style = Ui2.R<Style>("AvatarText"), FontSize = 22 } };
                DockPanel.SetDock(avatar, Dock.Left);
                head.Children.Add(avatar);
                var buttons = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(buttons, Dock.Right);
                if (member != null || coach != null)
                {
                    var edit = TablePage<object>.IconTextButton("Cập nhật hồ sơ", Icons.Edit, "Btn.Primary");
                    edit.Margin = new Thickness(8, 4, 0, 4);
                    edit.Click += async (_, _) =>
                    {
                        var ok = member != null ? await PeopleForms.MemberAsync(member, selfService: true) : await PeopleForms.CoachAsync(coach, selfService: true);
                        if (ok) await LoadAsync();
                    };
                    buttons.Children.Add(edit);
                }
                var pwd = TablePage<object>.IconTextButton("Đổi mật khẩu", "", "Btn.Secondary");
                pwd.Margin = new Thickness(8, 4, 0, 4);
                pwd.Click += async (_, _) => await AccountForms.ChangeOwnPasswordAsync(user.Id);
                buttons.Children.Add(pwd);
                head.Children.Add(buttons);
                var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                names.Children.Add(new TextBlock { Text = user.FullName, Style = Ui2.R<Style>("Text.H2") });
                var roleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                roleRow.Children.Add(new StatusBadge { Kind = "role", Status = user.Role.ToString() });
                roleRow.Children.Add(new TextBlock { Text = $"  @{user.Username}", Style = Ui2.R<Style>("Text.Secondary"), VerticalAlignment = VerticalAlignment.Center });
                names.Children.Add(roleRow);
                head.Children.Add(names);
                _body.Children.Add(Card(head));

                var rows = new List<(string, string)> { ("Số điện thoại", user.Phone), ("Email", user.Email), ("Ngày tạo tài khoản", user.CreatedAt.ToString("dd/MM/yyyy")) };
                if (member != null)
                {
                    var (state, end, plan) = Labels.Membership(member);
                    rows.AddRange(new[]
                    {
                        ("Gói thành viên", plan.Length > 0 ? $"{plan} – {StatusBadge.Describe("membership", state).Label}{(end.HasValue ? $" (hạn {end:dd/MM/yyyy})" : "")}" : "Chưa có gói"),
                        ("Giới tính / tuổi", $"{Labels.Gender(member.Gender)} {Labels.Age(member.DateOfBirth)}"),
                        ("Chiều cao / cân nặng", $"{member.HeightCm?.ToString("0") ?? "?"} cm / {member.WeightKg?.ToString("0.#") ?? "?"} kg"),
                        ("Trình độ", Labels.Level(member.FitnessLevel)),
                        ("Mục tiêu", member.Goals),
                        ("Lưu ý sức khỏe", member.HealthNotes),
                    });
                }
                if (coach != null)
                    rows.AddRange(new[]
                    {
                        ("Chuyên môn", coach.Specialty), ("Kinh nghiệm", $"{coach.YearsExperience} năm"), ("Chứng chỉ", coach.Certifications), ("Giới thiệu", coach.Bio),
                    });
                _body.Children.Add(Card(KeyValues(rows), "Thông tin chi tiết"));
            }
            catch (Exception ex) { UiHost.Toast($"Không tải được hồ sơ: {ex.Message}", ToastKind.Error); }
        }

        internal static Border Card(UIElement content, string? title = null)
        {
            var panel = new StackPanel();
            if (title != null) panel.Children.Add(new TextBlock { Text = title, Style = Ui2.R<Style>("Text.H3"), Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(content);
            return new Border { Style = Ui2.R<Style>("Card"), Margin = new Thickness(0, 0, 0, 16), Child = panel };
        }

        internal static Grid KeyValues(IEnumerable<(string Label, string Value)> rows)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (var (label, value) in rows)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, Style = Ui2.R<Style>("Text.Secondary"), Margin = new Thickness(0, 6, 12, 6) };
                var v = new TextBlock { Text = string.IsNullOrWhiteSpace(value) ? "—" : value, Style = Ui2.R<Style>("Text.Body"), Margin = new Thickness(0, 6, 0, 6) };
                Grid.SetRow(l, g.RowDefinitions.Count - 1); Grid.SetRow(v, g.RowDefinitions.Count - 1); Grid.SetColumn(v, 1);
                g.Children.Add(l); g.Children.Add(v);
            }
            return g;
        }
    }

}
