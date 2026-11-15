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
// Sự kiện khi bấm vào 1 dòng trong DataGrid
        private void dgUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsers.SelectedItem is User selectedUser)
            {
                txtName.Text = selectedUser.Name;
                txtEmail.Text = selectedUser.Email;
            }
        }

        // Sự kiện nút Sửa
        private void btnUpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is User selectedUser)
            {
                selectedUser.Name = txtName.Text;
                selectedUser.Email = txtEmail.Text;

                try
                {
                    _userService.UpdateExistingUser(selectedUser);
                    MessageBox.Show("Cập nhật thành công!");
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một người dùng trong bảng để sửa!");
            }
        }

        // Sự kiện nút Xóa
        private void btnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is User selectedUser)
            {
                var result = MessageBox.Show("Bạn có chắc chắn muốn xóa tài khoản này?", "Xác nhận", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _userService.DeleteUser(selectedUser);
                        MessageBox.Show("Xóa thành công!");
                        LoadData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một người dùng trong bảng để xóa!");
            }
        }
