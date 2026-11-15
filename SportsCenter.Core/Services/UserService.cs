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
            if (string.IsNullOrWhiteSpace(user.FullName))
                throw new Exception("Tên người dùng không được để trống!");
            user.Phone = user.Phone?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
                throw new Exception("Email không hợp lệ!");

            // Bổ sung kiểm tra trùng số điện thoại
            if (_userRepo.CheckPhoneExists(user.Phone))
                throw new Exception("Số điện thoại này đã được sử dụng trong hệ thống!");

            _userRepo.AddUser(user);
        }


        public void UpdateExistingUser(User user)
        {
            if (string.IsNullOrWhiteSpace(user.FullName))
                throw new Exception("Tên người dùng không được để trống!");

            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
                throw new Exception("Email không hợp lệ!");

            // Kiểm tra trùng số điện thoại, loại trừ ID của chính User đang sửa
            if (_userRepo.CheckPhoneExists(user.Phone, user.Id))
                throw new Exception("Số điện thoại này đã thuộc về tài khoản khác!");

            _userRepo.UpdateUser(user);
        }

        public void DeleteUser(User user)
        {
            _userRepo.DeleteUser(user);
        }
    }
}