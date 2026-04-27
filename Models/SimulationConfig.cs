using System;
using System.Text.Json.Serialization;

namespace MinorProject.Models;

public class SimulationConfig
{
    [JsonPropertyName("updateIntervalMs")]
    public double UpdateIntervalMs { get; set; } = 500;

    [JsonPropertyName("maxPower")]
    public double MaxPower { get; set; } = 80;

    [JsonPropertyName("powerEmergencyThreshold")]
    public double PowerEmergencyThreshold { get; set; } = 63;

    [JsonPropertyName("temperatureEmergencyThreshold")]
    public double TemperatureEmergencyThreshold { get; set; } = 100;

    [JsonPropertyName("pumpOnTemperature")]
    public double PumpOnTemperature { get; set; } = 80;

    [JsonPropertyName("pumpOffTemperature")]
    public double PumpOffTemperature { get; set; } = 70;

    [JsonPropertyName("fanOnTemperature")]
    public double FanOnTemperature { get; set; } = 90;

    [JsonPropertyName("fanOffTemperature")]
    public double FanOffTemperature { get; set; } = 80;

    [JsonPropertyName("heatingCoefficient")]
    public double HeatingCoefficient { get; set; } = 1.5;

    [JsonPropertyName("coolingCoefficient")]
    public double CoolingCoefficient { get; set; } = 0.8;

    [JsonPropertyName("noiseAmplitude")]
    public double NoiseAmplitude { get; set; } = 0.4;

    [JsonPropertyName("baseTemperature")]
    public double BaseTemperature { get; set; } = 30.0;

    [JsonPropertyName("pumpCoolingEffect")]
    public double PumpCoolingEffect { get; set; } = 15;

    [JsonPropertyName("fanCoolingEffect")]
    public double FanCoolingEffect { get; set; } = 25;

    [JsonPropertyName("idleTemperature")]
    public double IdleTemperature { get; set; } = 20.0;

    [JsonPropertyName("voltageMin")]
    public double VoltageMin { get; set; } = 100;

    [JsonPropertyName("voltageMax")]
    public double VoltageMax { get; set; } = 110;

    [JsonPropertyName("normalPressure")]
    public double NormalPressure { get; set; } = 4.0;

    [JsonPropertyName("pressureNoiseAmplitude")]
    public double PressureNoiseAmplitude { get; set; } = 0.1;

    public static SimulationConfig GetDefault()
    {
        return new SimulationConfig();
    }
}
