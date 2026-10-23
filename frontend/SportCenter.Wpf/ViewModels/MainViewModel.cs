namespace SportCenter.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public string Title => "Sport Center Management";

    public string Description => "Hệ thống quản lý trung tâm thể thao";

    public string Status => "Khung dự án đã sẵn sàng. Các chức năng sẽ được triển khai ở bước tiếp theo.";

    public IReadOnlyList<string> PlannedModules { get; } = new[]
    {
        "Tài khoản và phân quyền: Manager, Coach, Member",
        "Lớp học, ca học và phân công Coach",
        "Đặt lịch, hủy lịch và kiểm soát điều kiện đăng ký",
        "Điểm danh và theo dõi tiến độ (mở rộng tùy chọn)"
    };
}
