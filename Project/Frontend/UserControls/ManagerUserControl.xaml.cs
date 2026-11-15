using System;
using System.Windows;
using System.Windows.Controls;
using SportsCenter.Core.Services;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;

namespace SportsCenter.Frontend.UserControls
{
    public partial class ManagerUserControl : UserControl
    {
        // Khởi tạo Service trực tiếp (hoặc thông qua cơ chế cũ của nhóm)
        private readonly UserService _userService;

        public ManagerUserControl()
        {
            InitializeComponent();
            
            // Giả định khởi tạo trực tiếp để code chạy được trên nhánh hiện tại
            var context = new SportsCenterContext();
            _userService = new UserService(context);
            
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                dgUsers.ItemsSource = _userService.GetAllUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var newUser = new User 
            { 
                Name = txtName.Text, 
                Email = txtEmail.Text 
            };

            try
            {
                _userService.AddNewUser(newUser);
                MessageBox.Show("Thêm thành công!");
                LoadData(); 
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }
    }
}
