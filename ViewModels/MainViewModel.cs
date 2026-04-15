using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinorProject.Models;
using MinorProject.Services;

namespace MinorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public TransformerModel T1 { get; } = new();
    public TransformerModel T2 { get; } = new();
    public ObservableCollection<ConsumerModel> Consumers { get; } = new();

    private readonly TemplatePageTViewModel _infoT1;
    private readonly TemplatePageTViewModel _infoT2;
    private readonly LoadBalancerService _balancer = new();

    [ObservableProperty] private double _totalDemand;
    [ObservableProperty] private double _effectiveDemand;
    [ObservableProperty] private bool _isAutoBalancing = true;

    [ObservableProperty] private double _balancerMaxT1 = 80;
    [ObservableProperty] private double _balancerMaxT2 = 80;
    [ObservableProperty] private double _t2Threshold = 60;
    [ObservableProperty] private double _maxTotalDemand = 160;
    [ObservableProperty] private DistributionStrategy _distributionStrategy = DistributionStrategy.T1First;

    [ObservableProperty] private bool _isManualTransferEnabled;
    [ObservableProperty] private PowerTransferDirection _transferDirection = PowerTransferDirection.T1ToT2;
    [ObservableProperty] private double _manualTransferAmount = 5;

    [ObservableProperty] private string _t1AllocatedText = "0 МВА";
    [ObservableProperty] private string _t2AllocatedText = "0 МВА";
    [ObservableProperty] private string _distributionText = string.Empty;
    [ObservableProperty] private string _failoverWarning = string.Empty;
    [ObservableProperty] private string _demandWarning = string.Empty;
    [ObservableProperty] private string _appliedTransferText = "Ручной переброс отключен";
    [ObservableProperty] private string _transferWarning = string.Empty;

    [ObservableProperty] private string _totalPower = "—";
    [ObservableProperty] private string _avgTemperature = "—";

    [ObservableProperty] private IBrush _t1StatusBrush = Brushes.Green;
    [ObservableProperty] private IBrush _t2StatusBrush = Brushes.Green;
    [ObservableProperty] private string _t1StatusIcon = "●";
    [ObservableProperty] private string _t2StatusIcon = "●";

    [ObservableProperty] private string _editBalancerMaxT1 = "80";
    [ObservableProperty] private string _editBalancerMaxT2 = "80";
    [ObservableProperty] private string _editT2Threshold = "60";
    [ObservableProperty] private string _editMaxTotalDemand = "160";
    [ObservableProperty] private string _balancerSettingsError = string.Empty;
    [ObservableProperty] private string _balancerSettingsSuccess = string.Empty;

    public string StrategyText => DistributionStrategy switch
    {
        DistributionStrategy.T1First => "Т1 -> Т2 (приоритет Т1)",
        DistributionStrategy.T2First => "Т2 -> Т1 (приоритет Т2)",
        DistributionStrategy.Proportional => "Пропорционально",
        _ => "—"
    };

    public string TransferDirectionText => TransferDirection switch
    {
        PowerTransferDirection.T1ToT2 => "Т1 -> Т2",
        PowerTransferDirection.T2ToT1 => "Т2 -> Т1",
        _ => "—"
    };

    public MainViewModel(TemplatePageTViewModel infoT1, TemplatePageTViewModel infoT2)
    {
        _infoT1 = infoT1;
        _infoT2 = infoT2;

        InitializeConsumers();
        RecalculateDemand();
        SimulateLiveUpdates();
    }

    partial void OnDistributionStrategyChanged(DistributionStrategy value) => OnPropertyChanged(nameof(StrategyText));

    partial void OnTransferDirectionChanged(PowerTransferDirection value) => OnPropertyChanged(nameof(TransferDirectionText));

    partial void OnMaxTotalDemandChanged(double value) => RecalculateDemand();

    partial void OnIsManualTransferEnabledChanged(bool value)
    {
        if (!value)
        {
            AppliedTransferText = "Ручной переброс отключен";
            TransferWarning = string.Empty;
        }
    }

    private void InitializeConsumers()
    {
        double[] initialDemand = { 12, 10, 14, 8, 16, 10 };

        for (int index = 0; index < initialDemand.Length; index++)
        {
            var consumer = new ConsumerModel($"Потребитель {index + 1}", initialDemand[index]);
            consumer.PropertyChanged += OnConsumerPropertyChanged;
            Consumers.Add(consumer);
        }
    }

    private void OnConsumerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RecalculateDemand();
    }

    private void RecalculateDemand()
    {
        TotalDemand = Consumers.Sum(consumer => consumer.EffectiveDemand);
        EffectiveDemand = TotalDemand > MaxTotalDemand ? MaxTotalDemand : TotalDemand;

        DemandWarning = TotalDemand > MaxTotalDemand
            ? $"Запрос потребителей ограничен лимитом подстанции: {MaxTotalDemand:F1} МВА."
            : string.Empty;
    }

    private async void SimulateLiveUpdates()
    {
        while (true)
        {
            await Task.Delay(500);

            Dispatcher.UIThread.Post(() =>
            {
                if (IsAutoBalancing)
                {
                    var distribution = _balancer.Distribute(
                        EffectiveDemand,
                        _infoT1.IsAvailable, _infoT2.IsAvailable,
                        _infoT1.IsManualMode, _infoT2.IsManualMode,
                        _infoT1.OperatorPower, _infoT2.OperatorPower,
                        BalancerMaxT1, BalancerMaxT2,
                        T2Threshold,
                        DistributionStrategy,
                        IsManualTransferEnabled,
                        TransferDirection,
                        ManualTransferAmount
                    );

                    if (!_infoT1.IsManualMode)
                        _infoT1.OperatorPower = distribution.t1Power;
                    if (!_infoT2.IsManualMode)
                        _infoT2.OperatorPower = distribution.t2Power;

                    _infoT1.SetAllocatedPower(distribution.t1Power);
                    _infoT2.SetAllocatedPower(distribution.t2Power);

                    T1AllocatedText = $"{distribution.t1Power:F1} МВА";
                    T2AllocatedText = $"{distribution.t2Power:F1} МВА";
                    DistributionText = $"Запрос: {EffectiveDemand:F1} МВА | Т1: {distribution.t1Power:F1} | Т2: {distribution.t2Power:F1}";

                    if (!IsManualTransferEnabled)
                    {
                        AppliedTransferText = "Ручной переброс отключен";
                        TransferWarning = string.Empty;
                    }
                    else
                    {
                        AppliedTransferText = distribution.appliedTransfer > 0
                            ? $"Переброшено: {distribution.appliedTransfer:F1} МВА ({TransferDirectionText})"
                            : "Переброс не выполнен";
                        TransferWarning = distribution.warning;
                    }

                    bool t1Down = !_infoT1.IsAvailable;
                    bool t2Down = !_infoT2.IsAvailable;
                    if (t1Down && !t2Down)
                        FailoverWarning = "⚠ Т1 недоступен — вся нагрузка на Т2";
                    else if (t2Down && !t1Down)
                        FailoverWarning = "⚠ Т2 недоступен — вся нагрузка на Т1";
                    else if (t1Down && t2Down)
                        FailoverWarning = "⚠ Оба трансформатора недоступны!";
                    else
                        FailoverWarning = string.Empty;
                }

                T1.Power = $"{_infoT1.CurrentPower:F1} МВА";
                T1.OilTemperature = $"{_infoT1.CurrentOilTemp:F1} °C";
                T1.Voltage = $"{_infoT1.CurrentVoltage:F0} В";
                T1.Pressure = $"{_infoT1.CurrentPressure:F1} атм";

                if (_infoT1.Twin.IsEmergency)
                    T1StatusBrush = Brushes.Red;
                else if (_infoT1.Twin.SystemState == SystemState.Disabled)
                    T1StatusBrush = Brushes.Gray;
                else
                    T1StatusBrush = Brushes.Green;

                T1StatusIcon = _infoT1.Twin.IsEmergency ? "⚠" : "●";

                T2.Power = $"{_infoT2.CurrentPower:F1} МВА";
                T2.OilTemperature = $"{_infoT2.CurrentOilTemp:F1} °C";
                T2.Voltage = $"{_infoT2.CurrentVoltage:F0} В";
                T2.Pressure = $"{_infoT2.CurrentPressure:F1} атм";

                if (_infoT2.Twin.IsEmergency)
                    T2StatusBrush = Brushes.Red;
                else if (_infoT2.Twin.SystemState == SystemState.Disabled)
                    T2StatusBrush = Brushes.Gray;
                else
                    T2StatusBrush = Brushes.Green;

                T2StatusIcon = _infoT2.Twin.IsEmergency ? "⚠" : "●";

                double p1 = _infoT1.CurrentPower;
                double p2 = _infoT2.CurrentPower;
                double t1 = _infoT1.CurrentOilTemp;
                double t2 = _infoT2.CurrentOilTemp;

                TotalPower = $"{p1 + p2:F1} МВА";
                AvgTemperature = $"{(t1 + t2) / 2:F1} °C";
            });
        }
    }

    [RelayCommand]
    private void SetStrategyT1First() => DistributionStrategy = DistributionStrategy.T1First;

    [RelayCommand]
    private void SetStrategyT2First() => DistributionStrategy = DistributionStrategy.T2First;

    [RelayCommand]
    private void SetStrategyProportional() => DistributionStrategy = DistributionStrategy.Proportional;

    [RelayCommand]
    private void SetTransferDirectionT1ToT2() => TransferDirection = PowerTransferDirection.T1ToT2;

    [RelayCommand]
    private void SetTransferDirectionT2ToT1() => TransferDirection = PowerTransferDirection.T2ToT1;

    [RelayCommand]
    private void ApplyBalancerSettings()
    {
        BalancerSettingsError = string.Empty;
        BalancerSettingsSuccess = string.Empty;

        if (!double.TryParse(EditBalancerMaxT1, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var maxT1) || maxT1 <= 0)
        {
            BalancerSettingsError = "Неверный лимит Т1";
            return;
        }

        if (!double.TryParse(EditBalancerMaxT2, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var maxT2) || maxT2 <= 0)
        {
            BalancerSettingsError = "Неверный лимит Т2";
            return;
        }

        if (!double.TryParse(EditT2Threshold, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var threshold) || threshold < 0)
        {
            BalancerSettingsError = "Неверный порог подключения Т2";
            return;
        }

        if (!double.TryParse(EditMaxTotalDemand, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var maxDemand) || maxDemand <= 0)
        {
            BalancerSettingsError = "Неверный макс. лимит потребления";
            return;
        }

        BalancerMaxT1 = maxT1;
        BalancerMaxT2 = maxT2;
        T2Threshold = threshold;
        MaxTotalDemand = maxDemand;

        BalancerSettingsSuccess = "Настройки балансировщика применены";
        RecalculateDemand();
    }
}
