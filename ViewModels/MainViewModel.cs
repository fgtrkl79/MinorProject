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
        var random = new Random();
        double currentPowerT1 = 15.0; 
        double currentTempT1 = 45.0;
        double currentVoltageT1 = 220.0; // добавляем напряжение

        while (true)
        {
            await Task.Delay(500);
            
            currentTempT1 += (random.NextDouble() * 0.4) - 0.2;
            currentPowerT1 += (random.NextDouble() * 2) - 1;
            currentVoltageT1 += (random.NextDouble() * 2) - 1; // напряжение меняется на ±1 кВ
            
            if (currentPowerT1 < 5) currentPowerT1 = 5;
            if (currentPowerT1 > 25) currentPowerT1 = 25;
            if (currentVoltageT1 < 210) currentVoltageT1 = 210;
            if (currentVoltageT1 > 240) currentVoltageT1 = 240;
            
            Dispatcher.UIThread.Post(() =>
            {
                        T1.Power = $"{currentPowerT1:F1} МВт";
                T1.OilTemperature = $"{currentTempT1:F1} °C";
                // добавляем точку в график на вкладке T1 с мощностью, температурой и напряжением
                InfoT1.AddDataPoint(currentPowerT1, currentTempT1, currentVoltageT1);
            });
        }

    }
}