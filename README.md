# FitZone Sports Center – Hệ thống quản lý trung tâm thể thao

Ứng dụng WPF (.NET 8) quản lý trung tâm thể thao/tập luyện với 4 vai trò: **Quản lý**, **Huấn luyện viên**, **Thành viên**, **Lễ tân**.

## Cấu trúc

```
Project.slnx
├── SportsCenter.Core/        # Tầng dùng chung (mọi ứng dụng: WPF, web...)
│   ├── Models/               # Entity: User, Member, Coach, Sport, Room, TrainingClass, ClassSession,
│   │                         #   Enrollment, Attendance, CheckIn, MembershipPlan, Subscription, Payment,
│   │                         #   TrainingPlan, WorkoutLog, ProgressReview, Notification, SupportRequest, AuditLog
│   ├── Data/                 # SportsCenterContext (audit log tự động), DbConfig, DbSeeder, Migrations, AppSession
│   ├── Services/             # Nghiệp vụ + kiểm tra quyền theo vai trò
│   └── AI/                   # IAiCoachAssistant: ClaudeAiAssistant / TemplateAiAssistant (gợi ý mẫu)
└── Project/                  # Ứng dụng WPF
    └── Frontend/             # Themes, Controls (TablePage, Pager, BarChart...), Forms, Shell, Views theo vai trò
```

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

Tính năng AI nằm sau interface `IAiCoachAssistant` (`SportsCenter.Core/AI`):

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
dotnet ef migrations add <Ten> --project SportsCenter.Core --startup-project Project --output-dir Data/Migrations
# Áp vào CSDL
dotnet ef database update --project SportsCenter.Core --startup-project Project
# Xóa CSDL để seed lại từ đầu (chạy app sẽ tạo lại)
dotnet ef database drop --force --project SportsCenter.Core --startup-project Project
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
