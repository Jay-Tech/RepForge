using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Controls;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>One weigh-in row in the Body tab list.</summary>
public class BodyMetricItem(BodyMetric row, bool metricUnits)
{
    public BodyMetric Row { get; } = row;

    public string DateText { get; } = row.MeasuredUtc.ToLocalTime().ToString("ddd, MMM d, yyyy");

    public string WeightText { get; } = $"{row.Weight:0.#} {(metricUnits ? "kg" : "lb")}";

    public string BmiText { get; } = BodyViewModel.Bmi(row, metricUnits) is { } bmi
        ? $"BMI {bmi:0.0}  ·  {BodyViewModel.BmiCategory(bmi)}"
        : string.Empty;
}

public partial class BodyViewModel : ViewModelBase
{
    private const string UnitsSettingKey = "body.units";

    private RepForgeDb? _db;
    private bool _loaded;

    public ObservableCollection<BodyMetricItem> Entries { get; } = [];

    /// <summary>0 = imperial (lb/in), 1 = metric (kg/cm).</summary>
    [ObservableProperty]
    private int _unitsIndex;

    [ObservableProperty]
    private decimal? _weight;

    [ObservableProperty]
    private decimal? _height;

    [ObservableProperty]
    private string _currentSummary = string.Empty;

    /// <summary>0 = weight trend, 1 = BMI trend.</summary>
    [ObservableProperty]
    private int _chartModeIndex;

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _chartPoints = [];

    private List<BodyMetric> _rows = [];

    public bool IsMetric => UnitsIndex == 1;

    public BodyViewModel()
    {
        if (!Design.IsDesignMode)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db = await RepForgeDb.GetAsync();
        UnitsIndex = await _db.GetSettingAsync(UnitsSettingKey) == "metric" ? 1 : 0;
        _loaded = true;
        await RefreshAsync();
    }

    partial void OnUnitsIndexChanged(int value)
    {
        if (!_loaded || _db is null)
            return;
        _ = _db.SetSettingAsync(UnitsSettingKey, value == 1 ? "metric" : "imperial");
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_db is null)
            return;

        var rows = await _db.GetBodyMetricsAsync();
        _rows = rows;
        Entries.Clear();
        foreach (var row in rows)
            Entries.Add(new BodyMetricItem(row, IsMetric));
        RebuildChart();

        var latest = rows.FirstOrDefault();
        if (latest is not null)
        {
            // Prefill so the next weigh-in is one number away.
            Weight = (decimal)latest.Weight;
            if (latest.Height > 0)
                Height = (decimal)latest.Height;

            var unit = IsMetric ? "kg" : "lb";
            CurrentSummary = Bmi(latest, IsMetric) is { } bmi
                ? $"Current:  {latest.Weight:0.#} {unit}  ·  BMI {bmi:0.0} ({BmiCategory(bmi)})"
                : $"Current:  {latest.Weight:0.#} {unit}";
        }
        else
        {
            CurrentSummary = string.Empty;
        }
    }

    [RelayCommand]
    private async Task LogAsync()
    {
        if (_db is null || Weight is not { } weight || weight <= 0)
            return;

        await _db.SaveBodyMetricAsync(new BodyMetric
        {
            Weight = (double)weight,
            Height = Height is { } h && h > 0 ? (double)h : 0,
        });
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(BodyMetricItem item)
    {
        if (_db is null)
            return;
        await _db.DeleteBodyMetricAsync(item.Row);
        await RefreshAsync();
    }

    partial void OnChartModeIndexChanged(int value) => RebuildChart();

    private void RebuildChart()
    {
        ChartPoints = ChartModeIndex == 0
            ? _rows.Where(r => r.Weight > 0)
                .Select(r => new ChartPoint(r.MeasuredUtc, r.Weight))
                .ToList()
            : _rows.Select(r => (r.MeasuredUtc, Bmi: Bmi(r, IsMetric)))
                .Where(x => x.Bmi is not null)
                .Select(x => new ChartPoint(x.MeasuredUtc, x.Bmi!.Value))
                .ToList();
    }

    /// <summary>BMI from a weigh-in, or null when height wasn't recorded.</summary>
    public static double? Bmi(BodyMetric m, bool metricUnits)
    {
        if (m.Height <= 0 || m.Weight <= 0)
            return null;
        return metricUnits
            ? m.Weight / Math.Pow(m.Height / 100.0, 2)   // kg, cm
            : 703.0 * m.Weight / (m.Height * m.Height);  // lb, in
    }

    public static string BmiCategory(double bmi) => bmi switch
    {
        < 18.5 => "Underweight",
        < 25 => "Normal",
        < 30 => "Overweight",
        _ => "Obese",
    };
}
