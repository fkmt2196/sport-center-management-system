using System;
using System.Collections.Generic;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;

namespace SportsCenter.Core.Services
{
    public class UserService
    {
        private readonly UserRepository _userRepo;

        public UserService(SportsCenterContext context)
        {
            _userRepo = new UserRepository(context);
        }

        public List<User> GetAllUsers()
        {
            return _userRepo.GetAllUsers();
        }

        public void AddNewUser(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Name))
                throw new Exception("Tên người dùng không được để trống!");
            
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
                throw new Exception("Email không hợp lệ!");

            _userRepo.AddUser(user);
        }
    }
}
public void UpdateExistingUser(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Name))
                throw new Exception("Tên người dùng không được để trống!");
            
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
                throw new Exception("Email không hợp lệ!");

            _userRepo.UpdateUser(user);
        }

        public void DeleteUser(User user)
        {
            _userRepo.DeleteUser(user);
        }
