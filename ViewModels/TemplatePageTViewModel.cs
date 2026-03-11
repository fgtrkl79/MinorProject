using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace MinorProject.ViewModels;

public partial class TemplatePageTViewModel : ObservableObject
{
    private readonly ObservableCollection<ObservableValue> _chart1Values;
    private readonly ObservableCollection<ObservableValue> _chart2Values;
    [ObservableProperty] private ISeries[] _chart1Series;
    [ObservableProperty] private ISeries[] _chart2Series;
    [ObservableProperty] private ISeries[] _chart3Series;
    [ObservableProperty] private ISeries[] _chart4Series;

    [RelayCommand]
    private void PowerChange()
    {
        Debug.WriteLine("Нажата кнопка: Изменение мощности");
        // Логика...
    }

    [RelayCommand]
    private void ToggleSystem()
    {
        Debug.WriteLine("Нажата кнопка: Вкл/выкл");
        // Логика...
    }

    [RelayCommand]
    private void ShortCircuit()
    {
        Debug.WriteLine("Нажата кнопка: Короткое замыкание");
    }

    [RelayCommand]
    private void Leak()
    {
        Debug.WriteLine("Нажата кнопка: Протечка");
    }

    [RelayCommand]
    private void FanFailure()
    {
        Debug.WriteLine("Нажата кнопка: Выход из строя вентилятора");
    }

    [ObservableProperty]
    private string _oilTemperatureText = string.Empty;

    [ObservableProperty]
    private string _voltageText = string.Empty;

    // подпись/заголовок для графика, можно менять из VM
    [ObservableProperty]
    private string _chartTitle1 = "График T1: мощность (МВт)";

    [ObservableProperty]
    private string _chartTitle2 = "График Т1: температура масла (°C)";

    public TemplatePageTViewModel()
    {
        _chart1Values = new ObservableCollection<ObservableValue>();
        _chart2Values = new ObservableCollection<ObservableValue>();

        for (int i = 0; i < 50; i++)
        {
            _chart1Values.Add(new ObservableValue(0));
            _chart2Values.Add(new ObservableValue(0));
        }

        _chart1Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _chart1Values,
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                LineSmoothness = 0.5
            }
        };

        _chart2Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _chart2Values,
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                LineSmoothness = 0.5
            }
        };

        _chart3Series = Array.Empty<ISeries>();
        _chart4Series = Array.Empty<ISeries>();
    }

    public void AddDataPoint(double power, double oilTemp, double voltage)
    {
        _chart1Values.Add(new ObservableValue(power));

        if (_chart1Values.Count > 50)
        {
            _chart1Values.RemoveAt(0);
        }

        _chart2Values.Add(new ObservableValue(oilTemp));
        if (_chart2Values.Count > 50)
        {
            _chart2Values.RemoveAt(0);
        }
    }
}
