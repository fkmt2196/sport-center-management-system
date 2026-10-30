# Module học viên và style dùng chung

Phần của Dương gồm giao diện học viên, lịch sử tập luyện, tiến độ, thông tin gói
tập và bộ style WPF dùng chung. Module chạy bằng dữ liệu mẫu qua interface để có
thể phát triển độc lập với database, đăng nhập và booking engine.

## Chạy trên Windows

Cần .NET 8 SDK và WPF runtime, hoặc Visual Studio với workload .NET desktop development.

```powershell
dotnet build SportCenterManagement.sln
dotnet run --project frontend/SportCenter.Wpf/SportCenter.Wpf.csproj
```

App mở thẳng Member Dashboard trong chế độ **Dữ liệu mẫu**, dùng `DemoMemberSession`
với MemberID 1. Đây là tài khoản demo phục vụ phát triển, không phải đăng nhập hay
cơ chế phân quyền. Dữ liệu chỉ đọc, không lưu thay đổi sau khi đóng app.

## Chức năng

- Lịch theo tuần dạng card; tìm theo lớp, bộ môn, HLV hoặc phòng; lọc bộ môn và
  lớp đã đăng ký; xem thông tin chi tiết lớp, HLV, phòng, thời gian và số chỗ.
- Lịch sử điểm danh có lọc trạng thái và nhận xét của Coach. Tỷ lệ có mặt không
  tính buổi nghỉ có phép; tổng giờ tập chỉ tính buổi có mặt.
- Tiến độ tập luyện: kết quả gần nhất, mục tiêu, mức ban đầu và nhận xét. Phần
  trăm mục tiêu hỗ trợ cả mục tiêu tăng lẫn giảm, giới hạn từ 0 đến 100.
- Gói tập: thời hạn, số buổi sử dụng/còn lại và trạng thái; hồ sơ học viên.
- Các trạng thái đang tải, không có dữ liệu, lỗi có thể thử lại.

Lịch mẫu có ba tuần: tuần trước, tuần hiện tại và tuần sau theo ngày chạy app.
Tuần xa hơn hiển thị trạng thái trống. Gói tập và lịch sử mẫu dùng ngày tương đối
để màn hình demo vẫn có dữ liệu khi chạy vào ngày khác.

## Nơi tích hợp với các thành viên khác

| Phần | Hợp đồng / file | Cách tích hợp |
| --- | --- | --- |
| Đăng nhập | `Services/IMemberSession.cs` | Truyền MemberID từ phiên đăng nhập vào ViewModel. Không lấy MemberID từ ô nhập của người dùng. |
| Dữ liệu SQL/EF | `DAL/Interfaces/IMemberReadRepository.cs` | Viết repository thật và thay `DemoMemberReadRepository` tại composition root `App.xaml.cs`. Mọi truy vấn cá nhân phải lọc theo MemberID. |
| Lịch và booking của thành viên 3 | `GetScheduleAsync(memberId, from, to)` | Trả các lớp trong khoảng ngày, lồng thông tin Coach/Room; `IsRegistered` chỉ phản ánh đặt chỗ của học viên hiện tại. |
| Điểm danh/tiến độ của thành viên 4 | `GetAttendanceAsync` / `GetProgressAsync` | Trả dữ liệu đã được Coach lưu. Module học viên chỉ xem dữ liệu. |
| Dashboard sau đăng nhập | `Views/Members/MemberDashboardView.xaml` | Nhúng UserControl và gán `MemberDashboardViewModel` bằng service/session thật; gọi `LoadAsync`. Dispose ViewModel khi đóng màn hình. |

Đặt/hủy chỗ, kiểm tra trùng lịch và transaction chống vượt sĩ số thuộc booking
engine của thành viên 3. Bản này có màn xem chi tiết và trạng thái đã đăng ký;
chưa gửi yêu cầu tạo/hủy booking. Hợp đồng dữ liệu lịch được tách riêng để nối
với engine đó mà không đổi layout card.

Các model đọc nằm trong `Domain/Models/Members`, chưa áp đặt entity hay schema
EF lên phần database của nhóm. Có thể ánh xạ entity thật sang các model này.
BLL gọi repository tuần tự để tương thích với một EF DbContext có cùng vòng đời.
App tham chiếu DAL chỉ để khởi tạo dependency; View và ViewModel gọi BLL.

## Style dùng chung

`App.xaml` nạp `Colors.xaml` trước `Controls.xaml` trong `Resources/Styles`.
Các màn hình khác dùng được ngay các style mặc định cho Button, TextBox, ComboBox,
CheckBox, DataGrid và ProgressBar.

Các key tái sử dụng: `PrimaryButtonStyle`, `SecondaryButtonStyle`,
`NavigationButtonStyle`, `SurfaceStyle`, `SectionTitleStyle`, `MutedTextStyle`
và `BoolToVisibility`. Ví dụ:

```xml
<Button Content="Lưu" Style="{StaticResource PrimaryButtonStyle}" />
<Border Style="{StaticResource SurfaceStyle}">
    <TextBlock Text="Thông tin" Style="{StaticResource SectionTitleStyle}" />
</Border>
```

## Kiểm tra

Bộ kiểm tra console không cần NuGet test framework, chạy trên Windows:

```powershell
dotnet run --project tests/SportCenter.MemberModule.Tests/SportCenter.MemberModule.Tests.csproj
```

Kiểm tra dữ liệu theo MemberID, giới hạn ngày, dữ liệu lẫn học viên, cancellation,
bộ lọc, điều hướng tuần, số liệu điểm danh, tình trạng gói tập, mục tiêu giảm,
lỗi/thử lại, chống bấm lặp command và binding WPF ở hai kích thước.
Thêm `-- --render-dir <thư-mục-ảnh>` để xuất các màn hình khi kiểm tra layout.
