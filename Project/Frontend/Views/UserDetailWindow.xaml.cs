using System.Windows;
using SportsCenter.Core.Models;

namespace SportsCenter.Frontend.Windows
{
    public partial class UserDetailWindow : Window
    {
        public User CurrentUser { get; private set; }

        // Constructor dùng cho Thêm mới
        public UserDetailWindow()
        {
            InitializeComponent();
            CurrentUser = new User();
        }

        // Constructor dùng cho Sửa (truyền User cũ vào)
        public UserDetailWindow(User userToEdit)
        {
            InitializeComponent();
            CurrentUser = userToEdit;

            // Đổ dữ liệu cũ lên giao diện
            txtFullName.Text = CurrentUser.FullName;
            txtEmail.Text = CurrentUser.Email;
            txtPhone.Text = CurrentUser.Phone;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dữ liệu từ giao diện gán vào Model
            CurrentUser.FullName = txtFullName.Text;
            CurrentUser.Email = txtEmail.Text;
            CurrentUser.Phone = txtPhone.Text;

            this.DialogResult = true; // Báo hiệu lưu thành công
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}