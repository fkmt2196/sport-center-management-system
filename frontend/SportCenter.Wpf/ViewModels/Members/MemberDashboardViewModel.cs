using System.Collections.ObjectModel;
using System.Globalization;
using SportCenter.BLL.DTOs.Members;
using SportCenter.BLL.Interfaces;
using SportCenter.Domain.Models.Members;
using SportCenter.Wpf.Commands;
using SportCenter.Wpf.Services;

namespace SportCenter.Wpf.ViewModels.Members;

public sealed class MemberDashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IMemberDashboardService _service;
    private readonly IMemberSession _session;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<ClassCardViewModel> _allClasses = new();
    private readonly List<AttendanceItemViewModel> _allAttendance = new();
    private MemberDashboardData? _data;
    private DateOnly _weekStart;
    private string _selectedTab = "schedule";
    private string _searchText = "";
    private string _selectedSport = "Tất cả bộ môn";
    private string _attendanceFilter = "Tất cả trạng thái";
    private bool _registeredOnly;
    private bool _isBusy;
    private string _errorMessage = "";
    private ClassCardViewModel? _selectedClass;
    private bool _disposed;

    public MemberDashboardViewModel(IMemberDashboardService service, IMemberSession session, TimeProvider? clock = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _clock = clock ?? TimeProvider.System;
        _weekStart = Monday(Today);
        RefreshCommand = new(LoadAsync, () => !IsBusy);
        PreviousWeekCommand = new(() => ChangeWeekAsync(-7), () => !IsBusy);
        NextWeekCommand = new(() => ChangeWeekAsync(7), () => !IsBusy);
        CurrentWeekCommand = new(() => ChangeWeekAsync(null), () => !IsBusy);
        NavigateCommand = new(parameter => SelectTab(parameter as string ?? "schedule"));
        ShowClassCommand = new(parameter => SelectedClass = parameter as ClassCardViewModel);
        CloseClassCommand = new(_ => SelectedClass = null);
        ClearFiltersCommand = new(_ => { SearchText = ""; SelectedSport = Sports[0]; RegisteredOnly = false; });
    }

    public ObservableCollection<ClassCardViewModel> Classes { get; } = new();
    public ObservableCollection<AttendanceItemViewModel> Attendance { get; } = new();
    public ObservableCollection<ProgressItemViewModel> Progress { get; } = new();
    public ObservableCollection<string> Sports { get; } = new() { "Tất cả bộ môn" };
    public IReadOnlyList<string> AttendanceFilters { get; } = new[] { "Tất cả trạng thái", "Có mặt", "Vắng mặt", "Có phép" };
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand PreviousWeekCommand { get; }
    public AsyncRelayCommand NextWeekCommand { get; }
    public AsyncRelayCommand CurrentWeekCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand ShowClassCommand { get; }
    public RelayCommand CloseClassCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
    public bool IsDemo => _session.IsDemo;
    public string MemberName => _data?.Profile.FullName ?? "Học viên";
    public string MemberInitial => MemberName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?[..1] ?? "M";
    public string MemberCode => $"HV{_session.MemberId:0000}";
    public string Greeting => $"Chào {MemberName.Split(' ').Last()}, sẵn sàng tập luyện?";
    public string TodayLabel => _clock.GetLocalNow().ToString("dddd, dd 'tháng' MM, yyyy", CultureInfo.GetCultureInfo("vi-VN"));
    public string Email => _data?.Profile.Email ?? "—";
    public string Phone => _data?.Profile.Phone ?? "—";
    public string JoinedLabel => _data is null ? "—" : _data.Profile.JoinedOn.ToString("dd/MM/yyyy");
    public string WeekLabel => $"{_weekStart:dd/MM} – {_weekStart.AddDays(6):dd/MM/yyyy}";
    public DateOnly WeekStart => _weekStart;
    public string ClassCountLabel => $"{Classes.Count} lớp phù hợp";
    public int RegisteredCount => _allClasses.Count(item => item.Session.IsRegistered);
    public int AttendedCount => _allAttendance.Count(item => item.Record.Status == AttendanceStatus.Present);
    public string AttendanceRateLabel
    {
        get
        {
            var counted = _allAttendance.Count(item => item.Record.Status != AttendanceStatus.Excused);
            return counted == 0 ? "—" : $"{100d * AttendedCount / counted:0}%";
        }
    }
    public string TrainingHoursLabel => (_allAttendance.Where(item => item.Record.Status == AttendanceStatus.Present)
        .Sum(item => item.Record.DurationMinutes) / 60d).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));
    public string PlanName => _data?.Membership?.PlanName ?? "Chưa có gói tập";
    public bool HasMembership => _data?.Membership is not null;
    public int RemainingSessions => Math.Max(0, (_data?.Membership?.TotalSessions ?? 0) - (_data?.Membership?.UsedSessions ?? 0));
    public string MembershipPeriod => _data?.Membership is { } plan ? $"{plan.StartsOn:dd/MM/yyyy} – {plan.EndsOn:dd/MM/yyyy}" : "—";
    public string MembershipDescription => _data?.Membership?.Description ?? "Liên hệ trung tâm để được tư vấn gói tập phù hợp.";
    public string MembershipUsageLabel => _data?.Membership is { } plan ? $"Đã sử dụng {plan.UsedSessions}/{plan.TotalSessions} buổi" : "Chưa có lượt tập";
    public double MembershipUsagePercent => _data?.Membership is { TotalSessions: > 0 } plan
        ? Math.Clamp(100d * plan.UsedSessions / plan.TotalSessions, 0, 100) : 0;
    public string MembershipStatus => _data?.Membership switch
    {
        null => "Chưa đăng ký",
        var plan when Today < plan.StartsOn => "Chưa bắt đầu",
        var plan when Today > plan.EndsOn => "Đã hết hạn",
        _ when RemainingSessions == 0 => "Đã hết lượt",
        _ => "Đang hoạt động"
    };
    public string MembershipExpiryLabel => _data?.Membership is { } plan
        ? Today > plan.EndsOn ? $"Đã hết hạn từ {plan.EndsOn:dd/MM/yyyy}"
            : $"Hạn sử dụng: {plan.EndsOn:dd/MM/yyyy}" : "Chưa có ngày hết hạn";

    public bool IsScheduleTab => _selectedTab == "schedule";
    public bool IsHistoryTab => _selectedTab == "history";
    public bool IsProgressTab => _selectedTab == "progress";
    public bool IsProfileTab => _selectedTab == "profile";
    public string PageTitle => _selectedTab switch
    {
        "history" => "Lịch sử tập luyện",
        "progress" => "Tiến độ của bạn",
        "profile" => "Gói tập & hồ sơ",
        _ => "Lịch học của bạn"
    };

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RefreshCommand.NotifyCanExecuteChanged();
            PreviousWeekCommand.NotifyCanExecuteChanged();
            NextWeekCommand.NotifyCanExecuteChanged();
            CurrentWeekCommand.NotifyCanExecuteChanged();
            NotifyEmptyStates();
        }
    }
    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsScheduleEmpty => !IsBusy && Classes.Count == 0 && !HasError;
    public bool IsAttendanceEmpty => !IsBusy && Attendance.Count == 0 && !HasError;
    public bool IsProgressEmpty => !IsBusy && Progress.Count == 0 && !HasError;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) FilterClasses(); }
    }
    public string SelectedSport
    {
        get => _selectedSport;
        set { if (SetProperty(ref _selectedSport, value)) FilterClasses(); }
    }
    public bool RegisteredOnly
    {
        get => _registeredOnly;
        set { if (SetProperty(ref _registeredOnly, value)) FilterClasses(); }
    }
    public string AttendanceFilter
    {
        get => _attendanceFilter;
        set { if (SetProperty(ref _attendanceFilter, value)) FilterAttendance(); }
    }
    public ClassCardViewModel? SelectedClass
    {
        get => _selectedClass;
        private set { if (SetProperty(ref _selectedClass, value)) OnPropertyChanged(nameof(HasSelectedClass)); }
    }
    public bool HasSelectedClass => SelectedClass is not null;

    public async Task LoadAsync()
    {
        if (IsBusy || _disposed) return;
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            _data = await _service.GetDashboardAsync(_session.MemberId, _lifetime.Token);
            _allAttendance.Clear();
            _allAttendance.AddRange(_data.Attendance.Select(item => new AttendanceItemViewModel(item)));
            FilterAttendance();
            Progress.Clear();
            foreach (var item in _data.Progress) Progress.Add(new(item));
            NotifyProfile();
            await LoadScheduleCoreAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải dữ liệu học viên. Vui lòng kiểm tra kết nối và nhấn Thử lại.";
        }
        finally { IsBusy = false; }
    }

    private async Task ChangeWeekAsync(int? days)
    {
        if (IsBusy || _disposed) return;
        _weekStart = days.HasValue ? _weekStart.AddDays(days.Value) : Monday(Today);
        OnPropertyChanged(nameof(WeekLabel));
        OnPropertyChanged(nameof(WeekStart));
        IsBusy = true;
        ErrorMessage = "";
        try { await LoadScheduleCoreAsync(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { ErrorMessage = "Không thể tải lịch học. Vui lòng nhấn Thử lại."; }
        finally { IsBusy = false; }
    }

    private async Task LoadScheduleCoreAsync()
    {
        SelectedClass = null;
        _allClasses.Clear();
        FilterClasses();
        OnPropertyChanged(nameof(RegisteredCount));
        var sessions = await _service.GetScheduleAsync(_session.MemberId, _weekStart, _weekStart.AddDays(6), _lifetime.Token);
        _allClasses.AddRange(sessions.Select(item => new ClassCardViewModel(item, _clock.GetLocalNow())));
        var sport = SelectedSport;
        Sports.Clear();
        Sports.Add("Tất cả bộ môn");
        foreach (var item in sessions.Select(item => item.Sport).Distinct().OrderBy(item => item)) Sports.Add(item);
        SelectedSport = Sports.Contains(sport) ? sport : Sports[0];
        FilterClasses();
        OnPropertyChanged(nameof(RegisteredCount));
    }

    private void SelectTab(string tab)
    {
        if (tab is not ("schedule" or "history" or "progress" or "profile")) return;
        _selectedTab = tab;
        SelectedClass = null;
        foreach (var name in new[] { nameof(IsScheduleTab), nameof(IsHistoryTab), nameof(IsProgressTab), nameof(IsProfileTab), nameof(PageTitle) })
            OnPropertyChanged(name);
    }

    private void FilterClasses()
    {
        Classes.Clear();
        var query = (SearchText ?? "").Trim();
        foreach (var card in _allClasses.Where(card => (!RegisteredOnly || card.Session.IsRegistered)
            && (SelectedSport == Sports.FirstOrDefault() || card.Session.Sport == SelectedSport)
            && (query.Length == 0 || $"{card.Session.Name} {card.Session.Coach.FullName} {card.Session.Room.Name} {card.Session.Sport}"
                .Contains(query, StringComparison.CurrentCultureIgnoreCase))))
            Classes.Add(card);
        OnPropertyChanged(nameof(ClassCountLabel));
        NotifyEmptyStates();
    }

    private void FilterAttendance()
    {
        Attendance.Clear();
        foreach (var item in _allAttendance.Where(item => AttendanceFilter == AttendanceFilters[0] || item.StatusLabel == AttendanceFilter))
            Attendance.Add(item);
        NotifyEmptyStates();
    }

    private void NotifyEmptyStates()
    {
        OnPropertyChanged(nameof(IsScheduleEmpty));
        OnPropertyChanged(nameof(IsAttendanceEmpty));
        OnPropertyChanged(nameof(IsProgressEmpty));
    }

    private void NotifyProfile()
    {
        foreach (var name in new[] { nameof(MemberName), nameof(MemberInitial), nameof(Greeting), nameof(Email), nameof(Phone),
            nameof(JoinedLabel), nameof(AttendedCount), nameof(AttendanceRateLabel), nameof(TrainingHoursLabel), nameof(PlanName),
            nameof(HasMembership), nameof(RemainingSessions), nameof(MembershipPeriod), nameof(MembershipDescription),
            nameof(MembershipUsageLabel), nameof(MembershipUsagePercent), nameof(MembershipStatus), nameof(MembershipExpiryLabel) })
            OnPropertyChanged(name);
    }

    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
