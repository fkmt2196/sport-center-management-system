using System.Windows;
using System.Windows.Controls;
using Project.Frontend.Data;
using Project.Frontend.Shell;

namespace Project.Frontend.Controls
{
    /// <summary>Tiện ích dùng chung cho các trang danh sách.</summary>
    public static class ListPage
    {
        /// <summary>
        /// Thanh công cụ thích ứng: rộng thì [ô tìm kiếm | bộ lọc] cùng hàng; hẹp thì bộ lọc xuống hàng dưới.
        /// Toolbar là Grid 2 cột × 2 hàng; search ở (0,0), filters ở (0,1).
        /// </summary>
        public static void MakeToolbarResponsive(FrameworkElement page, Grid toolbar, FrameworkElement search, FrameworkElement filters, double breakpoint = 760)
        {
            void Apply()
            {
                var narrow = page.ActualWidth < breakpoint;
                Grid.SetRow(filters, narrow ? 1 : 0);
                Grid.SetColumn(filters, narrow ? 0 : 1);
                Grid.SetColumnSpan(filters, narrow ? 2 : 1);
                Grid.SetColumnSpan(search, narrow ? 2 : 1);
                filters.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 0, 0);
                if (filters is Panel p)
                    foreach (var child in p.Children.OfType<FrameworkElement>())
                        child.Margin = narrow ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 8, 0);
            }
            page.SizeChanged += (_, _) => Apply();
        }

        public static ComboBoxItem Option(string text, object? value) => new() { Content = text, Tag = value };

        public static object? SelectedValue(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag;

        /// <summary>Nạp lại option cho combobox lọc, giữ lựa chọn cũ nếu còn.</summary>
        public static void FillFilter(ComboBox combo, string allText, IEnumerable<(string Text, object Value)> options)
        {
            var previous = SelectedValue(combo);
            combo.Items.Clear();
            combo.Items.Add(Option(allText, null));
            foreach (var (text, value) in options) combo.Items.Add(Option(text, value));
            combo.SelectedIndex = 0;
            if (previous != null)
                foreach (ComboBoxItem item in combo.Items)
                    if (Equals(item.Tag, previous)) { combo.SelectedItem = item; break; }
        }

        /// <summary>Ẩn/hiện nút Sửa, Xóa trong cột thao tác (template dùng DynamicResource) và cột thao tác.</summary>
        public static void ApplyPermissions(FrameworkElement page, PagePermissions perm, FrameworkElement? addButton, DataGridColumn? actionColumn)
        {
            page.Resources["EditVisibility"] = perm.CanEdit ? Visibility.Visible : Visibility.Collapsed;
            page.Resources["DeleteVisibility"] = perm.CanDelete ? Visibility.Visible : Visibility.Collapsed;
            if (addButton != null) addButton.Visibility = perm.CanCreate ? Visibility.Visible : Visibility.Collapsed;
            if (actionColumn != null) actionColumn.Visibility = perm.HasRowActions ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Hỏi xác nhận → chạy thao tác xóa qua service → toast kết quả. Trả về true nếu đã xóa.</summary>
        public static async Task<bool> ConfirmDeleteAsync(string title, string message, Func<UiScope, Task> delete, string successMessage)
        {
            if (!await UiHost.ConfirmAsync(title, message)) return false;
            try
            {
                using var scope = new UiScope();
                await delete(scope);
                UiHost.Toast(successMessage, ToastKind.Success);
                return true;
            }
            catch (Exception ex)
            {
                UiHost.Toast($"Không thể xóa: {(ex.InnerException ?? ex).Message}", ToastKind.Error);
                return false;
            }
        }
    }
}
