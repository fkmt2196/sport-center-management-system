using System.Windows;
using SportCenter.Wpf.ViewModels;

namespace SportCenter.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
