using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SportsCenter.DAL.Data
{
    /// <summary>Cấu hình kết nối CSDL dùng chung cho mọi ứng dụng (WPF, web...).</summary>
    internal static class DbConfig
    {
        /// <summary>
        /// Có thể ghi đè bằng biến môi trường SPORTSCENTER_DB.
        /// Mặc định: SQL Server instance mặc định trên máy (đổi "localhost" thành ".\\SQLEXPRESS" nếu cần).
        /// </summary>
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("SPORTSCENTER_DB")
            ?? "Server=localhost;Database=SportsCenterDb;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SportsCenterContext CreateContext() =>
            new(new DbContextOptionsBuilder<SportsCenterContext>().UseSqlServer(ConnectionString).Options);
    }

    /// <summary>Dùng bởi "dotnet ef" khi tạo migration.</summary>
    internal class SportsCenterContextFactory : IDesignTimeDbContextFactory<SportsCenterContext>
    {
        public SportsCenterContext CreateDbContext(string[] args) => DbConfig.CreateContext();
    }
}
