using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MinorProject.ViewModels;

namespace MinorProject.Views;

public partial class MainTable : UserControl
{
    public MainTable()
    {
        InitializeComponent();
        // DataContext задаётся извне (MainWindow) через привязку TableVm
    }
}