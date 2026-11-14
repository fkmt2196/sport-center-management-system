using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Project.Frontend.Shell;

namespace Project.Frontend.Controls
{
    public enum ColKind { Text, TwoLine, Avatar, Badge, Progress }

    /// <summary>Khai báo một cột. Path/Path2 là tên property của dòng.</summary>
    public record Col(string Header, string Path, ColKind Kind = ColKind.Text)
    {
        public string? Path2 { get; init; }
        public string? BadgeKind { get; init; }
        public DataGridLength Width { get; init; } = new(1, DataGridLengthUnitType.Star);
        public double MinWidth { get; init; } = 60;
        public double HideBelow { get; init; }
        public string? SortPath { get; init; }
        public bool Bold { get; init; }
        public static DataGridLength Px(double w) => new(w);
        public static DataGridLength Star(double s) => new(s, DataGridLengthUnitType.Star);
    }

    /// <summary>Bộ lọc dạng combobox. Options được tính lại sau mỗi lần tải dữ liệu (nếu OptionsFrom có).</summary>
    public record Filter<T>(string AllText, Func<T, object?, bool> Match)
    {
        public IEnumerable<(string Text, object Value)>? Options { get; init; }
        public Func<IReadOnlyList<T>, IEnumerable<(string Text, object Value)>>? OptionsFrom { get; init; }
        public double Width { get; init; } = 180;
        /// <summary>Giá trị chọn sẵn khi mở trang.</summary>
        public object? Default { get; init; }
    }

    /// <summary>Nút ở góc phải tiêu đề trang. Run trả về true thì tải lại bảng.</summary>
    public record HeaderAction(string Text, string Icon, Func<Task<bool>> Run, bool Primary = true);

    /// <summary>Nút thao tác trên từng dòng. Run trả về true thì tải lại bảng.</summary>
    public record RowAction<T>(string Icon, string Tooltip, Func<T, Task<bool>> Run)
    {
        public bool Danger { get; init; }
        public Func<T, bool>? Visible { get; init; }
    }

    public class TableSpec<T> where T : class
    {
        public required string Title { get; init; }
        public string? Subtitle { get; init; }
        public string EmptyIcon { get; init; } = Icons.Info;
        public string EmptyText { get; init; } = "Không có dữ liệu";
        public required Func<Task<IReadOnlyList<T>>> Load { get; init; }
        public required Func<T, string> Search { get; init; }
        public string SearchPlaceholder { get; init; } = "Tìm kiếm...";
        public List<Col> Columns { get; init; } = new();
        public List<Filter<T>> Filters { get; init; } = new();
        public List<HeaderAction> Actions { get; init; } = new();
        public List<RowAction<T>> RowActions { get; init; } = new();
        /// <summary>Hành động khi nhấp đúp dòng (thường là xem/sửa).</summary>
        public Func<T, Task<bool>>? Open { get; init; }
        public string? DefaultSort { get; init; }
        public ListSortDirection SortDirection { get; init; } = ListSortDirection.Ascending;
        /// <summary>Đơn vị đếm hiển thị dưới tiêu đề, vd. "thành viên".</summary>
        public string Unit { get; init; } = "mục";
        /// <summary>Phần nội dung tùy biến hiển thị giữa tiêu đề và bảng (vd. thẻ thống kê).</summary>
        public FrameworkElement? Banner { get; init; }
    }

    /// <summary>
    /// Trang danh sách dùng chung: tiêu đề + nút, thanh tìm kiếm/lọc, bảng có sắp xếp, phân trang,
    /// badge trạng thái, nút thao tác có icon. Toàn bộ cấu hình qua TableSpec.
    /// </summary>
    public class TablePage<T> : UserControl where T : class
    {
        private readonly TableSpec<T> _spec;
        private readonly DataGrid _grid = new();
        private readonly TextBlock _count = new();
        private readonly List<(Filter<T> Spec, ComboBox Box)> _filters = new();
        private readonly ListController<T> _list;
        private readonly TextBox _search = new();
        private bool _loading;

