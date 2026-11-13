using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Project.Frontend.Data;

namespace Project.Frontend.Controls
{
    /// <summary>
    /// Điều khiển một bảng: tìm kiếm (không dấu) + bộ lọc + sắp xếp theo cột (trên toàn bộ dữ liệu) + phân trang.
    /// </summary>
    public class ListController<T> where T : class
    {
        private readonly DataGrid _grid;
        private readonly Pager _pager;
        private readonly FrameworkElement _emptyState;
        private readonly Func<T, string> _searchText;
        private readonly Dictionary<T, string> _folded = new(ReferenceEqualityComparer.Instance);
        private List<T> _all = new();
        private List<T> _filtered = new();
        private string _search = "";
        private string? _sortPath;
        private ListSortDirection _sortDir;

        public int Page { get; private set; } = 1;
        public int PageSize { get; private set; } = Pager.PageSizes[0];
        public Func<T, bool>? Filter { get; set; }
        /// <summary>Gọi sau mỗi lần lọc: (số kết quả sau lọc, tổng số).</summary>
        public event Action<int, int>? Changed;

        public ListController(DataGrid grid, Pager pager, FrameworkElement emptyState, Func<T, string> searchText,
                              string? defaultSort = null, ListSortDirection defaultDir = ListSortDirection.Ascending)
        {
            _searchText = searchText;
            _grid = grid;
            _pager = pager;
            _emptyState = emptyState;
            _sortPath = defaultSort;
            _sortDir = defaultDir;

            _grid.Sorting += OnSorting;
            _pager.PageRequested += p => { Page = p; Render(); };
            _pager.PageSizeChanged += s => { PageSize = s; Page = 1; Render(); };
        }

        public IReadOnlyList<T> All => _all;

        public void SetItems(IEnumerable<T> items)
        {
            _all = items.ToList();
            _folded.Clear();
            foreach (var item in _all) _folded[item] = Text.Fold(_searchText(item));
            Apply(resetPage: false);
        }

        public void SetSearch(string text)
        {
            _search = Text.Fold(text.Trim());
            Apply(resetPage: true);
        }

        public void Apply(bool resetPage = true)
        {
            IEnumerable<T> q = _all;
            if (_search.Length > 0)
            {
                var terms = _search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                q = q.Where(r => terms.All(t => _folded[r].Contains(t)));
            }
            if (Filter != null) q = q.Where(Filter);
            if (_sortPath != null)
            {
                var prop = typeof(T).GetProperty(_sortPath, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    q = _sortDir == ListSortDirection.Ascending ? q.OrderBy(r => prop.GetValue(r)) : q.OrderByDescending(r => prop.GetValue(r));
            }
            _filtered = q.ToList();
            if (resetPage) Page = 1;
            Render();
            Changed?.Invoke(_filtered.Count, _all.Count);
        }

        private void Render()
        {
            var pageCount = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            Page = Math.Clamp(Page, 1, pageCount);
            _grid.ItemsSource = _filtered.Skip((Page - 1) * PageSize).Take(PageSize).ToList();
            MarkSortedColumn(); // DataGrid xóa SortDirection mỗi khi đổi ItemsSource
            _pager.Update(Page, pageCount, _filtered.Count, PageSize);
            var empty = _filtered.Count == 0;
            _emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _grid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            Responsive.Apply(_grid);
        }

        private void OnSorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true; // sắp xếp toàn bộ danh sách, không chỉ trang hiện tại
            var path = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(path)) return;
            _sortDir = _sortPath == path && _sortDir == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            _sortPath = path;
            Apply(resetPage: true);
        }

        private void MarkSortedColumn()
        {
            foreach (var c in _grid.Columns)
                c.SortDirection = c.SortMemberPath == _sortPath ? _sortDir : null;
        }
    }

    /// <summary>Quyền thao tác trên một trang danh sách.</summary>
    public record PagePermissions(bool CanCreate, bool CanEdit, bool CanDelete)
    {
        public static readonly PagePermissions Full = new(true, true, true);
        public static readonly PagePermissions ReadOnly = new(false, false, false);
        public bool HasRowActions => CanEdit || CanDelete;
    }
}
