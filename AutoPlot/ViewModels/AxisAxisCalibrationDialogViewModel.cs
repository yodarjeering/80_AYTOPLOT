using AutoPlot.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlot.ViewModels
{
    public partial class AxisCalibrationDialogViewModel : ObservableObject
    {
        public IReadOnlyList<AppThemeOption> ThemeOptions { get; } =
        [
            new AppThemeOption(AppTheme.Light, "ライト"),
            new AppThemeOption(AppTheme.Dark, "ダーク"),
            new AppThemeOption(AppTheme.ChocoMint, "チョコミント")
        ];

        [ObservableProperty]
        private AppTheme selectedTheme;

        [ObservableProperty]
        private double xMin;

        [ObservableProperty]
        private double xMax;

        [ObservableProperty]
        private bool isXLog;

        [ObservableProperty]
        private double yMin;

        [ObservableProperty]
        private double yMax;

        [ObservableProperty]
        private bool isYLog;
    }
}
