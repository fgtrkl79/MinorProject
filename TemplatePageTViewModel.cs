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

    private bool _isEmergency;

    public double CurrentPower => _currentPower;
    public double CurrentOilTemp => _currentOilTemp;
    public double CurrentVoltage => _currentVoltage;

    private double _currentPower = 15; 
    private double _currentOilTemp = 45;
    private double _currentVoltage = 220;

    [RelayCommand]
    private void PowerChange()
    {
        if (_isEmergency)
        {
            Debug.WriteLine("Изменение мощности невозможно — авария!");
            return;
        }

        int delta;
        do
        {
            delta = _random.Next(-5, 6); 
        }
        while (delta == 0); 

        _currentPower += delta;

        
        if (_currentPower < 5) _currentPower = 5;
        if (_currentPower > 25) _currentPower = 25;

        
        _currentOilTemp += _random.Next(-2, 3);
        _currentVoltage += _random.Next(-3, 4);

        Debug.WriteLine($"Изменение мощности: {_currentPower}");

        AddDataPoint(_currentPower, _currentOilTemp, _currentVoltage);

        OilTemperatureText = $"{_currentOilTemp:F1} °C";
        VoltageText = $"{_currentVoltage:F0} В";
    }

    private bool _systemEnabled;

    [RelayCommand]
    private void ToggleSystem()
    {
        _systemEnabled = !_systemEnabled;

        Debug.WriteLine(_systemEnabled
            ? "Система включена"
            : "Система выключена");
        if (_systemEnabled)
        {
            _isEmergency = false;
            AddDataPoint(power: 50, oilTemp: 45, voltage: 220);
        }
        else
        {
            _isEmergency = true;
            AddDataPoint(power: 0, oilTemp: 30, voltage: 0);
        }
    }

    [RelayCommand]
    private void ShortCircuit()
    {
        Debug.WriteLine("Авария: короткое замыкание!");

        _isEmergency = true;

        _currentPower = 0;
        _currentVoltage = 0;
        _currentOilTemp = 120;

        AddDataPoint(_currentPower, _currentOilTemp, _currentVoltage);

        OilTemperatureText = "120 °C";
        VoltageText = "0 В";
    }

    [RelayCommand]
    private void Leak()
    {
        Debug.WriteLine("Авария: протечка масла!");

        _isEmergency = true;

        AddDataPoint(power: 20, oilTemp: 150, voltage: 210);

        OilTemperatureText = "150 °C";
        VoltageText = "210 В";
    }

    [RelayCommand]
    private void FanFailure()
    {
        Debug.WriteLine("Авария: выход из строя вентилятора!");

        _isEmergency = true;

        double temp = _random.Next(100, 160);

        AddDataPoint(power: 40, oilTemp: temp, voltage: 220);

        OilTemperatureText = $"{temp} °C";
        VoltageText = "220 В";
    }

    [RelayCommand]
    private void Repair()
    {
        Debug.WriteLine("Починка: авария сброшена");

        _isEmergency = false;

        _currentPower = 50;
        _currentOilTemp = 60;
        _currentVoltage = 220;

        AddDataPoint(_currentPower, _currentOilTemp, _currentVoltage);
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

    private readonly Random _random = new();

    [ObservableProperty]
    private double _value = 52;

    public Func<double, string> Labeler { get; set; } =
        value => value.ToString("N1");

    [RelayCommand]
    public void DoRandomChange()
    {
        Value = _random.Next(0, 100);

        Console.WriteLine(Value);
        
    }

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

        StartAutoUpdate(); 
    }


    private async void StartAutoUpdate()
    {
        while (true)
        {
            await Task.Delay(500);

            if (!_isEmergency)
            {

                _currentPower += (_random.NextDouble() * 2 - 1);
                if (_currentPower < 2) _currentPower = 2;
                if (_currentPower > 25) _currentPower = 25;

                _currentOilTemp += (_random.NextDouble() * 0.4 - 0.2);

                _currentVoltage += (_random.NextDouble() * 2 - 1);
                if (_currentVoltage < 210) _currentVoltage = 210;
                if (_currentVoltage > 240) _currentVoltage = 240;
            }
            
            AddDataPoint(_currentPower, _currentOilTemp, _currentVoltage);

            OilTemperatureText = $"{_currentOilTemp:F1} °C";
            VoltageText = $"{_currentVoltage:F0} В";
        }
    }

    public void AddDataPoint(double power, double oilTemp, double voltage)
    {
        if (_isEmergency)
        {
            power = 0;
            voltage = 0;
        }
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
