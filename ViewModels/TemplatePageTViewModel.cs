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

    public Axis[] Chart1YAxes { get; set; }
    public Axis[] Chart2YAxes { get; set; }

    // --- ОСНОВНЫЕ ПАРАМЕТРЫ ---
    private double _currentPower = 25; // Фактическая мощность (с учетом шума)
    private double _currentOilTemp = 45; // °C
    private double _currentVoltage = 220; // В
    private double _currentPressure = 4.0; // Давление (атм)

    public double CurrentPower => _currentPower;
    public double CurrentOilTemp => _currentOilTemp;
    public double CurrentVoltage => _currentVoltage;
    public double CurrentPressure => _currentPressure;

    // Свойство для ползунка мощности
    [ObservableProperty] private double _operatorPower = 25;

    // Метод, который вызывается АВТОМАТИЧЕСКИ, когда вы двигаете ползунок
    partial void OnOperatorPowerChanged(double value)
    {
        if (_isEmergency || !_systemEnabled) return;

        _currentPower = value;
        CheckProtectionsAndCooling();
    }

    // --- ФЛАГИ СОСТОЯНИЯ ---
    private bool _isEmergency;
    private bool _systemEnabled = true;
    private bool _isLeak; // Протечка масла
    private bool _isFanFailed; // Поломка вентилятора

    [ObservableProperty] private bool _isPumpOn;
    [ObservableProperty] private bool _isFanOn;

    // --- ТЕКСТЫ ДЛЯ UI ---
    [ObservableProperty] private string _oilTemperatureText = string.Empty;
    [ObservableProperty] private string _voltageText = string.Empty;
    [ObservableProperty] private string _pressureText = string.Empty;
    [ObservableProperty] private string _systemStatusText = "Статус: НОРМА";
    [ObservableProperty] private string _coolingStatusText = "Охлаждение: Отключено";
    [ObservableProperty] private string _chartTitle1 = "График T1: Мощность (МВА)";
    [ObservableProperty] private string _chartTitle2 = "График Т1: Температура масла (°C)";

    private readonly Random _random = new();

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
                Values = _chart1Values, Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                Fill = null, GeometryFill = null, GeometryStroke = null, LineSmoothness = 0.5
            }
        };
        _chart2Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _chart2Values, Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 }, Fill = null,
                GeometryFill = null, GeometryStroke = null, LineSmoothness = 0.5
            }
        };

        Chart1YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 84,
                MinStep = 10,
            }
        };

        Chart2YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,

                MaxLimit = 150,
                MinStep = 25
            }
        };

        StartAutoUpdate();
    }

    private void CheckProtectionsAndCooling()
    {
        if (_isEmergency || !_systemEnabled) return;

        // Берем ТЕКУЩЕЕ состояние насосов и вентиляторов
        bool needsPump = IsPumpOn;
        bool needsFan = IsFanOn;

        // 1. Аварийная защита по мощности (отключение выключателей)
        if (_currentPower > 63)
        {
            TriggerEmergency("АВАРИЯ: Превышение мощности (>63 МВА). Отключение трансформатора!");
            return;
        }

        // 2. Аварийная защита по температуре
        if (_currentOilTemp > 100)
        {
            TriggerEmergency("АВАРИЯ: Перегрев (>100 °C). Отключение трансформатора!");
            return;
        }

        // === ГИСТЕРЕЗИС ОХЛАЖДЕНИЯ (Зависит ТОЛЬКО от температуры) ===
    
        // Логика насосов
        if (_currentOilTemp >= 80) needsPump = true;      // Включаем при 80
        else if (_currentOilTemp <= 70) needsPump = false; // Выключаем, когда охладили до 70

        // Логика вентиляторов
        if (_currentOilTemp >= 90) needsFan = true;       // Включаем при 90
        else if (_currentOilTemp <= 80) needsFan = false; // Выключаем, когда охладили до 80

        // Учет физических поломок (если сломаны — принудительно выключаем)
        if (_isLeak) needsPump = false;
        if (_isFanFailed) needsFan = false;

        // Применяем новые состояния
        IsPumpOn = needsPump;
        IsFanOn = needsFan;

        // Обновляем текст для UI
        if (IsPumpOn && IsFanOn) CoolingStatusText = "Охлаждение: Насосы + Вентиляторы";
        else if (IsPumpOn) CoolingStatusText = "Охлаждение: Только насосы";
        else CoolingStatusText = "Охлаждение: Естественное (Выкл)";
    }

    private void TriggerEmergency(string reason)
    {
        Debug.WriteLine(reason);
        _isEmergency = true;
        _currentPower = 0;
        OperatorPower = 0; // Ползунок прыгнет в 0
        _currentVoltage = 0;
        IsPumpOn = false;
        IsFanOn = false;
        SystemStatusText = $"СТАТУС: {reason}";
        CoolingStatusText = "Охлаждение: АВАРИЯ";
    }

    private async void StartAutoUpdate()
    {
        while (true)
        {
            await Task.Delay(500);

            if (_systemEnabled)
            {
                if (!_isEmergency)
                {
                    // Добавляем чуть-чуть реалистичного "шума" к заданному ползунком значению
                    _currentPower = OperatorPower + (_random.NextDouble() * 0.8 - 0.4);
                    if (_currentPower < 0) _currentPower = 0;

                    _currentVoltage += (_random.NextDouble() * 2 - 1);
                    if (_currentVoltage < 210) _currentVoltage = 210;
                    if (_currentVoltage > 240) _currentVoltage = 240;

                    if (!_isLeak) _currentPressure = 4.0 + (_random.NextDouble() * 0.2 - 0.1);
                    else _currentPressure = 0.0;

                    CheckProtectionsAndCooling();
                }

                // Целевая температура (увеличили коэффициент нагрева до 1.5)
                double targetTemp = 30.0 + (_currentPower * 1.5); 
            
                // Охлаждение стало мощнее, чтобы справляться с новым нагревом
                if (IsPumpOn) targetTemp -= 15;
                if (IsFanOn) targetTemp -= 25;
            
                // Если авария и выключили систему
                if (_currentPower == 0) targetTemp = 20.0;

                // Прогрессивное изменение температуры (чем больше разница, тем быстрее скорость)
                double tempDifference = Math.Abs(targetTemp - _currentOilTemp);
                double dynamicSmoothingFactor = 0.02 + (tempDifference * 0.004);
            
                if (dynamicSmoothingFactor > 0.25) dynamicSmoothingFactor = 0.25;

                _currentOilTemp += (targetTemp - _currentOilTemp) * dynamicSmoothingFactor;
                _currentOilTemp += (_random.NextDouble() * 0.4 - 0.2);
            }

            AddDataPoint(_currentPower, _currentOilTemp);

            // OnPropertyChanged уведомляет UI, что данные обновились
            OnPropertyChanged(nameof(CurrentPower));
            OilTemperatureText = $"{_currentOilTemp:F1} °C";
            VoltageText = $"{_currentVoltage:F0} В";
            PressureText = $"{_currentPressure:F1} атм";
        }
    }

    [RelayCommand]
    private void ToggleSystem()
    {
        _systemEnabled = !_systemEnabled;
        if (_systemEnabled)
        {
            Repair();
            SystemStatusText = "Статус: НОРМА";
        }
        else
        {
            _currentPower = 0;
            OperatorPower = 0;
            _currentVoltage = 0;
            IsPumpOn = false;
            IsFanOn = false;
            SystemStatusText = "Статус: ОТКЛЮЧЕНО ОПЕРАТОРОМ";
            CoolingStatusText = "Охлаждение: Выкл";
        }
    }

    [RelayCommand]
    private void ShortCircuit()
    {
        _currentOilTemp += 40;
        TriggerEmergency("АВАРИЯ: Короткое замыкание! Защита сработала.");
    }

    [RelayCommand]
    private void Leak()
    {
        if (_isEmergency) return;
        _isLeak = true;
        SystemStatusText = "ВНИМАНИЕ: Протечка системы охлаждения!";
    }

    [RelayCommand]
    private void FanFailure()
    {
        if (_isEmergency) return;
        _isFanFailed = true;
        SystemStatusText = "ВНИМАНИЕ: Выход из строя вентиляторов!";
    }

    [RelayCommand]
    private void Repair()
    {
        _isEmergency = false;
        _isLeak = false;
        _isFanFailed = false;

        OperatorPower = 25; // Сброс ползунка к нормальным значениям
        _currentOilTemp = 45;
        _currentVoltage = 220;
        _currentPressure = 4.0;

        SystemStatusText = "Статус: НОРМА";
        CheckProtectionsAndCooling();
    }

    public void AddDataPoint(double power, double oilTemp)
    {
        _chart1Values.Add(new ObservableValue(power));
        if (_chart1Values.Count > 50) _chart1Values.RemoveAt(0);

        _chart2Values.Add(new ObservableValue(oilTemp));
        if (_chart2Values.Count > 50) _chart2Values.RemoveAt(0);
    }
}