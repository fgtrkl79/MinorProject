using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using MinorProject.Models;

namespace MinorProject.ViewModels;

public partial class TemplatePageTViewModel : ObservableObject
{
    private readonly ObservableCollection<ObservableValue> _chart1Values;
    private readonly ObservableCollection<ObservableValue> _chart2Values;
    [ObservableProperty] private ISeries[] _chart1Series;
    [ObservableProperty] private ISeries[] _chart2Series;

    public Axis[] Chart1YAxes { get; set; }
    public Axis[] Chart2YAxes { get; set; }

    private SimulationConfig _config;

    [ObservableProperty] private SimulationConfig _configProperty = SimulationConfig.GetDefault();

    // --- Цифровой двойник ---
    public DigitalTwinModel Twin { get; }

    private readonly string _configPath;
    private readonly string _historyPath;
    private readonly string _appFolder;
    private readonly JsonSerializerOptions _jsonOptions;

    // --- ОСНОВНЫЕ ПАРАМЕТРЫ ---
    private double _currentPower = 25;
    private double _currentOilTemp = 45;
    private double _currentVoltage = 110;
    private double _currentPressure = 4.0;

    public double CurrentPower => _currentPower;
    public double CurrentOilTemp => _currentOilTemp;
    public double CurrentVoltage => _currentVoltage;
    public double CurrentPressure => _currentPressure;

    [ObservableProperty] private double _operatorPower = 25;

    [ObservableProperty] private bool _isManualMode;

    private double _allocatedPower;
    public double AllocatedPower => _allocatedPower;

    public void SetAllocatedPower(double value)
    {
        _allocatedPower = value;
        OnPropertyChanged(nameof(AllocatedPower));
    }

    public bool IsAvailable => !_isEmergency && _systemEnabled;
    public double MaxPower => _config.MaxPower;

    partial void OnOperatorPowerChanged(double value)
    {
        if (_isEmergency || !_systemEnabled) return;
        _currentPower = value;
        CheckProtectionsAndCooling();
    }

    // --- ФЛАГИ СОСТОЯНИЯ ---
    private bool _isEmergency;
    private bool _systemEnabled = true;
    private bool _isLeak;
    private bool _isFanFailed;
    private bool _isPumpDisabledByLeak;
    private DateTime _leakStartTime;
    private const double LeakPumpWorkTime = 10.0;
    private const double LeakTemperatureMultiplier = 1.5;

    [ObservableProperty] private bool _isPumpOn;
    [ObservableProperty] private bool _isFanOn;

    // --- ТЕКСТЫ ДЛЯ UI ---
    [ObservableProperty] private string _oilTemperatureText = string.Empty;
    [ObservableProperty] private string _voltageText = string.Empty;
    [ObservableProperty] private string _pressureText = string.Empty;
    [ObservableProperty] private string _systemStatusText = "Статус: НОРМА";
    [ObservableProperty] private string _coolingStatusText = "Охлаждение: Отключено";
    [ObservableProperty] private string _chartTitle1 = string.Empty;
    [ObservableProperty] private string _chartTitle2 = string.Empty;

    // --- ИСТОРИЯ ДАННЫХ ---
    [ObservableProperty] private ObservableCollection<DataPoint> _historyData = new();
    private const int MaxHistoryDisplayCount = 100;
    private const int MaxChartPoints = 50;
    private int _historySaveCounter;
    private const int SaveHistoryEveryNPoints = 2;

