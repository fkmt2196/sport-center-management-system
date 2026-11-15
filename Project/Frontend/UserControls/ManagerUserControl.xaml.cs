using System;
using System.Windows;
using System.Windows.Controls;
// Các using này có thể cần sửa lại tùy theo không gian tên nhóm đang dùng
using SportsCenter.BLL.Services; 
using SportsCenter.BLL.ServiceScope; 
using SportsCenter.DAL.Models;

namespace SportsCenter.Frontend.UserControls
{
    public partial class ManagerUserControl : UserControl
    {
        public ManagerUserControl()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Sử dụng ServiceScope.Run theo yêu cầu của trưởng nhóm
                ServiceScope.Run(serviceProvider =>
                {
                    // userService.GetAllUsers() là hàm lấy dữ liệu em sẽ tạo bên BLL
                    var userService = serviceProvider.GetRequiredService<UserService>();
                    dgUsers.ItemsSource = userService.GetAllUsers();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            // Tạm thời lấy dữ liệu từ TextBox
            var newUser = new User 
            { 
                Name = txtName.Text, 
                Email = txtEmail.Text 
            };

            try
            {
                ServiceScope.Run(serviceProvider =>
                {
                    // AddNewUser là hàm thêm dữ liệu em sẽ tạo bên BLL
                    var userService = serviceProvider.GetRequiredService<UserService>();
                    userService.AddNewUser(newUser);
                });
                
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
