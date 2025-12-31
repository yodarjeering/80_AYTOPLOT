using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlot.ViewModels
{
    public partial class AxisCalibrationDialogViewModel : ObservableObject
    {
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
