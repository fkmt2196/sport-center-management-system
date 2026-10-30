using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SportCenter.BLL.DTOs.Members;
using SportCenter.BLL.Interfaces;
using SportCenter.BLL.Services;
using SportCenter.DAL.Interfaces;
using SportCenter.DAL.Repositories;
using SportCenter.Domain.Models.Members;
using SportCenter.Wpf.Commands;
using SportCenter.Wpf.Services;
using SportCenter.Wpf.ViewModels.Members;
using SportCenter.Wpf.Views.Members;

internal static class Program
{
    private static int _passed;
    private static readonly TimeProvider Clock = new FixedClock();

    [STAThread]
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            Test("Member data is scoped; a second member has independent history and bookings", () =>
            {
                var service = CreateService();
                var first = service.GetDashboardAsync(1).GetAwaiter().GetResult();
                var second = service.GetDashboardAsync(2).GetAwaiter().GetResult();
                Check(first.Profile.Id == 1 && first.Attendance.Count == 8, "Member 1 data missing.");
                Check(second.Profile.Id == 2 && second.Attendance.Count == 0 && second.Progress.Count == 0, "History leaked between members.");
                var from = new DateOnly(2026, 10, 26);
                var firstBookings = service.GetScheduleAsync(1, from, from.AddDays(6)).GetAwaiter().GetResult().Where(x => x.IsRegistered).Select(x => x.Id);
                var secondBookings = service.GetScheduleAsync(2, from, from.AddDays(6)).GetAwaiter().GetResult().Where(x => x.IsRegistered).Select(x => x.Id);
                Check(!firstBookings.Intersect(secondBookings).Any(), "Member bookings are not scoped.");
            });
            Test("Invalid members, missing profiles and inverted date ranges are rejected", () =>
            {
                var service = CreateService();
                Throws<ArgumentOutOfRangeException>(() => service.GetDashboardAsync(0).GetAwaiter().GetResult());
                Throws<KeyNotFoundException>(() => service.GetDashboardAsync(99).GetAwaiter().GetResult());
                Throws<KeyNotFoundException>(() => service.GetScheduleAsync(99, new(2026, 10, 26), new(2026, 11, 1)).GetAwaiter().GetResult());
                Throws<ArgumentException>(() => service.GetScheduleAsync(1, new(2026, 11, 1), new(2026, 10, 26)).GetAwaiter().GetResult());
            });
            Test("Foreign-member data from a faulty repository is rejected", () =>
            {
                var service = new MemberDashboardService(new WrongMemberRepository());
                Throws<InvalidOperationException>(() => service.GetDashboardAsync(1).GetAwaiter().GetResult());
            });
            Test("Schedule uses inclusive date bounds and returns an empty future week", () =>
            {
                var service = CreateService();
                var day = new DateOnly(2026, 10, 26);
                var monday = service.GetScheduleAsync(1, day, day).GetAwaiter().GetResult();
                Check(monday.Count == 1 && DateOnly.FromDateTime(monday[0].StartsAt.DateTime) == day, "Date bounds are incorrect.");
                Check(service.GetScheduleAsync(1, day.AddDays(50), day.AddDays(56)).GetAwaiter().GetResult().Count == 0, "Sample data should not fabricate an unlimited schedule.");
            });
            Test("Cancellation is propagated", () =>
            {
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => CreateService().GetDashboardAsync(1, cancellation.Token).GetAwaiter().GetResult());
            });
            Test("Dashboard search, sport and registered filters compose and clear correctly", () =>
            {
                using var vm = CreateViewModel();
                vm.LoadAsync().GetAwaiter().GetResult();
                Check(vm.Classes.Count == 6 && vm.RegisteredCount == 2, "Initial schedule missing.");
                vm.SelectedSport = "Yoga";
                Check(vm.Classes.Count == 2, "Sport filter failed.");
                vm.SearchText = "PHỤC HỒI";
                Check(vm.Classes.Count == 1, "Vietnamese case-insensitive search failed.");
                vm.RegisteredOnly = true;
                Check(vm.Classes.Count == 1, "Registered filter failed.");
                vm.SearchText = "no such class";
                Check(vm.IsScheduleEmpty, "Filtered empty state missing.");
                vm.ClearFiltersCommand.Execute(null);
                Check(vm.Classes.Count == 6, "Clear filters failed.");
                vm.ShowClassCommand.Execute(vm.Classes[0]);
                Check(vm.HasSelectedClass, "Class details did not open.");
                vm.CloseClassCommand.Execute(null);
                Check(!vm.HasSelectedClass, "Class details did not close.");
            });
            Test("Week navigation loads new dates and refresh preserves active filters", () =>
            {
                using var vm = CreateViewModel();
                vm.LoadAsync().GetAwaiter().GetResult();
                var originalWeek = vm.WeekStart;
                vm.SelectedSport = "Gym";
                vm.NextWeekCommand.ExecuteAsync().GetAwaiter().GetResult();
                Check(vm.WeekStart == originalWeek.AddDays(7) && vm.Classes.Count == 2, "Next week did not reload with the sport filter.");
                vm.RefreshCommand.ExecuteAsync().GetAwaiter().GetResult();
                Check(vm.SelectedSport == "Gym" && vm.Classes.Count == 2, "Refresh lost filters.");
                vm.NextWeekCommand.ExecuteAsync().GetAwaiter().GetResult();
                Check(vm.IsScheduleEmpty && vm.RegisteredCount == 0, "Stale schedule remained visible.");
                vm.CurrentWeekCommand.ExecuteAsync().GetAwaiter().GetResult();
                Check(vm.WeekStart == originalWeek && vm.Classes.Count == 6, "Current week did not reset an unavailable sport filter.");
            });
            Test("Attendance statistics exclude excused sessions; history filters work", () =>
            {
                using var vm = CreateViewModel();
                vm.LoadAsync().GetAwaiter().GetResult();
                Check(vm.AttendedCount == 6 && vm.AttendanceRateLabel == "86%", "Attendance calculation is wrong.");
                Check(vm.TrainingHoursLabel == "5,8", "Training hours should include present sessions only.");
                vm.AttendanceFilter = "Vắng mặt";
                Check(vm.Attendance.Count == 1, "History filter failed.");
                Check(vm.Attendance[0].Record.Status == AttendanceStatus.Absent, "Wrong history row.");
                Check(vm.AttendedCount == 6, "Filtering should not change the overall statistics.");
            });
            Test("Expired, future, exhausted and absent memberships display correctly", () =>
            {
                using var expired = CreateViewModel(2);
                expired.LoadAsync().GetAwaiter().GetResult();
                Check(expired.MembershipStatus == "Đã hết hạn" && expired.RemainingSessions == 0, "Expired membership incorrect.");
                Check(expired.AttendanceRateLabel == "—" && expired.IsProgressEmpty, "Empty member data should be safe.");
                var today = new DateOnly(2026, 10, 30);
                foreach (var pair in new (MemberMembership? Plan, string Expected)[]
                {
                    (null, "Chưa đăng ký"),
                    (new(1, "Future", today.AddDays(1), today.AddDays(30), 10, 0, ""), "Chưa bắt đầu"),
                    (new(1, "Used", today.AddDays(-1), today, 10, 10, ""), "Đã hết lượt"),
                    (new(1, "Active", today, today, 10, 2, ""), "Đang hoạt động")
                })
                {
                    using var vm = new MemberDashboardViewModel(new SnapshotService(pair.Plan), new DemoMemberSession(), Clock);
                    vm.LoadAsync().GetAwaiter().GetResult();
                    Check(vm.MembershipStatus == pair.Expected, $"Incorrect membership state: {pair.Expected}");
                }
            });
            Test("Progress handles decreasing goals, completed goals and equal baseline/target", () =>
            {
                ProgressItemViewModel Item(decimal baseline, decimal current, decimal target) => new(new(1, "Test", "kg", baseline, current, target, new(2026, 10, 30), ""));
                Check(Math.Abs(Item(71, 69.2m, 68).CompletionPercent - 60) < .01, "Decreasing goal incorrect.");
                Check(Item(0, 200, 100).CompletionPercent == 100, "Progress overflow.");
                Check(Item(10, 5, 20).CompletionPercent == 0, "Progress underflow.");
                Check(Item(10, 10, 10).CompletionPercent == 100, "Equal baseline and goal incorrect.");
            });
            Test("A failed load exposes retry and a successful retry clears the error", () =>
            {
                using var vm = new MemberDashboardViewModel(new FailOnceService(CreateService()), new DemoMemberSession(), Clock);
                vm.LoadAsync().GetAwaiter().GetResult();
                Check(vm.HasError && !vm.IsBusy && !vm.IsScheduleEmpty, "Failed load state incorrect.");
                vm.RefreshCommand.ExecuteAsync().GetAwaiter().GetResult();
                Check(!vm.HasError && vm.Classes.Count == 6, "Retry failed to recover.");
            });
            Test("Async commands prevent duplicate execution", () =>
            {
                var gate = new TaskCompletionSource();
                var calls = 0;
                var command = new AsyncRelayCommand(async () => { calls++; await gate.Task; });
                var running = command.ExecuteAsync();
                command.ExecuteAsync().GetAwaiter().GetResult();
                Check(calls == 1 && !command.CanExecute(null), "Duplicate command accepted.");
                gate.SetResult();
                running.GetAwaiter().GetResult();
                Check(command.CanExecute(null), "Command remained disabled.");
            });

            var renderDirectory = args.Length == 2 && args[0] == "--render-dir" ? args[1] : null;
            Test("WPF views, styles, nested bindings and detail commands load at desktop/minimum widths", () => VerifyViews(renderDirectory));
            Console.WriteLine($"PASS: {_passed} member-module checks.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifyViews(string? renderDirectory)
    {
        var app = new SportCenter.Wpf.App();
        app.InitializeComponent();
        var trace = new BindingErrorListener();
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        using var vm = CreateViewModel();
        vm.LoadAsync().GetAwaiter().GetResult();
        var view = new MemberDashboardView
        {
            DataContext = vm, FontFamily = new FontFamily("Segoe UI"), FontSize = 14,
            Language = XmlLanguage.GetLanguage("vi-VN")
        };
        var root = new Border { Child = view, Background = (Brush)app.FindResource("BackgroundBrush") };
        foreach (var tab in new[] { "schedule", "history", "progress", "profile" })
        {
            vm.NavigateCommand.Execute(tab);
            Render(root, 1440, 900, renderDirectory, tab);
        }
        vm.NavigateCommand.Execute("schedule");
        Render(root, 1060, 720, renderDirectory, "schedule-small");
        var details = Descendants<Button>(view).First(button => Equals(button.Content, "Xem chi tiết"));
        Check(details.Command is not null && details.CommandParameter is ClassCardViewModel, "Nested card command binding missing.");
        details.Command!.Execute(details.CommandParameter);
        Check(vm.HasSelectedClass, "Card command did not reach the dashboard.");
        Render(root, 1060, 720, renderDirectory, "class-details");
        var closeButton = Descendants<Button>(view).First(button => Equals(button.Content, "Đóng"));
        Check(closeButton.Foreground is SolidColorBrush { Color: var foreground } && foreground == Colors.White,
            "Primary button foreground must contrast with its green background.");
        Check(Descendants<TextBlock>(closeButton).All(text => text.Foreground is SolidColorBrush brush && brush.Color == Colors.White),
            "Button label did not inherit its foreground.");
        vm.CloseClassCommand.Execute(null);
        foreach (var tab in new[] { "history", "progress", "profile" })
        {
            vm.NavigateCommand.Execute(tab);
            Render(root, 1060, 720, renderDirectory, tab + "-small");
        }
        vm.NavigateCommand.Execute("history");
        Render(root, 1060, 720, null, "nav-check");
        var nav = Descendants<Button>(view).First(button => Equals(button.Content, "Lịch sử tập luyện"));
        Check(Equals(nav.Background, app.FindResource("PrimaryLightBrush")), "Active navigation styling failed.");
        Check(trace.Errors.Count == 0, "WPF binding errors: " + string.Join(Environment.NewLine, trace.Errors));
        app.Shutdown();
    }

    private static void Render(FrameworkElement root, int width, int height, string? directory, string name)
    {
        root.Width = width;
        root.Height = height;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static MemberDashboardService CreateService() => new(new DemoMemberReadRepository(Clock));
    private static MemberDashboardViewModel CreateViewModel(int id = 1) => new(CreateService(), new DemoMemberSession(id), Clock);
    private static void Test(string name, Action action) { action(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 30, 3, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }

    private sealed class SnapshotService(MemberMembership? membership) : IMemberDashboardService
    {
        public Task<MemberDashboardData> GetDashboardAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MemberDashboardData(new(memberId, "Test Member", "test@example.com", "", new(2026, 1, 1)),
                membership, Array.Empty<MemberAttendance>(), Array.Empty<MemberProgress>()));
        public Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemberClassSession>>(Array.Empty<MemberClassSession>());
    }

    private sealed class FailOnceService(IMemberDashboardService inner) : IMemberDashboardService
    {
        private bool _fail = true;
        public Task<MemberDashboardData> GetDashboardAsync(int memberId, CancellationToken cancellationToken = default)
        {
            if (_fail) { _fail = false; throw new IOException("Simulated connection failure."); }
            return inner.GetDashboardAsync(memberId, cancellationToken);
        }
        public Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            inner.GetScheduleAsync(memberId, from, to, cancellationToken);
    }

    private sealed class WrongMemberRepository : IMemberReadRepository
    {
        public Task<MemberProfile?> GetProfileAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberProfile?>(new(2, "Other Member", "other@example.com", "", new(2026, 1, 1)));
        public Task<MemberMembership?> GetMembershipAsync(int memberId, CancellationToken cancellationToken = default) => Task.FromResult<MemberMembership?>(null);
        public Task<IReadOnlyList<MemberAttendance>> GetAttendanceAsync(int memberId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MemberAttendance>>(Array.Empty<MemberAttendance>());
        public Task<IReadOnlyList<MemberProgress>> GetProgressAsync(int memberId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MemberProgress>>(Array.Empty<MemberProgress>());
        public Task<IReadOnlyList<MemberClassSession>> GetScheduleAsync(int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MemberClassSession>>(Array.Empty<MemberClassSession>());
    }
}
