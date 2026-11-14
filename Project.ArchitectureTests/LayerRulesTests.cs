using System.Reflection;
using NetArchTest.Rules;
using SportsCenter.BLL;
using SportsCenter.DAL.Data;
using Xunit;

namespace Project.ArchitectureTests
{
    /// <summary>
    /// Giữ đúng kiến trúc 3 tầng:
    ///   1. Presentation (Project – WPF)       → chỉ gọi BLL
    ///   2. Business Logic (SportsCenter.BLL)  → chỉ gọi DAL
    ///   3. Data Access   (SportsCenter.DAL)   → không gọi tầng nào phía trên
    /// Hướng tham chiếu giữa các project đã do compiler chặn; các test dưới đây kiểm tra thêm
    /// những điều compiler không chặn được (dùng entity qua tham chiếu bắc cầu, đổi internal thành public...).
    /// </summary>
    public class LayerRulesTests
    {
        private static readonly Assembly Presentation = typeof(App).Assembly;
        private static readonly Assembly Bll = typeof(ServiceScope).Assembly;
        private static readonly Assembly Dal = typeof(AppSession).Assembly;

        private const string BllNs = "SportsCenter.BLL";
        private const string DalNs = "SportsCenter.DAL";
        private static readonly string[] Wpf = { "System.Windows", "Microsoft.Win32", "Project" };

        // ---------------- Tầng 1: Presentation ----------------

        [Fact]
        public void Presentation_KhongTruyCap_CSDL()
        {
            var result = Types.InAssembly(Presentation)
                .ShouldNot().HaveDependencyOnAny(
                    $"{DalNs}.Data",                 // DbContext, AppSession, Seeder
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.Data.SqlClient",
                    "System.Data")
                .GetResult();

            AssertSuccess(result, "Giao diện chỉ được gọi BLL (ServiceScope), không truy cập CSDL");
        }

        [Fact]
        public void Presentation_ChiThamChieu_BLL()
        {
            var refs = Presentation.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.Contains("SportsCenter.BLL", refs);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", refs);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore.SqlServer", refs);
        }

        // ---------------- Tầng 2: Business Logic ----------------

        [Fact]
        public void BLL_KhongPhuThuoc_GiaoDien()
        {
            var result = Types.InAssembly(Bll).ShouldNot().HaveDependencyOnAny(Wpf).GetResult();
            AssertSuccess(result, "BLL không được dùng WPF/giao diện");
        }

        [Fact]
        public void BLL_Services_KhongCoConstructorPublic()
        {
            // Service chỉ được tạo qua ServiceScope (constructor nhận DbContext là internal).
            var leaking = Bll.GetTypes()
                .Where(t => t.Namespace == $"{BllNs}.Services")
                .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(SportsCenterContext))))
                .Select(t => t.Name)
                .ToList();
            Assert.True(leaking.Count == 0, $"Service có constructor public nhận DbContext: {string.Join(", ", leaking)}");
        }

        [Fact]
        public void AI_KhongPhuThuoc_Services()
        {
            var result = Types.InAssembly(Bll)
                .That().ResideInNamespace($"{BllNs}.AI")
                .ShouldNot().HaveDependencyOnAny($"{BllNs}.Services", $"{DalNs}.Data")
                .GetResult();

            AssertSuccess(result, "AI là thành phần thấp hơn Services (Services gọi AI, không ngược lại)");
        }

        // ---------------- Tầng 3: Data Access ----------------

        [Fact]
        public void DAL_KhongPhuThuoc_BLL_HayGiaoDien()
        {
            var result = Types.InAssembly(Dal)
                .ShouldNot().HaveDependencyOnAny(Wpf.Append(BllNs).ToArray())
                .GetResult();

            AssertSuccess(result, "DAL không được gọi lên BLL hay giao diện");
        }

        [Fact]
        public void DAL_DbContext_LaInternal()
        {
            foreach (var type in new[] { typeof(SportsCenterContext), typeof(DbConfig), typeof(DbSeeder) })
                Assert.False(type.IsPublic, $"{type.Name} phải là internal để giao diện không truy cập CSDL trực tiếp");
        }

        [Fact]
        public void Models_LaEntityThuan()
        {
            var result = Types.InAssembly(Dal)
                .That().ResideInNamespace($"{DalNs}.Models")
                .ShouldNot().HaveDependencyOnAny($"{DalNs}.Data", "Microsoft.EntityFrameworkCore")
                .GetResult();

            AssertSuccess(result, "Models chỉ chứa entity thuần (không phụ thuộc DbContext/EF)");
        }

        private static void AssertSuccess(TestResult result, string rule)
        {
            var failing = result.FailingTypeNames ?? Array.Empty<string>();
            Assert.True(result.IsSuccessful, $"{rule}. Vi phạm: {string.Join(", ", failing)}");
        }
    }
}
