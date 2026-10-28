using ExifInjector.App.ViewModels;
using Microsoft.UI.Xaml;

namespace ExifInjector.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
