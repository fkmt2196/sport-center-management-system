using System.Windows;
using System.Windows.Controls;

namespace SportCenter.Wpf.Views.Members;

public partial class MemberDashboardView : UserControl
{
    public MemberDashboardView() => InitializeComponent();

    private void OnNavigate(object sender, RoutedEventArgs e) => DashboardScroll.ScrollToTop();
}