        public TablePage(TableSpec<T> spec)
        {
            _spec = spec;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            // ---- Tiêu đề ----
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var a in spec.Actions)
            {
                var btn = IconTextButton(a.Text, a.Icon, a.Primary ? "Btn.Primary" : "Btn.Secondary");
                btn.Margin = new Thickness(10, 4, 0, 4);
                btn.Click += async (_, _) => { if (await Safe(a.Run)) await ReloadAsync(); };
                actions.Children.Add(btn);
            }
            DockPanel.SetDock(actions, Dock.Right);
            header.Children.Add(actions);
            var titles = new StackPanel();
            titles.Children.Add(new TextBlock { Text = spec.Title, Style = Res<Style>("Text.H1") });
            _count.Style = Res<Style>("Text.Secondary");
            _count.Margin = new Thickness(0, 4, 0, 0);
            _count.Text = spec.Subtitle ?? "Đang tải...";
            titles.Children.Add(_count);
            header.Children.Add(titles);
            root.Children.Add(header);

            if (spec.Banner != null)
            {
                Grid.SetRow(spec.Banner, 1);
                spec.Banner.Margin = new Thickness(0, 0, 0, 16);
                root.Children.Add(spec.Banner);
            }

            // ---- Thẻ: toolbar / bảng / phân trang ----
            var card = new Border { Style = Res<Style>("Card"), Padding = new Thickness(0) };
            Grid.SetRow(card, 2);
            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition());
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var toolbar = new Grid { Margin = new Thickness(16) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition());
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Ui.SetIcon(_search, Icons.Search);
            Ui.SetPlaceholder(_search, spec.SearchPlaceholder);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_search, "SearchBox");
            toolbar.Children.Add(_search);
            var filters = new WrapPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(filters, 1);
            foreach (var f in spec.Filters)
            {
                var box = new ComboBox { Width = f.Width, ToolTip = f.AllText };
                ListPage.FillFilter(box, f.AllText, f.Options ?? Array.Empty<(string, object)>());
                if (f.Default != null)
                    foreach (ComboBoxItem it in box.Items) if (Equals(it.Tag, f.Default)) { box.SelectedItem = it; break; }
                box.SelectionChanged += (_, _) => { if (!_loading) _list?.Apply(); };
                _filters.Add((f, box));
                filters.Children.Add(box);
            }
            var refresh = new Button { Style = Res<Style>("Btn.Secondary"), Width = 38, Padding = new Thickness(0), ToolTip = "Tải lại", Content = Glyph(Icons.Refresh) };
            refresh.Click += async (_, _) => await ReloadAsync();
            filters.Children.Add(refresh);
            toolbar.Children.Add(filters);
            cardGrid.Children.Add(toolbar);
            ListPage.MakeToolbarResponsive(this, toolbar, _search, filters);

            _grid.BorderBrush = Res<Brush>("LineBrush");
            _grid.BorderThickness = new Thickness(0, 1, 0, 0);
            Responsive.SetEnabled(_grid, true);
            Grid.SetRow(_grid, 1);
            BuildColumns();
            cardGrid.Children.Add(_grid);

            var empty = new Border { Visibility = Visibility.Collapsed, BorderBrush = Res<Brush>("LineBrush"), BorderThickness = new Thickness(0, 1, 0, 0) };
            empty.Child = new EmptyState(spec.EmptyIcon, spec.EmptyText, "Thử đổi từ khóa tìm kiếm hoặc bộ lọc.");
            Grid.SetRow(empty, 1);
            cardGrid.Children.Add(empty);

            var pager = new Pager();
            var pagerHost = new Border { BorderBrush = Res<Brush>("LineBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 10, 16, 10), Child = pager };
            Grid.SetRow(pagerHost, 2);
            cardGrid.Children.Add(pagerHost);
            card.Child = cardGrid;
            root.Children.Add(card);
            Content = root;

            _list = new ListController<T>(_grid, pager, empty, spec.Search, spec.DefaultSort, spec.SortDirection)
            {
                Filter = row => _filters.All(f => ListPage.SelectedValue(f.Box) is not { } v || f.Spec.Match(row, v))
            };
            _list.Changed += (shown, total) =>
                _count.Text = (spec.Subtitle != null ? spec.Subtitle + " · " : "")
                              + (shown == total ? $"{total} {spec.Unit}" : $"{shown} / {total} {spec.Unit} phù hợp bộ lọc");
            _search.TextChanged += (_, _) => _list.SetSearch(_search.Text);

            // Nút trong cột thao tác: Tag = RowAction
            _grid.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(async (_, e) =>
            {
                if (e.OriginalSource is Button { Tag: RowAction<T> action, DataContext: T row })
                {
                    e.Handled = true;
                    if (await Safe(() => action.Run(row))) await ReloadAsync();
                }
            }));
            if (spec.Open != null)
                _grid.MouseDoubleClick += async (_, e) =>
                {
                    if (e.OriginalSource is DependencyObject d && FindParent<DataGridRow>(d) is { Item: T row })
                        if (await Safe(() => spec.Open(row))) await ReloadAsync();
                };

            Loaded += async (_, _) => { if (!_loadedOnce) { _loadedOnce = true; await ReloadAsync(); } };
        }

        private bool _loadedOnce;

        public async Task ReloadAsync()
        {
            try
            {
                var rows = await _spec.Load();
                _loading = true;
                foreach (var (f, box) in _filters.Where(x => x.Spec.OptionsFrom != null))
                    ListPage.FillFilter(box, f.AllText, f.OptionsFrom!(rows));
                _loading = false;
                _list.SetItems(rows);
            }
            catch (Exception ex)
            {
                _loading = false;
                _count.Text = "Không tải được dữ liệu";
                UiHost.Toast($"Không tải được dữ liệu: {(ex.InnerException ?? ex).Message}", ToastKind.Error);
            }
        }

        /// <summary>Chạy thao tác, báo lỗi nghiệp vụ bằng toast.</summary>
        private static async Task<bool> Safe(Func<Task<bool>> run)
        {
            try { return await run(); }
            catch (SportsCenter.BLL.Services.ValidationException ex) { UiHost.Toast(ex.Message, ToastKind.Warning); }
            catch (UnauthorizedAccessException ex) { UiHost.Toast(ex.Message, ToastKind.Error); }
            catch (Exception ex) { UiHost.Toast($"Lỗi: {(ex.InnerException ?? ex).Message}", ToastKind.Error); }
            return false;
        }

        #region Cột

        private void BuildColumns()
        {
            foreach (var c in _spec.Columns)
            {
                DataGridColumn col = c.Kind == ColKind.Text && !c.Bold
                    ? new DataGridTextColumn
                    {
                        Binding = new Binding(c.Path),
                        ElementStyle = TextStyle(false)
                    }
                    : new DataGridTemplateColumn { CellTemplate = new DataTemplate { VisualTree = CellFactory(c) } };
                col.Header = c.Header;
                col.Width = c.Width;
                col.MinWidth = c.Kind == ColKind.Badge ? Math.Max(c.MinWidth, 128) : c.MinWidth;
                if (c.Kind == ColKind.Badge && c.Width.IsAbsolute && c.Width.Value < 128) col.Width = new DataGridLength(128);
                col.SortMemberPath = c.SortPath ?? c.Path;
                if (c.HideBelow > 0) Responsive.SetHideBelow(col, c.HideBelow);
                _grid.Columns.Add(col);
            }
            if (_spec.RowActions.Count > 0)
            {
                var panel = new FrameworkElementFactory(typeof(StackPanel));
                panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                foreach (var a in _spec.RowActions)
                {
                    var b = new FrameworkElementFactory(typeof(Button));
                    b.SetValue(StyleProperty, Res<Style>(a.Danger ? "Btn.IconDanger" : "Btn.Icon"));
                    b.SetValue(ContentControl.ContentProperty, a.Icon);
                    b.SetValue(ToolTipProperty, a.Tooltip);
                    b.SetValue(TagProperty, a);
                    b.SetValue(MarginProperty, new Thickness(0, 0, 4, 0));
                    b.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, a.Tooltip);
                    if (a.Visible != null)
                        b.SetBinding(VisibilityProperty, new Binding { Converter = new FuncConverter(o => o is T r && a.Visible(r) ? Visibility.Visible : Visibility.Collapsed) });
                    panel.AppendChild(b);
                }
                _grid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = "Thao tác", CanUserSort = false, CellTemplate = new DataTemplate { VisualTree = panel },
                    Width = new DataGridLength(Math.Max(92, _spec.RowActions.Count * 38 + 20)), MinWidth = Math.Max(92, _spec.RowActions.Count * 38 + 20)
                });
            }
        }

        private static Style TextStyle(bool bold)
        {
            var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            s.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
            if (bold) s.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            return s;
        }

        private static FrameworkElementFactory Text(string path, bool bold = false, bool caption = false)
        {
            var t = new FrameworkElementFactory(typeof(TextBlock));
            t.SetBinding(TextBlock.TextProperty, new Binding(path));
            t.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            if (bold) t.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            if (caption) t.SetValue(StyleProperty, Res<Style>("Text.Caption"));
            if (caption) t.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            return t;
        }

        private static FrameworkElementFactory CellFactory(Col c)
        {
            switch (c.Kind)
            {
                case ColKind.Badge:
                {
                    var b = new FrameworkElementFactory(typeof(StatusBadge));
                    b.SetValue(StatusBadge.KindProperty, c.BadgeKind ?? "");
                    b.SetBinding(StatusBadge.StatusProperty, new Binding(c.Path));
                    return b;
                }
                case ColKind.TwoLine:
                {
                    var p = new FrameworkElementFactory(typeof(StackPanel));
                    p.SetValue(MarginProperty, new Thickness(0, 7, 0, 7));
                    p.AppendChild(Text(c.Path, bold: c.Bold));
                    if (c.Path2 != null) p.AppendChild(Text(c.Path2, caption: true));
                    return p;
                }
                case ColKind.Avatar:
                {
                    var row = new FrameworkElementFactory(typeof(StackPanel));
                    row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                    row.SetValue(MarginProperty, new Thickness(0, 7, 0, 7));
                    var circle = new FrameworkElementFactory(typeof(Border));
                    circle.SetValue(StyleProperty, Res<Style>("Avatar"));
                    var initials = new FrameworkElementFactory(typeof(TextBlock));
                    initials.SetValue(StyleProperty, Res<Style>("AvatarText"));
                    initials.SetBinding(TextBlock.TextProperty, new Binding(c.Path) { Converter = new FuncConverter(o => AppInfo.Initials(o as string ?? "")) });
                    circle.AppendChild(initials);
                    row.AppendChild(circle);
                    var texts = new FrameworkElementFactory(typeof(StackPanel));
                    texts.SetValue(MarginProperty, new Thickness(10, 0, 0, 0));
                    texts.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
                    texts.AppendChild(Text(c.Path, bold: true));
                    if (c.Path2 != null) texts.AppendChild(Text(c.Path2, caption: true));
                    row.AppendChild(texts);
                    return row;
                }
                case ColKind.Progress:
                {
                    var p = new FrameworkElementFactory(typeof(StackPanel));
                    var t = Text(c.Path);
                    t.SetValue(TextBlock.FontSizeProperty, 12.0);
                    p.AppendChild(t);
                    var bar = new FrameworkElementFactory(typeof(ProgressBar));
                    bar.SetBinding(RangeBase.ValueProperty, new Binding(c.Path2) { Mode = BindingMode.OneWay });
                    bar.SetValue(RangeBase.MaximumProperty, 100.0);
                    bar.SetValue(HeightProperty, 5.0);
                    bar.SetValue(WidthProperty, 80.0);
                    bar.SetValue(MarginProperty, new Thickness(0, 4, 0, 0));
                    bar.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    bar.SetValue(BorderThicknessProperty, new Thickness(0));
                    bar.SetValue(ForegroundProperty, Res<Brush>("PrimaryBrush"));
                    bar.SetValue(BackgroundProperty, Res<Brush>("PrimarySoft2Brush"));
                    p.AppendChild(bar);
                    return p;
                }
                default:
                    return Text(c.Path, bold: c.Bold);
            }
        }

        #endregion

        #region Tiện ích

        public static Button IconTextButton(string text, string icon, string style)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = icon, Style = Res<Style>("Text.Icon"), Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Content = sp, Style = Res<Style>(style) };
            System.Windows.Automation.AutomationProperties.SetName(b, text);
            return b;
        }

        private static TextBlock Glyph(string g) => new() { Text = g, Style = Res<Style>("Text.Icon") };

        private static TRes Res<TRes>(string key) => (TRes)Application.Current.FindResource(key);

        private static TParent? FindParent<TParent>(DependencyObject d) where TParent : DependencyObject
        {
            while (d != null && d is not TParent) d = VisualTreeHelper.GetParent(d);
            return d as TParent;
        }

        #endregion
    }

    /// <summary>Converter từ lambda (dùng trong template dựng bằng code).</summary>
    public class FuncConverter : IValueConverter
    {
        private readonly Func<object?, object?> _f;
        public FuncConverter(Func<object?, object?> f) { _f = f; }
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => _f(value);
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
