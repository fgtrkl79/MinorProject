using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;

namespace MinorProject.Models;

public partial class ConsumerModel : ObservableObject
{
    public ConsumerModel(string name, double requestedPower = 10)
    {
        Name = name;
        _requestedPower = requestedPower;
    }

    [ObservableProperty] private string _name;
    [ObservableProperty] private double _requestedPower;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isBroken;

    public double EffectiveDemand => IsEnabled && !IsBroken ? RequestedPower : 0;

    public string PowerButtonText => IsEnabled ? "Выключить" : "Включить";
    public string RepairButtonText => IsBroken ? "Починить" : "Сломать";
    public string StatusText => IsBroken
        ? "Состояние: сломан"
        : IsEnabled
            ? "Состояние: включен"
            : "Состояние: выключен";
    public IBrush StatusBrush => IsBroken
        ? new SolidColorBrush(Color.Parse("#FF3B30"))
        : IsEnabled
            ? new SolidColorBrush(Color.Parse("#16A34A"))
            : new SolidColorBrush(Color.Parse("#F59E0B"));
    public IBrush StatusBackgroundBrush => IsBroken
        ? new SolidColorBrush(Color.Parse("#FDE7E7"))
        : IsEnabled
            ? new SolidColorBrush(Color.Parse("#DCFCE7"))
            : new SolidColorBrush(Color.Parse("#FEF3C7"));
    public IBrush CardBorderBrush => IsBroken
        ? new SolidColorBrush(Color.Parse("#DC2626"))
        : IsEnabled
            ? new SolidColorBrush(Color.Parse("#16A34A"))
            : new SolidColorBrush(Color.Parse("#D97706"));

    partial void OnRequestedPowerChanged(double value) => OnPropertyChanged(nameof(EffectiveDemand));

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveDemand));
        OnPropertyChanged(nameof(PowerButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusBackgroundBrush));
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    partial void OnIsBrokenChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveDemand));
        OnPropertyChanged(nameof(RepairButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusBackgroundBrush));
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    [RelayCommand]
    private void TogglePower()
    {
        if (IsBroken)
            return;

        IsEnabled = !IsEnabled;
    }

    [RelayCommand]
    private void ToggleBroken()
    {
        IsBroken = !IsBroken;
        if (IsBroken)
            IsEnabled = false;
    }
}
