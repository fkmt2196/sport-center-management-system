using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Services
{
    /// <summary>Băm mật khẩu PBKDF2-SHA256 có salt. Định dạng: pbkdf2$vòng-lặp$salt$hash (base64).</summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }

    /// <summary>Kết quả kiểm tra dữ liệu: lỗi gắn với tên trường (để form hiển thị đúng chỗ).</summary>
    public class ValidationException : Exception
    {
        public string? Field { get; }
        public ValidationException(string message, string? field = null) : base(message) { Field = field; }
    }

    public class AuthService
    {
        private readonly SportsCenterContext _db;
        public AuthService(SportsCenterContext db) { _db = db; }

        /// <summary>Đăng nhập; trả về null nếu sai thông tin. Tài khoản bị khóa ném ValidationException.</summary>
        public async Task<User?> LoginAsync(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim());
            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;
            if (!user.IsActive) throw new ValidationException("Tài khoản đã bị khóa. Vui lòng liên hệ quầy lễ tân.");

            user.LastLogin = DateTime.Now;
            AppSession.SignIn(user);
            _db.AuditLogs.Add(new AuditLog { UserId = user.Id, Username = user.Username, Action = "Đăng nhập", EntityName = nameof(User), EntityId = user.Id.ToString(), Details = $"Vai trò: {user.Role}" });
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task LogoutAsync()
        {
            if (AppSession.UserId is int id)
            {
                _db.AuditLogs.Add(new AuditLog { UserId = id, Username = AppSession.Username, Action = "Đăng xuất", EntityName = nameof(User), EntityId = id.ToString() });
                await _db.SaveChangesAsync();
            }
            AppSession.SignOut();
        }

        /// <summary>Thành viên tự đăng ký tài khoản (vai trò Member).</summary>
        public async Task<User> RegisterMemberAsync(string username, string password, string fullName, string phone, string email)
        {
            username = username.Trim();
            if (await _db.Users.AnyAsync(u => u.Username == username))
                throw new ValidationException("Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.", "Username");

            var user = new User
            {
                Username = username, PasswordHash = PasswordHasher.Hash(password), FullName = fullName.Trim(),
                Phone = phone.Trim(), Email = email.Trim(), Role = UserRole.Member,
                Member = new Member { JoinDate = DateTime.Today }
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _db.Users.FirstAsync(u => u.Id == userId);
            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
                throw new ValidationException("Mật khẩu hiện tại không đúng.", "Current");
            user.PasswordHash = PasswordHasher.Hash(newPassword);
            await _db.SaveChangesAsync();
        }
    }
}
