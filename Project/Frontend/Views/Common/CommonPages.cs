using SportsCenter.BLL;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Forms;
using Project.Frontend.Shell;
using SportsCenter.BLL.AI;
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

    // =====================================================================
    // TRỢ LÝ AI (chat)
    // =====================================================================
    public class AiAssistantPage : UserControl
    {
        private readonly IAiCoachAssistant _ai = AiAssistantFactory.Create();
        private readonly List<AiChatTurn> _history = new();
        private readonly StackPanel _messages = new() { Margin = new Thickness(0, 0, 8, 0) };
        private readonly ScrollViewer _scroll;
        private readonly TextBox _input = new() { AcceptsReturn = false, MinHeight = 44 };
        private readonly Button _send;
        private readonly ComboBox? _memberPicker;
        private string? _lastSuggestion;
        private Button? _savePlan;
        private bool _busy;

        public AiAssistantPage()
        {
            var isCoach = Me.User.Role == UserRole.Coach;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var status = new Border
            {
                CornerRadius = new CornerRadius(999), Padding = new Thickness(12, 4, 12, 4), VerticalAlignment = VerticalAlignment.Center,
                Background = Ui2.R<Brush>(_ai.IsOnline ? "SuccessSoftBrush" : "WarningSoftBrush"),
                Child = new TextBlock { Text = (_ai.IsOnline ? "● " : "○ ") + _ai.ProviderName, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ui2.R<Brush>(_ai.IsOnline ? "SuccessBrush" : "WarningBrush") }
            };
            DockPanel.SetDock(status, Dock.Right);
            header.Children.Add(status);
            var titles = new StackPanel();
            titles.Children.Add(new TextBlock { Text = isCoach ? "Trợ lý AI – gợi ý bài tập" : "Hỏi trợ lý AI", Style = Ui2.R<Style>("Text.H1") });
            titles.Children.Add(new TextBlock { Text = isCoach ? "Chọn học viên để AI gợi ý bài tập theo mục tiêu, trình độ và lịch sử tập." : "Hỏi về lịch tập, bài tập, gói tập và dịch vụ của trung tâm.",
                Style = Ui2.R<Style>("Text.Secondary"), Margin = new Thickness(0, 4, 0, 0) });
            header.Children.Add(titles);
            root.Children.Add(header);

            // Thanh công cụ: chọn học viên (HLV) hoặc câu hỏi nhanh (thành viên)
            var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(tools, 1);
            if (isCoach)
            {
                _memberPicker = new ComboBox { Width = 320, Margin = new Thickness(0, 0, 10, 0), ToolTip = "Học viên" };
                System.Windows.Automation.AutomationProperties.SetAutomationId(_memberPicker, "AiMemberPicker");
                tools.Children.Add(_memberPicker);
                var suggest = TablePage<object>.IconTextButton("Gợi ý bài tập", "", "Btn.Primary");
                System.Windows.Automation.AutomationProperties.SetAutomationId(suggest, "AiSuggest");
                suggest.Click += async (_, _) => await SuggestAsync();
                tools.Children.Add(suggest);
                _savePlan = TablePage<object>.IconTextButton("Lưu thành kế hoạch tập", Icons.Edit, "Btn.Secondary");
                _savePlan.Margin = new Thickness(10, 0, 0, 0);
                _savePlan.IsEnabled = false;
                _savePlan.Click += async (_, _) =>
                {
                    if (_lastSuggestion != null && Me.CoachId is int cid)
                        await TrainingForms.PlanAsync(cid, null, ListPage.SelectedValue(_memberPicker) as int?, _lastSuggestion, "Theo gợi ý của trợ lý AI");
                };
                tools.Children.Add(_savePlan);
            }
            else
            {
                foreach (var q in new[] { "Gợi ý bài tập phù hợp với tôi", "Trung tâm mở cửa mấy giờ?", "Có những gói tập nào?", "Lịch học của tôi thế nào?", "Nên ăn gì sau buổi tập?" })
                {
                    var chip = new Button { Content = q, Style = Ui2.R<Style>("Btn.Secondary"), Height = 32, Margin = new Thickness(0, 0, 8, 8), FontWeight = FontWeights.Normal };
                    chip.Click += async (_, _) => await AskAsync(q);
                    tools.Children.Add(chip);
                }
            }
            root.Children.Add(tools);

            _scroll = new ScrollViewer { Content = _messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16) };
            var chatCard = new Border { Style = Ui2.R<Style>("Card"), Padding = new Thickness(0), Child = _scroll };
            Grid.SetRow(chatCard, 2);
            root.Children.Add(chatCard);

            var inputRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(inputRow, 3);
            _send = TablePage<object>.IconTextButton("Gửi", "", "Btn.Primary");
            _send.Height = 44;
            _send.Margin = new Thickness(10, 0, 0, 0);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_send, "AiSend");
            _send.Click += async (_, _) => await AskAsync(_input.Text);
            DockPanel.SetDock(_send, Dock.Right);
            inputRow.Children.Add(_send);
            Ui.SetPlaceholder(_input, isCoach ? "Hỏi thêm, vd. \"Thay squat bằng bài khác vì học viên đau gối\"" : "Nhập câu hỏi của bạn...");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_input, "AiInput");
            _input.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await AskAsync(_input.Text); } };
            inputRow.Children.Add(_input);
            root.Children.Add(inputRow);
            Content = root;

            AddBubble(false, isCoach
                ? "Xin chào! Chọn một học viên rồi bấm \"Gợi ý bài tập\". Mình sẽ dựa trên mục tiêu, trình độ, lưu ý sức khỏe và kết quả tập gần đây của học viên."
                : $"Chào {Me.User.FullName}! Mình có thể gợi ý bài tập, trả lời về lịch học, gói tập và dịch vụ của {AppInfo.CenterName}.");
            Loaded += async (_, _) => await LoadMembersAsync();
        }

        private async Task LoadMembersAsync()
        {
            if (_memberPicker == null || Me.CoachId is not int cid || _memberPicker.Items.Count > 0) return;
            var members = await ServiceScope.Run(s => s.Coaches.MyMembersAsync(cid));
            ListPage.FillFilter(_memberPicker, "— Chọn học viên —", members.Select(m => ($"{m.Member.User!.FullName} · {string.Join(", ", m.Classes)}", (object)m.Member.Id)));
        }

        private async Task<AiMemberProfile?> ProfileAsync()
        {
            var memberId = _memberPicker != null ? ListPage.SelectedValue(_memberPicker) as int? : Me.MemberId;
            return memberId is int id ? await ServiceScope.Run(s => s.AiContext.BuildProfileAsync(id)) : null;
        }

        private async Task SuggestAsync()
        {
            var profile = await ProfileAsync();
            if (profile == null) { UiHost.Toast("Hãy chọn học viên trước.", ToastKind.Warning); return; }
            await RunAsync($"Gợi ý bài tập cho {profile.Name}", async () =>
            {
                var answer = await _ai.SuggestWorkoutAsync(profile, null);
                _lastSuggestion = answer;
                if (_savePlan != null) _savePlan.IsEnabled = true;
                return answer;
            });
        }

        private async Task AskAsync(string question)
        {
            question = question.Trim();
            if (question.Length == 0) return;
            _input.Clear();
            var profile = await ProfileAsync();
            var info = await ServiceScope.Run(s => s.AiContext.CenterInfoAsync());
            await RunAsync(question, async () =>
            {
                var answer = await _ai.AskAsync(question, profile, info, _history.ToList());
                if (_memberPicker != null) { _lastSuggestion = answer; if (_savePlan != null) _savePlan.IsEnabled = true; }
                return answer;
            });
        }

        private async Task RunAsync(string userText, Func<Task<string>> work)
        {
            if (_busy) return;
            _busy = true; _send.IsEnabled = false;
            AddBubble(true, userText);
            var thinking = AddBubble(false, "Đang suy nghĩ...");
            try
            {
                var answer = await work();
                _messages.Children.Remove(thinking);
                AddBubble(false, answer);
                _history.Add(new AiChatTurn(true, userText));
                _history.Add(new AiChatTurn(false, answer));
            }
            catch (Exception ex)
            {
                _messages.Children.Remove(thinking);
                AddBubble(false, $"Xin lỗi, đã xảy ra lỗi: {ex.Message}");
            }
            finally { _busy = false; _send.IsEnabled = true; _input.Focus(); }
        }

        private Border AddBubble(bool isUser, string text)
        {
            var bubble = new Border
            {
                Background = Ui2.R<Brush>(isUser ? "PrimaryBrush" : "NeutralSoftBrush"),
                CornerRadius = isUser ? new CornerRadius(14, 14, 4, 14) : new CornerRadius(14, 14, 14, 4),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(isUser ? 80 : 0, 0, isUser ? 0 : 80, 10),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left, MaxWidth = 760,
                Child = new TextBox
                {
                    Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                    Foreground = Ui2.R<Brush>(isUser ? "TextOnPrimaryBrush" : "TextBrush"), Padding = new Thickness(0), MinHeight = 0,
                    Template = PlainTextTemplate()
                }
            };
            _messages.Children.Add(bubble);
            _scroll.ScrollToEnd();
            return bubble;
        }

        /// <summary>TextBox chỉ đọc không viền (cho phép bôi đen/copy nội dung trả lời).</summary>
        private static ControlTemplate PlainTextTemplate()
        {
            var t = new ControlTemplate(typeof(TextBox));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            t.VisualTree = host;
            return t;
        }
    }
}
