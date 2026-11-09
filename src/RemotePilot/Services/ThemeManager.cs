using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace RemotePilot.Services;

public enum AppTheme
{
    Light,
    Dark
}

public class ThemeManager : INotifyPropertyChanged
{
    private static ThemeManager? _instance;
    public static ThemeManager Instance => _instance ??= new ThemeManager();

    private AppTheme _currentTheme = AppTheme.Light;
    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ApplyTheme(_currentTheme);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDark));
                ThemeChanged?.Invoke(this, _currentTheme);
            }
        }
    }

    public bool IsDark => _currentTheme == AppTheme.Dark;

    public event EventHandler<AppTheme>? ThemeChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private ThemeManager()
    {
    }

    public void Initialize(AppTheme defaultTheme = AppTheme.Light)
    {
        ApplyTheme(defaultTheme);
    }

    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
    }

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
    }

    private void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var colors = theme == AppTheme.Dark ? GetDarkPalette() : GetLightPalette();

        foreach (var (key, color) in colors)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            app.Resources[key] = brush;
            app.Resources[$"{key}Color"] = color;
        }

        // 兼容常用的通用键
        app.Resources["Background"] = app.Resources["AppBackground"];
        app.Resources["Foreground"] = app.Resources["AppForeground"];
        app.Resources["CardBackground"] = app.Resources["CardBackground"];
        app.Resources["PrimaryColor"] = colors["Primary"];
        app.Resources["PrimaryBrush"] = app.Resources["Primary"];
    }

    private static Dictionary<string, Color> GetLightPalette()
    {
        return new Dictionary<string, Color>
        {
            ["AppBackground"] = (Color)ColorConverter.ConvertFromString("#F8F9FA"),
            ["SurfaceBackground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
            ["CardBackground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
            ["SidebarBackground"] = (Color)ColorConverter.ConvertFromString("#F0F2F5"),
            ["ToolbarBackground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
            ["AppForeground"] = (Color)ColorConverter.ConvertFromString("#1F2328"),
            ["MutedForeground"] = (Color)ColorConverter.ConvertFromString("#656D76"),
            ["BorderBrush"] = (Color)ColorConverter.ConvertFromString("#D0D7DE"),
            ["HoverBackground"] = (Color)ColorConverter.ConvertFromString("#EAECEF"),
            ["SelectedBackground"] = (Color)ColorConverter.ConvertFromString("#DBEAFE"),
            ["Primary"] = (Color)ColorConverter.ConvertFromString("#0969DA"),
            ["PrimaryHover"] = (Color)ColorConverter.ConvertFromString("#0854B0"),
            ["Success"] = (Color)ColorConverter.ConvertFromString("#1A7F37"),
            ["Warning"] = (Color)ColorConverter.ConvertFromString("#9A6700"),
            ["Danger"] = (Color)ColorConverter.ConvertFromString("#CF222E"),
            ["BadgeBackground"] = (Color)ColorConverter.ConvertFromString("#EFF1F3"),
            ["DataGridHeader"] = (Color)ColorConverter.ConvertFromString("#F6F8FA"),
            ["DataGridRowAlternate"] = (Color)ColorConverter.ConvertFromString("#FBFBFC")
        };
    }

    private static Dictionary<string, Color> GetDarkPalette()
    {
        return new Dictionary<string, Color>
        {
            ["AppBackground"] = (Color)ColorConverter.ConvertFromString("#12141C"),
            ["SurfaceBackground"] = (Color)ColorConverter.ConvertFromString("#1F2937"),
            ["CardBackground"] = (Color)ColorConverter.ConvertFromString("#1F2937"),
            ["SidebarBackground"] = (Color)ColorConverter.ConvertFromString("#111827"),
            ["ToolbarBackground"] = (Color)ColorConverter.ConvertFromString("#1F2937"),
            ["AppForeground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
            ["MutedForeground"] = (Color)ColorConverter.ConvertFromString("#9CA3AF"),
            ["BorderBrush"] = (Color)ColorConverter.ConvertFromString("#374151"),
            ["HoverBackground"] = (Color)ColorConverter.ConvertFromString("#374151"),
            ["SelectedBackground"] = (Color)ColorConverter.ConvertFromString("#1D4ED8"),
            ["Primary"] = (Color)ColorConverter.ConvertFromString("#3B82F6"),
            ["PrimaryHover"] = (Color)ColorConverter.ConvertFromString("#60A5FA"),
            ["Success"] = (Color)ColorConverter.ConvertFromString("#22C55E"),
            ["Warning"] = (Color)ColorConverter.ConvertFromString("#EAB308"),
            ["Danger"] = (Color)ColorConverter.ConvertFromString("#EF4444"),
            ["BadgeBackground"] = (Color)ColorConverter.ConvertFromString("#374151"),
            ["DataGridHeader"] = (Color)ColorConverter.ConvertFromString("#1F2937"),
            ["DataGridRowAlternate"] = (Color)ColorConverter.ConvertFromString("#111827")
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
