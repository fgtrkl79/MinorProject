using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MinorProject.Models;

namespace MinorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public TransformerModel T1 { get; } = new ();
    public TransformerModel T2 { get; } = new ();

    // информация для страницы T1. свойство, чтобы можно было привязаться из других VM/Views
    public TemplatePageTViewModel InfoT1 { get; } = new TemplatePageTViewModel
    {
        OilTemperatureText = "45°C",
        VoltageText = "220kV"
    };

    // задел на будущее: инфа для Т2 пока пустая
    public TemplatePageTViewModel InfoT2 { get; } = new TemplatePageTViewModel();
    
    public MainViewModel()
    {
        T1.Voltage = "220 кВ";
        T1.OilTemperature = "45 °C";
        T1.Pressure = "Норма";
        T1.Power = "15 МВт";

        T2.Voltage = "110 кВ";
        T2.OilTemperature = "50 °C";
        T2.Pressure = "Норма";
        T2.Power = "10 МВт";
        
        SimulateLiveUpdates();
    }

    private async void SimulateLiveUpdates()
    {
        while (true)
        {
            await Task.Delay(500);

            Dispatcher.UIThread.Post(() =>
            {
                T1.Power = $"{InfoT1.CurrentPower:F1} МВт";
                T1.OilTemperature = $"{InfoT1.CurrentOilTemp:F1} °C";
                T1.Voltage = $"{InfoT1.CurrentVoltage:F0} кВ";
            });
        }
    }
}