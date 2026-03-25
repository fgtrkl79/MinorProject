using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MinorProject.Models;

namespace MinorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public TransformerModel T1 { get; } = new();
    public TransformerModel T2 { get; } = new();

    // Ссылка на ту самую модель T1, которая привязана к вкладке с ползунком
    private readonly TemplatePageTViewModel _infoT1;
    
    // В конструкторе мы ПРИНИМАЕМ infoT1
    public MainViewModel(TemplatePageTViewModel infoT1)
    {
        _infoT1 = infoT1;

        // Исходные (заглушечные) данные для Т2, так как он пока "в разработке"
        T2.Voltage = "220 В";
        T2.OilTemperature = "50.0 °C";
        T2.Pressure = "4.1 атм";
        T2.Power = "10.0 МВА";
        
        SimulateLiveUpdates();
    }

    private async void SimulateLiveUpdates()
    {
        while (true)
        {
            await Task.Delay(500);

            // Обновляем UI-поток
            Dispatcher.UIThread.Post(() =>
            {
                // Берем живые данные из _infoT1 (где работает ползунок и автоматика)
                T1.Power = $"{_infoT1.CurrentPower:F1} МВА";
                T1.OilTemperature = $"{_infoT1.CurrentOilTemp:F1} °C";
                T1.Voltage = $"{_infoT1.CurrentVoltage:F0} В";
                
                // Добавили давление, которое есть в методичке
                // (Если компилятор ругается на CurrentPressure - убедитесь, что в TemplatePageTViewModel 
                // есть строчка: public double CurrentPressure => _currentPressure;)
                T1.Pressure = $"{_infoT1.CurrentPressure:F1} атм";
                
                // Для красоты добавим легкий "шум" для Т2, чтобы он тоже казался живым
                double t2Temp = 50.0 + (new Random().NextDouble() * 0.4 - 0.2);
                T2.OilTemperature = $"{t2Temp:F1} °C";
            });
        }
    }
}