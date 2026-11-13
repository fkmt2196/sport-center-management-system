using System.Windows;
using Project.Frontend.Data;
using SportsCenter.Core.Services;

namespace Project.Frontend.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Title = $"Đăng nhập - {AppInfo.CenterName}";
            BrandName.Text = AppInfo.CenterName;
            BrandFooter.Text = $"© {DateTime.Today.Year} {AppInfo.CenterName}";

            // Cửa sổ hẹp: ẩn khối thương hiệu, chỉ giữ form.
            SizeChanged += (_, _) =>
            {
                var narrow = ActualWidth < 760;
                BrandPanel.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
                BrandColumn.Width = narrow ? new GridLength(0) : new GridLength(5, GridUnitType.Star);
            };
            Loaded += (_, _) => UsernameTextBox.Focus();
        }

        #region Đăng ký thành viên

        private void ShowRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            LoginButton.IsDefault = false;
            RegisterButton.IsDefault = true;
            RegFullName.Focus();
        }

        private void ShowLogin_Click(object sender, RoutedEventArgs e)
        {
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
            RegisterButton.IsDefault = false;
            LoginButton.IsDefault = true;
        }

        private void ShowRegError(string message)
        {
            RegError.Text = message;
            RegErrorPanel.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var name = RegFullName.Text.Trim();
            var username = RegUsername.Text.Trim();
            var phone = RegPhone.Text.Trim();
            var email = RegEmail.Text.Trim();
            string? error =
                name.Length == 0 ? "Vui lòng nhập họ và tên." :
                !System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9._]{3,50}$") ? "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới." :
                !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0\d{9}$") ? "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0." :
                email.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ? "Email không hợp lệ." :
                RegPassword.Password.Length < 6 ? "Mật khẩu phải có ít nhất 6 ký tự." :
                RegPassword.Password != RegConfirm.Password ? "Mật khẩu nhập lại không khớp." : null;
            if (error != null) { ShowRegError(error); return; }

            ShowRegError("");
            RegisterButton.IsEnabled = false;
            try
            {
                using var scope = new UiScope();
                await scope.Auth.RegisterMemberAsync(username, RegPassword.Password, name, phone, email);
                ShowLogin_Click(sender, e);
                UsernameTextBox.Text = username;
                PasswordBox.Clear();
                PasswordBox.Focus();
                ShowError("");
                MessageBox.Show(this, "Đăng ký thành công! Hãy đăng nhập và đăng ký gói thành viên để bắt đầu tập.", "Đăng ký thành viên", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (ValidationException ex) { ShowRegError(ex.Message); }
            catch (Exception ex) { ShowRegError($"Lỗi: {ex.Message}"); }
            finally { RegisterButton.IsEnabled = true; }
        }

        #endregion

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorPanel.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetBusy(bool busy)
        {
            LoginButton.IsEnabled = !busy;
            LoginButtonText.Text = busy ? "Đang đăng nhập..." : "Đăng nhập";
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;
            ShowError("");
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Vui lòng nhập tên đăng nhập và mật khẩu.");
                return;
            }

            SetBusy(true);
            try
            {
                using var scope = new UiScope();
                var user = await scope.Auth.LoginAsync(username, password);
                if (user != null)
                {
                    var memberId = (await scope.Members.GetByUserIdAsync(user.Id))?.Id;
                    var coachId = (await scope.Coaches.GetByUserIdAsync(user.Id))?.Id;
                    Me.Init(user, memberId, coachId);
                    new DashboardWindow(user).Show();
                    Close();
                }
                else
                {
                    ShowError("Tên đăng nhập hoặc mật khẩu không đúng.");
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (ValidationException ex) { ShowError(ex.Message); }
            catch (Exception ex) { ShowError($"Lỗi kết nối CSDL: {ex.Message}"); }
            finally { SetBusy(false); }
        }
    }
}
