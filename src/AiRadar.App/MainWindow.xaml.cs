using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using AiRadar.App.ViewModels;

namespace AiRadar.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.InitializeAsync();

    private async void OnSaveTokenClick(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password;
        TokenBox.Clear();
        await _viewModel.SaveTokenAsync(token);
    }

    private void OnToolDoubleClick(object sender, MouseButtonEventArgs e) =>
        _viewModel.OpenToolCommand.Execute(ToolList.SelectedItem);

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
