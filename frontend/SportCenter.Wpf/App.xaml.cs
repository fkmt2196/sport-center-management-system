using System.Windows;
using SportCenter.BLL.Services;
using SportCenter.DAL.Repositories;
using SportCenter.Wpf.Services;
using SportCenter.Wpf.ViewModels.Members;
using SportCenter.Wpf.Views;

namespace SportCenter.Wpf;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Composition root: replace the sample repository and session when SQL/auth modules are ready.
        var service = new MemberDashboardService(new DemoMemberReadRepository());
        var viewModel = new MemberDashboardViewModel(service, new DemoMemberSession());
        var window = new MainWindow { DataContext = viewModel };
        window.Closed += (_, _) => viewModel.Dispose();
        MainWindow = window;
        window.Show();
        await viewModel.LoadAsync();
    }
}
