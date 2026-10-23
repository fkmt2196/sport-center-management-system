# Sport Center Management System

Ứng dụng desktop WPF quản lý trung tâm thể thao, xây dựng bằng C# và .NET 8.
Nhóm có **5 thành viên**: Võ Minh Trí, Huỳnh Nhật Huy, Phạm Tùng Dương,
Trần Hải Long và Trần Thiện Mỹ.

Repo hiện là **khung dự án ban đầu**: có solution, các project tham chiếu đúng tầng,
màn hình WPF khởi động và thư mục dành cho từng phần. Nghiệp vụ và kết nối cơ sở
dữ liệu sẽ được triển khai sau.

## Cấu trúc

```text
sport-center-management-system/
├── SportCenterManagement.sln           # Mở file này để làm việc với cả solution
├── global.json                        # Dùng .NET SDK 8
├── .editorconfig                      # Quy tắc định dạng chung
├── .gitignore                         # Bỏ qua build output, cấu hình máy và secrets
├── db/
│   ├── README.md                      # Hướng dẫn quản lý script
│   ├── scripts/                       # Script tạo/cập nhật database
│   └── seeds/                         # Dữ liệu mẫu không chứa thông tin thật
├── frontend/
│   └── SportCenter.Wpf/                # Giao diện WPF, theo MVVM
│       ├── Assets/                     # Hình ảnh, icon
│       ├── Converters/                 # Chuyển đổi giá trị binding
│       ├── Resources/Styles/           # Style và resource XAML dùng chung
│       ├── Services/                   # Dialog, điều hướng giao diện
│       ├── ViewModels/                 # Trạng thái và lệnh của giao diện
│       └── Views/                      # Các màn hình XAML
└── backend/
    ├── SportCenter.Domain/            # Entity, enum và kiểu dùng chung
    │   ├── Common/
    │   ├── Entities/
    │   └── Enums/
    ├── SportCenter.BLL/               # Business Logic Layer: xử lý nghiệp vụ
    │   ├── DTOs/
    │   ├── Interfaces/
    │   ├── Services/
    │   └── Validators/
    └── SportCenter.DAL/               # Data Access Layer: truy cập dữ liệu
        ├── Data/                      # DbContext và cấu hình database sau này
        ├── Configurations/            # Mapping entity sau này
        ├── Interfaces/
        └── Repositories/
```

`frontend` phụ trách hiển thị và tương tác. `backend` gồm các thư viện C# được WPF
gọi trực tiếp; cấu trúc hiện tại không cần chạy một web API riêng.

## Chạy dự án

Cài **.NET 8 SDK**. Nếu dùng Visual Studio, cài workload **.NET desktop development**.

Mở `SportCenterManagement.sln`, đặt `SportCenter.Wpf` làm startup project và nhấn F5.
Hoặc chạy tại thư mục gốc repo trên Windows:

```powershell
dotnet restore SportCenterManagement.sln
dotnet build SportCenterManagement.sln --no-restore
dotnet run --project frontend/SportCenter.Wpf/SportCenter.Wpf.csproj
```

Hiện chưa cần cấu hình database. Màn hình đầu tiên chỉ giới thiệu các module dự kiến,
chưa có đăng nhập, CRUD hay đặt lịch.

## Tài liệu cho nhóm

- [Quản lý script database](db/README.md)

Các thư mục trống có `.gitkeep` để Git lưu lại cấu trúc. Khi thêm file thật vào thư
mục, có thể xóa `.gitkeep` tương ứng.
