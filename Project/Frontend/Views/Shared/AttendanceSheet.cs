using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Shell;
using SportsCenter.Core.Models;
using SportsCenter.Core.Services;

namespace Project.Frontend.Views.Shared
{
    /// <summary>Hộp thoại điểm danh một buổi tập: mỗi học viên chọn Có mặt / Đi muộn / Vắng / Có phép + ghi chú.</summary>
    public static class AttendanceSheet
    {
        private static T R<T>(string k) => (T)Application.Current.FindResource(k);

        public static async Task<bool> ShowAsync(SessionRow session)
        {
            var roster = await UiScope.Run(s => s.Attendance.RosterAsync(session.Id));
            if (roster.Count == 0) { UiHost.Toast("Lớp chưa có học viên nào đăng ký.", ToastKind.Info); return false; }

            var statuses = new[] { AttendanceStatus.Present, AttendanceStatus.Late, AttendanceStatus.Absent, AttendanceStatus.Excused };
            var rows = new List<(RosterItem Item, Dictionary<AttendanceStatus, RadioButton> Radios, TextBox Notes)>();
            var list = new StackPanel();
            foreach (var (item, idx) in roster.Select((r, i) => (r, i)))
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 2), Background = idx % 2 == 0 ? Brushes.Transparent : R<Brush>("SurfaceAltBrush") };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 160 });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 120 });
                var name = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 6) };
                name.Children.Add(new Border { Style = R<Style>("Avatar"), Width = 30, Height = 30, Child = new TextBlock { Text = AppInfo.Initials(item.FullName), Style = R<Style>("AvatarText") } });
                name.Children.Add(new TextBlock { Text = item.FullName, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                row.Children.Add(name);

                var radios = new Dictionary<AttendanceStatus, RadioButton>();
                var group = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (var st in statuses)
                {
                    var rb = new RadioButton
                    {
                        Content = StatusBadge.Describe("attendance", st.ToString()).Label, GroupName = $"att{item.MemberId}",
                        Margin = new Thickness(0, 0, 14, 0), VerticalContentAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand,
                        IsChecked = (item.Status ?? AttendanceStatus.Present) == st
                    };
                    radios[st] = rb;
                    group.Children.Add(rb);
                }
                Grid.SetColumn(group, 1);
                row.Children.Add(group);
                var notes = new TextBox { Text = item.Notes, MinHeight = 32, Margin = new Thickness(4, 4, 8, 4), MaxLength = 500 };
                Ui.SetPlaceholder(notes, "Ghi chú");
                Grid.SetColumn(notes, 2);
                row.Children.Add(notes);
                rows.Add((item, radios, notes));
                list.Children.Add(row);
            }

            var body = new DockPanel { Margin = new Thickness(24, 0, 24, 8) };
            var tools = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var allPresent = new Button { Content = "Tất cả có mặt", Style = R<Style>("Btn.Secondary"), Height = 32 };
            allPresent.Click += (_, _) => rows.ForEach(r => r.Radios[AttendanceStatus.Present].IsChecked = true);
            DockPanel.SetDock(allPresent, Dock.Right);
            tools.Children.Add(allPresent);
            tools.Children.Add(new TextBlock
            {
                Text = $"{roster.Count} học viên · đã điểm danh trước đó: {roster.Count(r => r.Status != null)}", Style = R<Style>("Text.Secondary"), VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(tools, Dock.Top);
            body.Children.Add(tools);
            body.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 440 });

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Hủy", Style = R<Style>("Btn.Secondary"), Margin = new Thickness(0, 0, 10, 0), MinWidth = 90 };
            cancel.Click += (_, _) => UiHost.CloseDialog(false);
            var save = TablePage<object>.IconTextButton("Lưu điểm danh", "", "Btn.Primary");
            System.Windows.Automation.AutomationProperties.SetAutomationId(save, "SaveAttendance");
            save.Click += async (_, _) =>
            {
                save.IsEnabled = false;
                try
                {
                    var items = rows.Select(r => (r.Item.MemberId, r.Radios.First(x => x.Value.IsChecked == true).Key, r.Notes.Text)).ToList();
                    await UiScope.Run(s => s.Attendance.SaveAttendanceAsync(session.Id, items));
                    UiHost.CloseDialog(true);
                    UiHost.Toast($"Đã lưu điểm danh: {items.Count(i => i.Key is AttendanceStatus.Present or AttendanceStatus.Late)}/{items.Count} có mặt.");
                }
                catch (ValidationException ex) { UiHost.Toast(ex.Message, ToastKind.Warning); save.IsEnabled = true; }
                catch (Exception ex) { UiHost.Toast($"Không lưu được: {ex.Message}", ToastKind.Error); save.IsEnabled = true; }
            };
            footer.Children.Add(cancel);
            footer.Children.Add(save);

            var card = Project.Frontend.Forms.DialogChrome.Build($"Điểm danh – {session.ClassName}", $"{session.DateText} · {session.TimeText} · {session.RoomName}", Icons.Checklist, body, footer, 880);
            return await UiHost.ShowDialogAsync(card);
        }
    }
}
