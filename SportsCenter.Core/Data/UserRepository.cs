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
    }
}