using CommunityToolkit.Mvvm.ComponentModel;

namespace MinorProject.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Вью-модель для вкладки Т1
    public TemplatePageTViewModel InfoT1Vm { get; }
    
    // Вью-модель для Главной таблицы
    public MainViewModel TableVm { get; }

    public MainWindowViewModel()
    {
        // 1. Создаем двигатель логики Т1 (с ползунком и графиками)
        InfoT1Vm = new TemplatePageTViewModel();

        // 2. Передаем этот ЖЕ двигатель в таблицу, чтобы она брала оттуда цифры
        TableVm = new MainViewModel(InfoT1Vm);
    }
}