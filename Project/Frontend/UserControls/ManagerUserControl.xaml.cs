using Microsoft.EntityFrameworkCore;
using SportsCenter.Core.Data;
using SportsCenter.Core.Models;
using SportsCenter.Core.Services;
using SportsCenter.Frontend.Windows;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SportsCenter.Frontend.UserControls
{
    public partial class ManagerUserControl : UserControl
    {
        private readonly UserService _userService;
        private ObservableCollection<User> _userList = new ObservableCollection<User>();

        public ManagerUserControl()
        {
            InitializeComponent();

            // Khởi tạo Options cho DbContext (Sử dụng cấu hình tạm thời để chạy)
            var optionsBuilder = new DbContextOptionsBuilder<SportsCenterContext>();
           

            var context = new SportsCenterContext(optionsBuilder.Options);
            _userService = new UserService(context);

            LoadData();
        }

        private void LoadData()
        {
            var usersFromDb = _userService.GetAllUsers();
            _userList = new ObservableCollection<User>(usersFromDb);
            dgUsers.ItemsSource = _userList;
        }

        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            // Mở Window dạng thêm mới
            var window = new UserDetailWindow();
            if (window.ShowDialog() == true) // Nếu người dùng bấm Lưu
            {
                try
                {
                    _userService.AddNewUser(window.CurrentUser); // Lưu vào SQL

                    _userList.Add(window.CurrentUser); // Tự động vẽ thêm 1 dòng trên giao diện

                    MessageBox.Show("Thêm thành công!");
                
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi: " + ex.Message);
                }
            }
        }


        // Sự kiện khi bấm vào 1 dòng trong DataGrid
        private void dgUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Bỏ trống hàm này, vì chúng ta không cần đổ dữ liệu ra Textbox bên ngoài nữa.
            // Dữ liệu sẽ được truyền thẳng vào Window phụ khi bấm nút "Sửa".
        }



        private void btnUpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is User selectedUser)
            {
                var window = new UserDetailWindow(selectedUser);
                if (window.ShowDialog() == true)
                {
                    try
                    {
                        _userService.UpdateExistingUser(selectedUser); // Cập nhật SQL

                        // F5 lại bề mặt hiển thị của bảng (cực nhanh, không chọc DB)
                        dgUsers.Items.Refresh();

                        MessageBox.Show("Cập nhật thành công!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn tài khoản để sửa!");
            }
        }

        // Logic cho tìm kiếm và lọc cơ bản (Sẽ tối ưu bằng ObservableCollection sau)
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterData();
        }

        private void cmbRoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterData();
        }

        // Sự kiện nút Xóa
        private void btnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is User selectedUser)
            {
                var result = MessageBox.Show("Bạn có chắc muốn xóa?", "Xác nhận", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _userService.DeleteUser(selectedUser); // Xóa khỏi SQL

                        _userList.Remove(selectedUser); // Tự động xóa dòng trên giao diện

                        MessageBox.Show("Xóa thành công!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn tài khoản để xóa!");
            }
        }

        private void FilterData()
        {
            // Đảm bảo danh sách đã được nạp trước khi lọc
            if (_userList == null) return;

            // Lấy luồng hiển thị (View) của ObservableCollection hiện tại
            var view = CollectionViewSource.GetDefaultView(_userList);

            // Áp dụng điều kiện lọc
            view.Filter = item =>
            {
                if (item is User user)
                {
                    // 1. Kiểm tra điều kiện tìm kiếm (Tìm theo Tên hoặc Email, không phân biệt hoa thường)
                    string searchText = txtSearch.Text.ToLower();
                    bool matchSearch = string.IsNullOrEmpty(searchText) ||
                                       user.FullName.ToLower().Contains(searchText) ||
                                       user.Email.ToLower().Contains(searchText);

                    // 2. Kiểm tra điều kiện lọc vai trò
                    bool matchRole = true;
                    if (cmbRoleFilter.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                    {
                        string roleTag = selectedItem.Tag?.ToString() ?? string.Empty;
                        matchRole = user.Role.ToString() == roleTag; // So sánh Enum dưới dạng chữ
                    }

                    // Dòng dữ liệu này chỉ được hiển thị nếu thỏa mãn CẢ 2 điều kiện
                    return matchSearch && matchRole;
                }
                return false;
            };
        }
    }
}