using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using SportsCenter.Core.Data;

namespace Project
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Môi trường chạy: biến DOTNET_ENVIRONMENT nếu có; nếu không thì bản Debug = Development, bản Release = Production.
        /// </summary>
        public static string EnvironmentName { get; } =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
#if DEBUG
            ?? "Development";
#else
            ?? "Production";
#endif

        public static bool IsDevelopment =>
            string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Định dạng kiểu Việt Nam cho toàn bộ giao diện: ngày dd/MM/yyyy, số 4.500.000.
            var vi = new CultureInfo("vi-VN");
            CultureInfo.DefaultThreadCurrentCulture = vi;
            CultureInfo.DefaultThreadCurrentUICulture = vi;
            Thread.CurrentThread.CurrentCulture = vi;
            Thread.CurrentThread.CurrentUICulture = vi;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(vi.IetfLanguageTag)));

            // Lỗi giao diện chưa được bắt: báo bằng toast thay vì tắt ứng dụng.
            DispatcherUnhandledException += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(args.Exception);
                Project.Frontend.Shell.UiHost.Toast($"Đã xảy ra lỗi: {args.Exception.Message}", Project.Frontend.Shell.ToastKind.Error);
                args.Handled = true;
            };

            base.OnStartup(e);

            if (IsDevelopment)
            {
                try
                {
                    using var context = DbConfig.CreateContext();
                    context.Database.Migrate();   // đảm bảo schema mới nhất trước khi seed
                    DbSeeder.Seed(context);       // dữ liệu mẫu khi CSDL còn trống
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể khởi tạo dữ liệu mẫu:\n{ex.Message}", "Database",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
