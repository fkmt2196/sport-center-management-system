using System.Collections.Generic;
using System.Linq;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Data
{
    public class UserRepository
    {
        private readonly SportsCenterContext _context;

        public UserRepository(SportsCenterContext context)
        {
            _context = context;
        }

        public List<User> GetAllUsers()
        {
            return _context.Users.ToList();
        }

        public void AddUser(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }



        public void UpdateUser(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
        }

        public void DeleteUser(User user)
        {
            _context.Users.Remove(user);
            _context.SaveChanges();
        }

        public User? GetById(int id)
        {
            return _context.Users.FirstOrDefault(u => u.Id == id);
        }

        // Kiểm tra số điện thoại trùng lặp (trừ ID của user đang được sửa nếu có)
        public bool CheckPhoneExists(string phone, int? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            phone = phone.Trim();
            if (currentUserId.HasValue)
                return _context.Users.Any(u => u.Phone == phone && u.Id != currentUserId.Value);

            return _context.Users.Any(u => u.Phone == phone);
        }

    }
}