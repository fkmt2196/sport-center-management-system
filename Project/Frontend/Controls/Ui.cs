using System.Windows;

namespace Project.Frontend.Controls
{
    /// <summary>
    /// Attached property dùng trong Styles.xaml:
    ///  - Placeholder: chữ gợi ý hiển thị khi TextBox trống.
    ///  - HasError: viền đỏ cho ô nhập không hợp lệ.
    ///  - Icon: glyph (Segoe Fluent Icons) hiển thị bên trái ô nhập.
    /// </summary>
    public static class Ui
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(Ui), new PropertyMetadata(string.Empty));
        public static string GetPlaceholder(DependencyObject o) => (string)o.GetValue(PlaceholderProperty);
        public static void SetPlaceholder(DependencyObject o, string v) => o.SetValue(PlaceholderProperty, v);

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.RegisterAttached("HasError", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        public static bool GetHasError(DependencyObject o) => (bool)o.GetValue(HasErrorProperty);
        public static void SetHasError(DependencyObject o, bool v) => o.SetValue(HasErrorProperty, v);

        /// <summary>Sidebar thu gọn (chỉ icon). Kế thừa xuống các phần tử con.</summary>
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(Ui),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetIsCompact(DependencyObject o) => (bool)o.GetValue(IsCompactProperty);
        public static void SetIsCompact(DependencyObject o, bool v) => o.SetValue(IsCompactProperty, v);

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached("Icon", typeof(string), typeof(Ui), new PropertyMetadata(string.Empty));
        public static string GetIcon(DependencyObject o) => (string)o.GetValue(IconProperty);
        public static void SetIcon(DependencyObject o, string v) => o.SetValue(IconProperty, v);
    }

    /// <summary>Mã glyph của font Segoe Fluent Icons / Segoe MDL2 Assets.</summary>
    public static class Icons
    {
        public const string Home = "";
        public const string People = "";
        public const string Contact = "";
        public const string Education = "";
        public const string Library = "";
        public const string Calendar = "";
        public const string Checklist = "";
        public const string Clock = "";
        public const string Money = "";
        public const string SignOut = "";
        public const string Menu = "";
        public const string Search = "";
        public const string Add = "";
        public const string Edit = "";
        public const string Delete = "";
        public const string Close = "";
        public const string Refresh = "";
        public const string Filter = "";
        public const string ChevronLeft = "";
        public const string ChevronRight = "";
        public const string Success = "";
        public const string Error = "";
        public const string Warning = "";
        public const string Info = "";
        public const string Location = "";
        public const string Construction = "";
    }
}
