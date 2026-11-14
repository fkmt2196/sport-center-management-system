# FitZone Sports Center – Hệ thống quản lý trung tâm thể thao

Ứng dụng WPF (.NET 8) quản lý trung tâm thể thao/tập luyện với 4 vai trò: **Quản lý**, **Huấn luyện viên**, **Thành viên**, **Lễ tân**.

## Cấu trúc – kiến trúc 3 tầng

```
Project.slnx
├── Project/                      # 1. PRESENTATION (WPF) – chỉ tham chiếu SportsCenter.BLL
│   └── Frontend/                 #    Themes, Controls (TablePage, Pager, BarChart...), Forms, Shell, Views theo vai trò
├── SportsCenter.BLL/             # 2. BUSINESS LOGIC – chỉ tham chiếu SportsCenter.DAL
│   ├── ServiceScope.cs           #    Điểm vào duy nhất cho giao diện: ServiceScope.Run(s => s.Members.ListAsync())
│   │                             #    + DatabaseInitializer (migrate/seed khi khởi động)
│   ├── Services/                 #    Nghiệp vụ + kiểm tra quyền theo vai trò (Authorization), PasswordHasher
│   └── AI/                       #    IAiCoachAssistant: ClaudeAiAssistant / TemplateAiAssistant (gợi ý mẫu)
├── SportsCenter.DAL/             # 3. DATA ACCESS – không tham chiếu tầng nào phía trên
│   ├── Models/                   #    Entity: User, Member, Coach, Sport, Room, TrainingClass, ClassSession,
│   │                             #      Enrollment, Attendance, CheckIn, MembershipPlan, Subscription, Payment,
│   │                             #      TrainingPlan, WorkoutLog, ProgressReview, Notification, SupportRequest, AuditLog
│   └── Data/                     #    SportsCenterContext (audit log tự động), DbConfig, DbSeeder, Migrations, AppSession
└── Project.ArchitectureTests/    # Test kiến trúc (NetArchTest) giữ đúng ranh giới 3 tầng
```

Luồng gọi: **Giao diện → `ServiceScope` (BLL) → Service → `SportsCenterContext` (DAL) → SQL Server**.

### Quy tắc phân tầng

Được **compiler** đảm bảo:

- Hướng tham chiếu project: `Project → SportsCenter.BLL → SportsCenter.DAL` (DAL không thể gọi lên BLL/giao diện).
- `SportsCenterContext`, `DbConfig`, `DbSeeder` là `internal` trong DAL, chỉ BLL thấy được (`InternalsVisibleTo`).
  Giao diện không thể tự tạo DbContext hay truy vấn CSDL.
- Constructor của các service là `internal`: giao diện chỉ lấy service qua `ServiceScope`.

Được **`Project.ArchitectureTests`** kiểm tra thêm (`dotnet test Project.slnx`):

- Giao diện không dùng `SportsCenter.DAL.Data`, `Microsoft.EntityFrameworkCore`, `Microsoft.Data.SqlClient`, `System.Data`.
- BLL không dùng WPF (`System.Windows`, `Microsoft.Win32`) hay `Project.*`.
- DAL không dùng BLL hay WPF; `Models` là entity thuần (không phụ thuộc DbContext/EF).
- `AI` không phụ thuộc `Services`; DbContext/DbConfig/DbSeeder vẫn là `internal`; không service nào có constructor public nhận DbContext.

Giao diện được dùng entity trong `SportsCenter.DAL.Models` để hiển thị/nhập liệu (tham chiếu bắc cầu qua BLL).
Thêm chức năng mới: viết truy vấn trong service ở BLL, thêm thuộc tính vào `ServiceScope` nếu là service mới, rồi gọi
từ giao diện qua `ServiceScope.Run(...)`.

## Yêu cầu

- .NET 8 SDK
- SQL Server (instance mặc định `localhost`, đăng nhập Windows). Đổi chuỗi kết nối bằng biến môi trường
  `SPORTSCENTER_DB`, ví dụ: `Server=.\SQLEXPRESS;Database=SportsCenterDb;Trusted_Connection=True;TrustServerCertificate=True;`
