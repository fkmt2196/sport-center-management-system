using System.Windows;

namespace Project.Frontend.Shell
{
    public enum ToastKind { Success, Error, Warning, Info }

    /// <summary>Cửa sổ chính cung cấp toast và hộp thoại phủ (overlay) cho các trang.</summary>
    public interface IUiHost
    {
        void ShowToast(string message, ToastKind kind);
        Task<bool> ShowDialogAsync(FrameworkElement content);
        void CloseDialog(bool result);
        void NavigateTo(string title);
        Task RefreshNotificationsAsync();
    }

    /// <summary>Điểm truy cập tĩnh để trang con gọi toast/hộp thoại mà không cần tham chiếu cửa sổ.</summary>
    public static class UiHost
    {
        private static IUiHost? _host;

        public static void Register(IUiHost host) => _host = host;
        public static void Unregister(IUiHost host) { if (ReferenceEquals(_host, host)) _host = null; }

        public static void Toast(string message, ToastKind kind = ToastKind.Success)
        {
            if (_host != null) _host.ShowToast(message, kind);
            else MessageBox.Show(message);
        }

        public static Task<bool> ShowDialogAsync(FrameworkElement content) =>
            _host?.ShowDialogAsync(content) ?? Task.FromResult(false);

        public static void CloseDialog(bool result) => _host?.CloseDialog(result);

        public static void NavigateTo(string title) => _host?.NavigateTo(title);

        public static Task RefreshNotificationsAsync() => _host?.RefreshNotificationsAsync() ?? Task.CompletedTask;

        /// <summary>Hộp xác nhận (dùng cho thao tác xóa).</summary>
        public static Task<bool> ConfirmAsync(string title, string message, string confirmText = "Xóa", bool danger = true) =>
            ShowDialogAsync(new ConfirmDialog(title, message, confirmText, danger));
    }
}
