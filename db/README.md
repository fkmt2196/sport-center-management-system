# Database

Thư mục này dành cho script database dùng chung khi nhóm triển khai dữ liệu.
Giai đoạn dựng khung chưa chọn hệ quản trị database và chưa cần connection string.

- `scripts/`: script tạo schema hoặc thay đổi schema; nên đặt tên có thứ tự,
  ví dụ `001_initial_schema.sql`.
- `seeds/`: dữ liệu mẫu phục vụ phát triển và demo, không dùng dữ liệu cá nhân thật.

Nếu dùng EF Core Migrations sau này, đặt migration trong `SportCenter.DAL` và ghi
lệnh cập nhật database vào tài liệu này. Chọn một quy trình quản lý schema thống
nhất để tránh script SQL và migration bị lệch nhau.

Chỉ commit script và cấu hình mẫu. File `.mdf`, `.ldf`, `.bak`, SQLite local và
cấu hình chứa thông tin đăng nhập phải nằm ngoài Git.
