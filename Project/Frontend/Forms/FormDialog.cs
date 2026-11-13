using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Project.Frontend.Controls;
using Project.Frontend.Shell;

namespace Project.Frontend.Forms
{
    public enum FieldKind { Text, Password, Multiline, Number, Money, Date, Time, Combo, Check, MultiCheck, Decimal }

    /// <summary>Khai báo một trường của form.</summary>
    public class FormField
    {
        public FormField(string key, string label, FieldKind kind = FieldKind.Text) { Key = key; Label = label; Kind = kind; }
        public string Key { get; }
        public string Label { get; }
        public FieldKind Kind { get; }
        public bool Required { get; init; }
        /// <summary>Chiếm cả 2 cột.</summary>
        public bool Wide { get; init; }
        public bool ReadOnly { get; init; }
        public string? Placeholder { get; init; }
        public string? Hint { get; init; }
        public int? MaxLength { get; init; }
        public object? Value { get; init; }
        public IEnumerable<(string Text, object Value)>? Options { get; init; }
        /// <summary>Kiểm tra riêng của trường; trả về thông báo lỗi hoặc null.</summary>
        public Func<FormDialog, object?, string?>? Validate { get; init; }
        /// <summary>Gọi khi giá trị thay đổi (vd. combobox phụ thuộc nhau).</summary>
        public Action<FormDialog, object?>? OnChanged { get; init; }
    }

    /// <summary>
    /// Hộp thoại form 2 cột dùng chung: validation hiển thị dưới từng trường, banner lỗi tổng,
    /// trạng thái đang lưu. Hiển thị trong lớp phủ của cửa sổ chính (UiHost).
    /// </summary>
    public class FormDialog : Border
    {
        private class FieldUi
        {
            public required FormField Field;
            public required FrameworkElement Container;
            public required Control Input;
            public required TextBlock Error;
            public bool Touched;
        }

        private readonly Dictionary<string, FieldUi> _fields = new();
        private readonly List<FieldUi> _order = new();
        private readonly Grid _body = new();
        private readonly Border _banner;
        private readonly TextBlock _bannerText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        private readonly Button _submit;
        private readonly TextBlock _submitText;
        private readonly string _submitLabel;
        private readonly Func<FormDialog, Task<bool>> _onSubmit;
        private bool _twoColumns = true;
        private bool _busy;

        public FormDialog(string title, string subtitle, string icon, IEnumerable<FormField> fields,
                          Func<FormDialog, Task<bool>> onSubmit, string submitText = "Lưu")
        {
            _onSubmit = onSubmit;
            _submitLabel = submitText;
            Style = (Style)FindResource("Card");
            Padding = new Thickness(0);
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 10, Direction = 270, Opacity = 0.22 };

            var root = new DockPanel();

