using Avalonia;
using Avalonia.Controls;
using ImagePerfect.ViewModels;
using Avalonia.Markup.Xaml;

namespace ImagePerfect.Views;

public partial class BackUpWindow : Window
{
    public BackUpWindow(MainWindowViewModel mainVm)
    {
        InitializeComponent();
        // Bind directly to the existing MainWindowViewModel
        DataContext = mainVm;
    }
}