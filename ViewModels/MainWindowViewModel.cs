using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace MinorProject.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainViewModel TableVm { get; } = new (); 

    // прямой доступ к InfoT1 в таблице
    public TemplatePageTViewModel InfoT1Vm => TableVm.InfoT1;

    // заглушка для T2 – пока не используется
    public TemplatePageTViewModel InfoT2Vm => TableVm.InfoT2; 
    
    public MainWindowViewModel()
    {
        // симуляция выполняется внутри TableVm, здесь пусто
    }

}