            // --- Header ---
            var header = new DockPanel { Margin = new Thickness(24, 20, 16, 16) };
            var close = new Button { Style = (Style)FindResource("Btn.Icon"), Content = Icons.Close, FontSize = 11, ToolTip = "Đóng (Esc)", VerticalAlignment = VerticalAlignment.Top };
            close.Click += (_, _) => { if (!_busy) UiHost.CloseDialog(false); };
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var iconBox = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = Res("PrimarySoftBrush"), Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock { Text = icon, Style = (Style)FindResource("Text.Icon"), FontSize = 20, Foreground = Res("PrimaryBrush"), HorizontalAlignment = HorizontalAlignment.Center }
            };
            DockPanel.SetDock(iconBox, Dock.Left);
            header.Children.Add(iconBox);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Text.H2") });
            titles.Children.Add(new TextBlock { Text = subtitle, Style = (Style)FindResource("Text.Secondary"), Margin = new Thickness(0, 2, 0, 0) });
            header.Children.Add(titles);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // --- Footer ---
            var footer = new Border { BorderBrush = Res("LineBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 14, 24, 16), Background = Res("SurfaceAltBrush"), CornerRadius = new CornerRadius(0, 0, 12, 12) };
            var footerPanel = new DockPanel();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Hủy", Style = (Style)FindResource("Btn.Secondary"), Margin = new Thickness(0, 0, 10, 0), MinWidth = 90 };
            cancel.Click += (_, _) => { if (!_busy) UiHost.CloseDialog(false); };
            _submitText = new TextBlock { Text = submitText };
            var submitContent = new StackPanel { Orientation = Orientation.Horizontal };
            submitContent.Children.Add(new TextBlock { Text = "", Style = (Style)FindResource("Text.Icon"), Margin = new Thickness(0, 0, 8, 0) });
            submitContent.Children.Add(_submitText);
            _submit = new Button { Content = submitContent, Style = (Style)FindResource("Btn.Primary"), MinWidth = 110 };
            _submit.Click += async (_, _) => await SubmitAsync();
            System.Windows.Automation.AutomationProperties.SetAutomationId(_submit, "FormSubmit");
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancel, "FormCancel");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_bannerText, "FormBanner");
            buttons.Children.Add(cancel);
            buttons.Children.Add(_submit);
            DockPanel.SetDock(buttons, Dock.Right);
            footerPanel.Children.Add(buttons);
            footerPanel.Children.Add(new TextBlock { Text = "* Trường bắt buộc", Style = (Style)FindResource("Text.Caption"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            footer.Child = footerPanel;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            // --- Body ---
            var bodyPanel = new StackPanel { Margin = new Thickness(24, 0, 24, 8) };
            _banner = new Border
            {
                Background = Res("DangerSoftBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 16), Visibility = Visibility.Collapsed
            };
            var bannerRow = new DockPanel();
            var bannerIcon = new TextBlock { Text = Icons.Error, Style = (Style)FindResource("Text.Icon"), Foreground = Res("DangerBrush"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(bannerIcon, Dock.Left);
            bannerRow.Children.Add(bannerIcon);
            _bannerText.Foreground = Res("DangerBrush");
            bannerRow.Children.Add(_bannerText);
            _banner.Child = bannerRow;
            bodyPanel.Children.Add(_banner);

            _body.ColumnDefinitions.Add(new ColumnDefinition());
            _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            _body.ColumnDefinitions.Add(new ColumnDefinition());
            bodyPanel.Children.Add(_body);
            root.Children.Add(bodyPanel);

            foreach (var f in fields) AddField(f);
            Child = root;

            PreviewKeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter && !(Keyboard.FocusedElement is TextBox { AcceptsReturn: true }) && !(Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true }))
                {
                    e.Handled = true;
                    await SubmitAsync();
                }
            };
            Loaded += (_, _) =>
            {
                var window = Window.GetWindow(this);
                if (window != null) { window.SizeChanged += (_, _) => FitToWindow(window); FitToWindow(window); }
                _order.FirstOrDefault(f => !f.Field.ReadOnly && f.Input.IsEnabled)?.Input.Focus();
            };
        }

        private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

        #region Dựng trường

        private void AddField(FormField f)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            Control input;

            if (f.Kind != FieldKind.Check)
            {
                var label = new TextBlock { Style = (Style)FindResource("Text.Label") };
                label.Inlines.Add(new System.Windows.Documents.Run(f.Label));
                if (f.Required) label.Inlines.Add(new System.Windows.Documents.Run(" *") { Foreground = Res("DangerBrush") });
                container.Children.Add(label);
            }

            switch (f.Kind)
            {
                case FieldKind.Password:
                    input = new PasswordBox { MaxLength = f.MaxLength ?? 100 };
                    ((PasswordBox)input).PasswordChanged += (_, _) => Changed(f.Key);
                    break;
                case FieldKind.Date:
                    var dp = new DatePicker { SelectedDate = f.Value as DateTime?, IsEnabled = !f.ReadOnly, SelectedDateFormat = DatePickerFormat.Short };
                    dp.SelectedDateChanged += (_, _) => Changed(f.Key);
                    dp.Loaded += (_, _) => LocalizeDatePicker(dp);
                    input = dp;
                    break;
                case FieldKind.Combo:
                    var cb = new ComboBox { IsEnabled = !f.ReadOnly };
                    FillCombo(cb, f.Options, f.Value);
                    cb.SelectionChanged += (_, _) => Changed(f.Key);
                    input = cb;
                    break;
                case FieldKind.MultiCheck:
                    var wrap = new WrapPanel();
                    var selected = (f.Value as System.Collections.IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
                    foreach (var (text, value) in f.Options ?? Enumerable.Empty<(string, object)>())
                    {
                        var box = new CheckBox { Content = text, Tag = value, IsChecked = selected.Contains(value), Margin = new Thickness(0, 6, 14, 6), IsEnabled = !f.ReadOnly };
                        box.Checked += (_, _) => Changed(f.Key);
                        box.Unchecked += (_, _) => Changed(f.Key);
                        wrap.Children.Add(box);
                    }
                    input = new ContentControl { Content = wrap, Focusable = false };
                    break;
                case FieldKind.Check:
                    var chk = new CheckBox { Content = f.Label, IsChecked = f.Value as bool? ?? false, IsEnabled = !f.ReadOnly, Margin = new Thickness(0, 26, 0, 0) };
                    chk.Checked += (_, _) => Changed(f.Key);
                    chk.Unchecked += (_, _) => Changed(f.Key);
                    input = chk;
                    break;
                default:
                    var tb = new TextBox { IsReadOnly = f.ReadOnly, Text = FormatInitial(f) };
                    if (f.MaxLength.HasValue) tb.MaxLength = f.MaxLength.Value;
                    if (f.Placeholder != null) Ui.SetPlaceholder(tb, f.Placeholder);
                    if (f.Kind == FieldKind.Multiline)
                    {
                        tb.AcceptsReturn = true; tb.TextWrapping = TextWrapping.Wrap; tb.MinHeight = 84;
                        tb.VerticalContentAlignment = VerticalAlignment.Top; tb.Padding = new Thickness(10, 8, 10, 8);
                    }
                    if (f.Kind is FieldKind.Number or FieldKind.Money)
                    {
                        tb.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
                        DataObject.AddPastingHandler(tb, (_, e) =>
                        {
                            if (e.DataObject.GetData(typeof(string)) is not string s || !s.Replace(".", "").Replace(",", "").All(char.IsDigit)) e.CancelCommand();
                        });
                        if (f.Kind == FieldKind.Money)
                            tb.LostFocus += (_, _) => { if (ParseMoney(tb.Text) is decimal m) tb.Text = m.ToString("N0"); };
                        Ui.SetPlaceholder(tb, f.Placeholder ?? "0");
                    }
                    if (f.Kind == FieldKind.Time) Ui.SetPlaceholder(tb, f.Placeholder ?? "HH:mm");
                    if (f.Kind == FieldKind.Decimal) tb.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(ch => char.IsDigit(ch) || ch == '.' || ch == ',');
                    tb.TextChanged += (_, _) => Changed(f.Key);
                    input = tb;
                    break;
            }

            System.Windows.Automation.AutomationProperties.SetAutomationId(input, "Field_" + f.Key);
            System.Windows.Automation.AutomationProperties.SetName(input, f.Label);
            input.LostFocus += (_, _) => { var ui = _fields[f.Key]; ui.Touched = true; ValidateField(ui); };
            container.Children.Add(input);

            var error = new TextBlock { Style = (Style)FindResource("Text.Error"), Visibility = Visibility.Collapsed };
            container.Children.Add(error);
            if (f.Hint != null)
                container.Children.Add(new TextBlock { Text = f.Hint, Style = (Style)FindResource("Text.Caption"), Margin = new Thickness(0, 4, 0, 0) });

            var fieldUi = new FieldUi { Field = f, Container = container, Input = input, Error = error };
            _fields[f.Key] = fieldUi;
            _order.Add(fieldUi);
            _body.Children.Add(container);
            Relayout();
        }

        /// <summary>Đổi chữ gợi ý mặc định "Select a date" của DatePicker sang tiếng Việt.</summary>
        private static void LocalizeDatePicker(DatePicker dp)
        {
            dp.ApplyTemplate();
            if (dp.Template.FindName("PART_TextBox", dp) is System.Windows.Controls.Primitives.DatePickerTextBox box)
            {
                box.ApplyTemplate();
                if (box.Template.FindName("PART_Watermark", box) is ContentControl watermark)
                    watermark.Content = "Chọn ngày (dd/MM/yyyy)";
            }
        }

        private static string FormatInitial(FormField f) => f.Value switch
        {
            null => "",
            decimal d when f.Kind == FieldKind.Money => d.ToString("N0"),
            double dv => dv.ToString("0.#", CultureInfo.InvariantCulture),
            TimeSpan t => t.ToString(@"hh\:mm"),
            DateTime dt => dt.ToString("dd/MM/yyyy"),
            _ => f.Value.ToString() ?? ""
        };

        private static void FillCombo(ComboBox cb, IEnumerable<(string Text, object Value)>? options, object? selected)
        {
            cb.Items.Clear();
            foreach (var (text, value) in options ?? Enumerable.Empty<(string, object)>())
            {
                var item = new ComboBoxItem { Content = text, Tag = value };
                cb.Items.Add(item);
                if (selected != null && Equals(value, selected)) cb.SelectedItem = item;
            }
        }

        /// <summary>Xếp trường vào lưới 2 cột (trường Wide chiếm cả hàng); 1 cột khi hộp thoại hẹp.</summary>
        private void Relayout()
        {
            _body.RowDefinitions.Clear();
            int row = -1, col = 0;
            var rowFull = true; // hàng hiện tại đã kín (hoặc chưa có hàng nào)
            foreach (var ui in _order)
            {
                var wide = ui.Field.Wide || !_twoColumns;
                if (wide || rowFull)
                {
                    row++;
                    _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    col = 0;
                    rowFull = wide;      // trường rộng chiếm cả hàng; trường thường chừa cột phải
                }
                else
                {
                    col = 2;             // cột phải của hàng hiện tại (cột 1 là khoảng cách)
                    rowFull = true;
                }
                Grid.SetRow(ui.Container, row);
                Grid.SetColumn(ui.Container, col);
                Grid.SetColumnSpan(ui.Container, wide ? 3 : 1);
            }
        }

        private void FitToWindow(Window window)
        {
            Width = Math.Max(300, Math.Min(720, window.ActualWidth - 64));
            var two = Width >= 560;
            if (two != _twoColumns) { _twoColumns = two; Relayout(); }
        }

        #endregion

        #region Giá trị

        public object? Get(string key)
        {
            var ui = _fields[key];
            return ui.Input switch
            {
                PasswordBox pb => pb.Password,
                DatePicker dp => dp.SelectedDate,
                ComboBox cb => (cb.SelectedItem as ComboBoxItem)?.Tag,
                CheckBox chk => chk.IsChecked == true,
                ContentControl { Content: WrapPanel wp } => wp.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Tag).ToList(),
                TextBox tb => ui.Field.Kind switch
                {
                    FieldKind.Number => int.TryParse(tb.Text.Trim(), out var n) ? n : null,
                    FieldKind.Decimal => double.TryParse(tb.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl) ? dbl : null,
                    FieldKind.Money => ParseMoney(tb.Text),
                    FieldKind.Time => ParseTime(tb.Text),
                    _ => tb.Text.Trim()
                },
                _ => null
            };
        }

        public string Text(string key) => Get(key) as string ?? "";
        public T? Value<T>(string key) where T : struct => Get(key) is T v ? v : null;

        public void SetOptions(string key, IEnumerable<(string Text, object Value)> options, object? selected = null)
        {
            if (_fields[key].Input is ComboBox cb) FillCombo(cb, options, selected);
        }

        private static decimal? ParseMoney(string text)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? null : decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static TimeSpan? ParseTime(string text) =>
            TimeSpan.TryParseExact(text.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1) ? t : null;

        #endregion

        #region Validation & lưu

        private void Changed(string key)
        {
            if (!_fields.TryGetValue(key, out var ui)) return;
            if (ui.Error.Visibility == Visibility.Visible) ValidateField(ui); // tự xóa lỗi khi người dùng sửa đúng
            ui.Field.OnChanged?.Invoke(this, Get(key));
        }

        /// <summary>Hiển thị lỗi cho một trường (dùng cho lỗi từ phía lưu, vd. trùng tên đăng nhập).</summary>
        public void SetError(string key, string? message)
        {
            var ui = _fields[key];
            ui.Error.Text = message ?? "";
            ui.Error.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            Ui.SetHasError(ui.Input, !string.IsNullOrEmpty(message));
        }

        private bool ValidateField(FieldUi ui)
        {
            var f = ui.Field;
            if (f.ReadOnly) return true;
            string? error = null;
            var value = Get(f.Key);
            var rawText = (ui.Input as TextBox)?.Text.Trim() ?? "";

            var empty = value switch { null => true, string s => s.Length == 0, System.Collections.ICollection c => c.Count == 0, _ => false };
            if (f.Kind is FieldKind.Number or FieldKind.Money or FieldKind.Time or FieldKind.Decimal && value == null && rawText.Length > 0)
                error = f.Kind == FieldKind.Time ? "Giờ không hợp lệ, nhập theo dạng HH:mm (vd. 18:30)." : "Vui lòng nhập số hợp lệ.";
            else if (f.Required && empty)
                error = (f.Kind is FieldKind.Combo or FieldKind.Date or FieldKind.MultiCheck ? "Vui lòng chọn " : "Vui lòng nhập ") + LowerFirst(f.Label) + ".";
            else if (!empty && f.Validate != null)
                error = f.Validate(this, value);

            SetError(f.Key, error);
            return error == null;
        }

        private static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0], CultureInfo.CurrentCulture) + s[1..];

        public void ShowBanner(string? message)
        {
            _bannerText.Text = message ?? "";
            _banner.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private async Task SubmitAsync()
        {
            if (_busy) return;
            var invalid = _order.Where(ui => !ValidateField(ui)).ToList();
            if (invalid.Count > 0)
            {
                ShowBanner(invalid.Count == 1 ? "Vui lòng kiểm tra lại 1 trường chưa hợp lệ." : $"Vui lòng kiểm tra lại {invalid.Count} trường chưa hợp lệ.");
                invalid[0].Input.Focus();
                return;
            }
            ShowBanner(null);

            SetBusy(true);
            try
            {
                if (await _onSubmit(this)) UiHost.CloseDialog(true);
                else if (_banner.Visibility != Visibility.Visible && _order.Any(u => u.Error.Visibility == Visibility.Visible))
                    ShowBanner("Vui lòng kiểm tra lại các trường được đánh dấu.");
            }
            catch (SportsCenter.Core.Services.ValidationException ex)
            {
                if (ex.Field != null && _fields.ContainsKey(ex.Field)) { SetError(ex.Field, ex.Message); ShowBanner("Vui lòng kiểm tra lại các trường được đánh dấu."); }
                else ShowBanner(ex.Message);
            }
            catch (UnauthorizedAccessException ex) { ShowBanner(ex.Message); }
            catch (Exception ex)
            {
                ShowBanner($"Không lưu được dữ liệu: {(ex.InnerException ?? ex).Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _submit.IsEnabled = !busy;
            _submitText.Text = busy ? "Đang lưu..." : _submitLabel;
            _body.IsEnabled = !busy;
        }

        #endregion
    }
}