- (Tùy chọn) `dotnet-ef` để quản lý migration: `dotnet tool install -g dotnet-ef`

## Chạy thử

```powershell
dotnet build Project.slnx
dotnet run --project Project
```

Ở môi trường **Development** (mặc định khi chạy bản Debug, hoặc đặt `DOTNET_ENVIRONMENT=Development`) ứng dụng tự
áp migration và **seed dữ liệu mẫu** tiếng Việt nếu CSDL `SportsCenterDb` còn trống.

**Tài khoản mẫu:** xem file `ACCOUNTS.md` (không đưa lên git). Mật khẩu chung: `123456`.

| Vai trò | Tài khoản ví dụ |
|---|---|
| Quản lý | `manager` |
| Lễ tân | `letan`, `letan2` |
| Huấn luyện viên | `hlv01` … `hlv08` |
| Thành viên | `hv001` … `hv060` (hv001–hv003: gói sắp hết hạn) |

Thành viên mới cũng có thể tự **đăng ký tài khoản** ngay ở màn hình đăng nhập.

## Trợ lý AI

Tính năng AI nằm sau interface `IAiCoachAssistant` (`SportsCenter.BLL/AI`):

- Có biến môi trường `ANTHROPIC_API_KEY` → gọi Claude (`claude-opus-5-5`) qua SDK chính thức của Anthropic,
  bật server-side fallback khi mô hình chính từ chối trả lời.
- Không có API key (hoặc gọi API lỗi) → `TemplateAiAssistant` trả gợi ý mẫu theo mục tiêu, trình độ, chấn thương,
  lịch sử tập — ứng dụng vẫn chạy bình thường.

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."   # rồi chạy lại ứng dụng
```

## Migration

```powershell
# Tạo migration mới sau khi đổi Models
dotnet ef migrations add <Ten> --project SportsCenter.DAL --output-dir Data/Migrations
# Áp vào CSDL
dotnet ef database update --project SportsCenter.DAL
# Xóa CSDL để seed lại từ đầu (chạy app sẽ tạo lại)
dotnet ef database drop --force --project SportsCenter.DAL
```

## Chức năng theo vai trò

- **Quản lý:** tổng quan, báo cáo (doanh thu theo tháng, thành viên mới, đăng ký lớp, lấp đầy lớp, xuất CSV);
  thành viên, huấn luyện viên, tài khoản & phân quyền (đổi vai trò, khóa/mở, đặt lại mật khẩu); lớp học (phân công HLV,
  xếp lịch theo tuần), lịch hoạt động (thêm/đổi/hủy ca – tự thông báo cho thành viên), bộ môn, phòng tập; gói thành viên,
  đăng ký gói, thanh toán; nhật ký thao tác; yêu cầu hỗ trợ.
- **Huấn luyện viên:** lịch dạy, lớp của tôi, học viên (hồ sơ, mục tiêu, tiến độ), điểm danh từng buổi, kế hoạch tập
  cá nhân/cả lớp, kết quả tập, đánh giá tiến độ, gửi thông báo/bài tập về nhà, trợ lý AI gợi ý bài tập.
- **Thành viên:** lịch tập & thông tin HLV, đăng ký/hủy lớp, đăng ký/gia hạn gói (online), lịch sử điểm danh,
  kết quả & nhận xét, kế hoạch tập, hỏi AI, thông báo (nhắc lịch, đổi lịch, hết hạn gói), cập nhật hồ sơ.
- **Lễ tân:** check-in (kiểm tra hạn gói, tự điểm danh buổi lớp sắp diễn ra), đăng ký thành viên tại quầy,
  đăng ký/gia hạn gói, đăng ký/hủy lớp hộ, ghi nhận thanh toán, in/xuất hóa đơn, yêu cầu hỗ trợ.

Mọi thay đổi dữ liệu và đăng nhập/đăng xuất được ghi tự động vào **Nhật ký thao tác**.