    // --- РЕДАКТИРУЕМЫЕ НАСТРОЙКИ (для TextBox) ---
    [ObservableProperty] private string _editUpdateIntervalMs = "500";
    [ObservableProperty] private string _editMaxPower = "80";
    [ObservableProperty] private string _editPowerEmergencyThreshold = "63";
    [ObservableProperty] private string _editTemperatureEmergencyThreshold = "100";
    [ObservableProperty] private string _editPumpOnTemperature = "80";
    [ObservableProperty] private string _editPumpOffTemperature = "70";
    [ObservableProperty] private string _editFanOnTemperature = "90";
    [ObservableProperty] private string _editFanOffTemperature = "80";
    [ObservableProperty] private string _editHeatingCoefficient = "1.5";
    [ObservableProperty] private string _editPumpCoolingEffect = "15";
    [ObservableProperty] private string _editFanCoolingEffect = "25";
    [ObservableProperty] private string _editSettingsError = string.Empty;
    [ObservableProperty] private string _editSettingsSuccess = string.Empty;

    private readonly Random _random = new();

    public TemplatePageTViewModel() : this(new DigitalTwinModel("T1", "Трансформатор Т1"))
    {
    }

    public TemplatePageTViewModel(DigitalTwinModel twin)
    {
        Twin = twin;

        var projectDir = GetProjectDirectory();
        _appFolder = Path.Combine(projectDir, "Data");
        Directory.CreateDirectory(_appFolder);

        _configPath = Path.Combine(_appFolder, $"config_{twin.Id.ToLower()}.json");
        _historyPath = Path.Combine(_appFolder, $"history_{twin.Id.ToLower()}.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _config = SimulationConfig.GetDefault();

        ChartTitle1 = $"График {twin.Name}: Мощность (МВА)";
        ChartTitle2 = $"График {twin.Name}: Температура масла (°C)";

        _chart1Values = new ObservableCollection<ObservableValue>();
        _chart2Values = new ObservableCollection<ObservableValue>();

        for (int i = 0; i < MaxChartPoints; i++)
        {
            _chart1Values.Add(new ObservableValue(0));
            _chart2Values.Add(new ObservableValue(0));
        }

        _chart1Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _chart1Values,
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.5
            }
        };

        _chart2Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _chart2Values,
                Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.5
            }
        };

        Chart1YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = _config.MaxPower + 4,
                MinStep = 10,
            }
        };

        Chart2YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = _config.TemperatureEmergencyThreshold + 50,
                MinStep = 25
            }
        };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _config = await LoadConfigAsync();
        ConfigProperty = _config;
        SyncConfigToEditFields();
        Chart1YAxes[0].MaxLimit = _config.MaxPower + 4;
        Chart2YAxes[0].MaxLimit = _config.TemperatureEmergencyThreshold + 50;

        StartAutoUpdate();
    }

    private void SyncConfigToEditFields()
    {
        EditUpdateIntervalMs = _config.UpdateIntervalMs.ToString("F0", CultureInfo.InvariantCulture);
        EditMaxPower = _config.MaxPower.ToString("F0", CultureInfo.InvariantCulture);
        EditPowerEmergencyThreshold = _config.PowerEmergencyThreshold.ToString("F0", CultureInfo.InvariantCulture);
        EditTemperatureEmergencyThreshold = _config.TemperatureEmergencyThreshold.ToString("F0", CultureInfo.InvariantCulture);
        EditPumpOnTemperature = _config.PumpOnTemperature.ToString("F0", CultureInfo.InvariantCulture);
        EditPumpOffTemperature = _config.PumpOffTemperature.ToString("F0", CultureInfo.InvariantCulture);
        EditFanOnTemperature = _config.FanOnTemperature.ToString("F0", CultureInfo.InvariantCulture);
        EditFanOffTemperature = _config.FanOffTemperature.ToString("F0", CultureInfo.InvariantCulture);
        EditHeatingCoefficient = _config.HeatingCoefficient.ToString("F2", CultureInfo.InvariantCulture);
        EditPumpCoolingEffect = _config.PumpCoolingEffect.ToString("F0", CultureInfo.InvariantCulture);
        EditFanCoolingEffect = _config.FanCoolingEffect.ToString("F0", CultureInfo.InvariantCulture);
    }

    private void CheckProtectionsAndCooling()
    {
        if (_isEmergency || !_systemEnabled) return;

        bool needsPump = IsPumpOn;
        bool needsFan = IsFanOn;

        if (_currentPower > _config.PowerEmergencyThreshold)
        {
            TriggerEmergency($"АВАРИЯ: Превышение мощности (>{_config.PowerEmergencyThreshold} МВА). Отключение трансформатора!");
            return;
        }

        if (_currentOilTemp > _config.TemperatureEmergencyThreshold)
        {
            TriggerEmergency($"АВАРИЯ: Перегрев (>{_config.TemperatureEmergencyThreshold} °C). Отключение трансформатора!");
            return;
        }

        if (_currentOilTemp >= _config.PumpOnTemperature) needsPump = true;
        else if (_currentOilTemp <= _config.PumpOffTemperature) needsPump = false;

        if (_currentOilTemp >= _config.FanOnTemperature) needsFan = true;
        else if (_currentOilTemp <= _config.FanOffTemperature) needsFan = false;

        if (_isLeak)
        {
            var leakDuration = (DateTime.Now - _leakStartTime).TotalSeconds;
            if (leakDuration > LeakPumpWorkTime)
            {
                _isPumpDisabledByLeak = true;
                needsPump = false;
            }
        }

        if (_isPumpDisabledByLeak) needsPump = false;
        if (_isFanFailed) needsFan = false;

        IsPumpOn = needsPump;
        IsFanOn = needsFan;

        if (IsPumpOn && IsFanOn) CoolingStatusText = "Охлаждение: Насосы + Вентиляторы";
        else if (IsPumpOn) CoolingStatusText = "Охлаждение: Только насосы";
        else if (_isLeak && !_isPumpDisabledByLeak) CoolingStatusText = "Охлаждение: Насос (Протечка!)";
        else if (_isPumpDisabledByLeak) CoolingStatusText = "Охлаждение: Отключено (Протечка!)";
        else CoolingStatusText = "Охлаждение: Естественное (Выкл)";

        SyncTwin();
    }

    private void TriggerEmergency(string reason)
    {
        _isEmergency = true;
        _currentPower = 0;
        OperatorPower = 0;
        _currentVoltage = 0;
        IsPumpOn = false;
        IsFanOn = false;
        SystemStatusText = $"СТАТУС: {reason}";
        CoolingStatusText = "Охлаждение: АВАРИЯ";
        SyncTwin();
    }

    private void SyncTwin()
    {
        Twin.Power = _currentPower;
        Twin.OilTemperature = _currentOilTemp;
        Twin.Voltage = _currentVoltage;
        Twin.Pressure = _currentPressure;
        Twin.StatusText = SystemStatusText;
        Twin.CoolingText = CoolingStatusText;
        Twin.IsEmergency = _isEmergency;
        Twin.IsPumpOn = IsPumpOn;
        Twin.IsFanOn = IsFanOn;
        Twin.SystemState = GetCurrentSystemState();
        Twin.CoolingState = GetCurrentCoolingState();
    }

    private async void StartAutoUpdate()
    {
        while (true)
        {
            await Task.Delay((int)_config.UpdateIntervalMs);

            if (_systemEnabled)
            {
                if (!_isEmergency)
                {
                    _currentPower = OperatorPower + (_random.NextDouble() * _config.NoiseAmplitude * 2 - _config.NoiseAmplitude);
                    if (_currentPower < 0) _currentPower = 0;

                    _currentVoltage += (_random.NextDouble() * 2 - 1);
                    if (_currentVoltage < _config.VoltageMin) _currentVoltage = _config.VoltageMin;
                    if (_currentVoltage > _config.VoltageMax) _currentVoltage = _config.VoltageMax;

                    if (!_isLeak) _currentPressure = _config.NormalPressure + (_random.NextDouble() * _config.PressureNoiseAmplitude * 2 - _config.PressureNoiseAmplitude);
                    else _currentPressure = 0.0;

                    CheckProtectionsAndCooling();
                }

                double targetTemp = _config.BaseTemperature + (_currentPower * _config.HeatingCoefficient);

                if (IsPumpOn) targetTemp -= _config.PumpCoolingEffect;
                if (IsFanOn) targetTemp -= _config.FanCoolingEffect;

                if (_isLeak && _isPumpDisabledByLeak)
                {
                    targetTemp += (_currentPower * _config.HeatingCoefficient) * (LeakTemperatureMultiplier - 1);
                }

                if (_currentPower == 0) targetTemp = _config.IdleTemperature;

                double tempDifference = Math.Abs(targetTemp - _currentOilTemp);
                double dynamicSmoothingFactor = 0.02 + (tempDifference * 0.004);

                if (_isLeak && _isPumpDisabledByLeak)
                {
                    dynamicSmoothingFactor *= 1.3;
                }

                if (dynamicSmoothingFactor > 0.25) dynamicSmoothingFactor = 0.25;

                _currentOilTemp += (targetTemp - _currentOilTemp) * dynamicSmoothingFactor;
                _currentOilTemp += (_random.NextDouble() * _config.NoiseAmplitude - _config.NoiseAmplitude / 2);
            }

            AddDataPoint(_currentPower, _currentOilTemp);

            OnPropertyChanged(nameof(CurrentPower));
            OilTemperatureText = $"{_currentOilTemp:F1} °C";
            VoltageText = $"{_currentVoltage:F1} кВ";
            PressureText = $"{_currentPressure:F1} атм";

            SyncTwin();
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
        SyncTwin();
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
        _leakStartTime = DateTime.Now;
        _isPumpDisabledByLeak = false;
        SystemStatusText = "ВНИМАНИЕ: Протечка системы охлаждения!";
        SyncTwin();
    }

    [RelayCommand]
    private void FanFailure()
    {
        if (_isEmergency) return;
        _isFanFailed = true;
        SystemStatusText = "ВНИМАНИЕ: Выход из строя вентиляторов!";
        SyncTwin();
    }

    [RelayCommand]
    private void Repair()
    {
        _isEmergency = false;
        _isLeak = false;
        _isFanFailed = false;
        _isPumpDisabledByLeak = false;

        OperatorPower = 25;
        _currentOilTemp = 45;
        _currentVoltage = _config.VoltageMax;
        _currentPressure = 4.0;

        SystemStatusText = "Статус: НОРМА";
        CheckProtectionsAndCooling();
        SyncTwin();
    }

    private SystemState GetCurrentSystemState()
    {
        if (_isEmergency) return SystemState.Emergency;
        if (!_systemEnabled) return SystemState.Disabled;
        return SystemState.Normal;
    }

    private CoolingState GetCurrentCoolingState()
    {
        if (IsPumpOn && IsFanOn) return CoolingState.PumpAndFan;
        if (IsPumpOn) return CoolingState.PumpOnly;
        return CoolingState.Natural;
    }

    public void AddDataPoint(double power, double oilTemp)
    {
        _chart1Values.Add(new ObservableValue(power));
        if (_chart1Values.Count > MaxChartPoints) _chart1Values.RemoveAt(0);

        _chart2Values.Add(new ObservableValue(oilTemp));
        if (_chart2Values.Count > MaxChartPoints) _chart2Values.RemoveAt(0);

        _historySaveCounter++;
        if (_historySaveCounter >= SaveHistoryEveryNPoints)
        {
            _historySaveCounter = 0;
            var dataPoint = new DataPoint(
                _currentPower,
                _currentOilTemp,
                _currentVoltage,
                _currentPressure,
                GetCurrentSystemState(),
                GetCurrentCoolingState()
            );

            HistoryData.Add(dataPoint);
            if (HistoryData.Count > MaxHistoryDisplayCount)
            {
                HistoryData.RemoveAt(0);
            }

            _ = AppendDataPointAsync(dataPoint);
        }
    }

    [RelayCommand]
    private async Task ExportHistory()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                var fallbackPath = Path.Combine(_appFolder, $"export_{Twin.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                var allHistory = await LoadHistoryAsync();
                await ExportToCsvAsync(fallbackPath, allHistory);
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Экспорт истории {Twin.Name}",
                SuggestedFileName = $"export_{Twin.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV файл") { Patterns = new[] { "*.csv" } }
                }
            });

            if (file != null)
            {
                var allHistory = await LoadHistoryAsync();
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Power,OilTemperature,Voltage,Pressure,SystemState,CoolingState");

                foreach (var point in allHistory)
                {
                    var line = string.Format(CultureInfo.InvariantCulture,
                        "{0:O},{1:F2},{2:F1},{3:F1},{4:F1},{5},{6}",
                        point.Timestamp, point.Power, point.OilTemperature,
                        point.Voltage, point.Pressure, point.SystemState, point.CoolingState);
                    sb.AppendLine(line);
                }

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(sb.ToString());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Export error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveConfig()
    {
        EditSettingsError = string.Empty;
        EditSettingsSuccess = string.Empty;

        try
        {
            if (!double.TryParse(EditUpdateIntervalMs, NumberStyles.Float, CultureInfo.InvariantCulture, out var updateInterval) || updateInterval < 50)
            {
                EditSettingsError = "Неверный интервал обновления (мин. 50 мс)";
                return;
            }
            if (!double.TryParse(EditMaxPower, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxPower) || maxPower <= 0)
            {
                EditSettingsError = "Неверная максимальная мощность";
                return;
            }
            if (!double.TryParse(EditPowerEmergencyThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var powerThreshold) || powerThreshold <= 0)
            {
                EditSettingsError = "Неверный порог мощности";
                return;
            }
            if (!double.TryParse(EditTemperatureEmergencyThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var tempThreshold) || tempThreshold <= 0)
            {
                EditSettingsError = "Неверный порог температуры";
                return;
            }
            if (!double.TryParse(EditPumpOnTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var pumpOn))
            {
                EditSettingsError = "Неверная температура включения насоса";
                return;
            }
            if (!double.TryParse(EditPumpOffTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var pumpOff))
            {
                EditSettingsError = "Неверная температура выключения насоса";
                return;
            }
            if (!double.TryParse(EditFanOnTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var fanOn))
            {
                EditSettingsError = "Неверная температура включения вентилятора";
                return;
            }
            if (!double.TryParse(EditFanOffTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var fanOff))
            {
                EditSettingsError = "Неверная температура выключения вентилятора";
                return;
            }
            if (!double.TryParse(EditHeatingCoefficient, NumberStyles.Float, CultureInfo.InvariantCulture, out var heating) || heating < 0)
            {
                EditSettingsError = "Неверный коэффициент нагрева";
                return;
            }
            if (!double.TryParse(EditPumpCoolingEffect, NumberStyles.Float, CultureInfo.InvariantCulture, out var pumpEffect))
            {
                EditSettingsError = "Неверный эффект насоса";
                return;
            }
            if (!double.TryParse(EditFanCoolingEffect, NumberStyles.Float, CultureInfo.InvariantCulture, out var fanEffect))
            {
                EditSettingsError = "Неверный эффект вентилятора";
                return;
            }

            if (pumpOn <= pumpOff)
            {
                EditSettingsError = "Темп. включения насоса должна быть выше темп. выключения";
                return;
            }
            if (fanOn <= fanOff)
            {
                EditSettingsError = "Темп. включения вентилятора должна быть выше темп. выключения";
                return;
            }
            if (powerThreshold > maxPower)
            {
                EditSettingsError = "Порог мощности не может превышать макс. мощность";
                return;
            }
            if (tempThreshold <= fanOn)
            {
                EditSettingsError = "Порог температуры должен быть выше темп. включения вентилятора";
                return;
            }

            _config.UpdateIntervalMs = updateInterval;
            _config.MaxPower = maxPower;
            _config.PowerEmergencyThreshold = powerThreshold;
            _config.TemperatureEmergencyThreshold = tempThreshold;
            _config.PumpOnTemperature = pumpOn;
            _config.PumpOffTemperature = pumpOff;
            _config.FanOnTemperature = fanOn;
            _config.FanOffTemperature = fanOff;
            _config.HeatingCoefficient = heating;
            _config.PumpCoolingEffect = pumpEffect;
            _config.FanCoolingEffect = fanEffect;

            ConfigProperty = _config;
            Chart1YAxes[0].MaxLimit = _config.MaxPower + 4;
            Chart2YAxes[0].MaxLimit = _config.TemperatureEmergencyThreshold + 50;

            await SaveConfigAsync(_config);
            EditSettingsSuccess = "Конфигурация сохранена";
        }
        catch (Exception ex)
        {
            EditSettingsError = $"Ошибка: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetConfig()
    {
        _config = SimulationConfig.GetDefault();
        ConfigProperty = _config;
        SyncConfigToEditFields();
        Chart1YAxes[0].MaxLimit = _config.MaxPower + 4;
        Chart2YAxes[0].MaxLimit = _config.TemperatureEmergencyThreshold + 50;
        await SaveConfigAsync(_config);
        EditSettingsError = string.Empty;
        EditSettingsSuccess = "Сброшено к значениям по умолчанию";
    }

    public SimulationConfig Config => _config;

    // --- Файловые операции ---

    private async Task<SimulationConfig> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                var defaultConfig = SimulationConfig.GetDefault();
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<SimulationConfig>(json, _jsonOptions);
            return config ?? SimulationConfig.GetDefault();
        }
        catch
        {
            return SimulationConfig.GetDefault();
        }
    }

    private async Task SaveConfigAsync(SimulationConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private async Task<List<DataPoint>> LoadHistoryAsync()
    {
        try
        {
            if (!File.Exists(_historyPath))
                return new List<DataPoint>();

            var json = await File.ReadAllTextAsync(_historyPath);
            var wrapper = JsonSerializer.Deserialize<HistoryWrapper>(json, _jsonOptions);
            return wrapper?.DataPoints ?? new List<DataPoint>();
        }
        catch
        {
            return new List<DataPoint>();
        }
    }

    private async Task AppendDataPointAsync(DataPoint dataPoint)
    {
        try
        {
            var history = await LoadHistoryAsync();
            history.Add(dataPoint);

            if (history.Count > 10000)
            {
                var olderFile = Path.Combine(_appFolder, $"history_{Twin.Id.ToLower()}_archive_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                var archive = history.GetRange(0, history.Count - 5000);
                var archiveWrapper = new HistoryWrapper { TransformerId = Twin.Id, DataPoints = archive };
                var archiveJson = JsonSerializer.Serialize(archiveWrapper, _jsonOptions);
                await File.WriteAllTextAsync(olderFile, archiveJson);

                history.RemoveRange(0, history.Count - 5000);
            }

            var wrapper = new HistoryWrapper { TransformerId = Twin.Id, DataPoints = history };
            var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await File.WriteAllTextAsync(_historyPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error appending data point: {ex.Message}");
        }
    }

    private async Task ExportToCsvAsync(string filePath, List<DataPoint> dataPoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Power,OilTemperature,Voltage,Pressure,SystemState,CoolingState");

        foreach (var point in dataPoints)
        {
            var line = string.Format(CultureInfo.InvariantCulture,
                "{0:O},{1:F2},{2:F1},{3:F1},{4:F1},{5},{6}",
                point.Timestamp, point.Power, point.OilTemperature,
                point.Voltage, point.Pressure, point.SystemState, point.CoolingState);
            sb.AppendLine(line);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    private static string GetProjectDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MinorProject.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? baseDir;
    }
}

public class HistoryWrapper
{
    public string TransformerId { get; set; } = "T1";
    public List<DataPoint> DataPoints { get; set; } = new();
}
