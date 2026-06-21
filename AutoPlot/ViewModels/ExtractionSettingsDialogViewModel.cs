using AutoPlot.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlot.ViewModels
{
    public partial class ExtractionSettingsDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private int movingAverageWindow = 5;

        [ObservableProperty]
        private int curveThreshold = 180;

        [ObservableProperty]
        private int noiseMaskThreshold = 180;

        [ObservableProperty]
        private int traceSearchBandWidth = 15;

        [ObservableProperty]
        private int minCurveLength = 20;

        [ObservableProperty]
        private int outlierRemovalThreshold = 0;

        public ExtractionSettingsDialogViewModel()
        {
        }

        public ExtractionSettingsDialogViewModel(ExtractionSettings settings)
        {
            MovingAverageWindow = settings.MovingAverageWindow;
            CurveThreshold = settings.CurveThreshold;
            NoiseMaskThreshold = settings.NoiseMaskThreshold;
            TraceSearchBandWidth = settings.TraceSearchBandWidth;
            MinCurveLength = settings.MinCurveLength;
            OutlierRemovalThreshold = settings.OutlierRemovalThreshold;
        }

        public ExtractionSettings ToSettings()
        {
            return new ExtractionSettings
            {
                MovingAverageWindow = MovingAverageWindow,
                CurveThreshold = CurveThreshold,
                NoiseMaskThreshold = NoiseMaskThreshold,
                TraceSearchBandWidth = TraceSearchBandWidth,
                MinCurveLength = MinCurveLength,
                OutlierRemovalThreshold = OutlierRemovalThreshold
            };
        }

        public bool TryValidate(out string errorMessage)
        {
            if (MovingAverageWindow < 1)
            {
                errorMessage = "MovingAverageWindow must be 1 or greater.";
                return false;
            }

            if (CurveThreshold < 0 || CurveThreshold > 255)
            {
                errorMessage = "CurveThreshold must be between 0 and 255.";
                return false;
            }

            if (NoiseMaskThreshold < 0 || NoiseMaskThreshold > 255)
            {
                errorMessage = "NoiseMaskThreshold must be between 0 and 255.";
                return false;
            }

            if (TraceSearchBandWidth < 1)
            {
                errorMessage = "TraceSearchBandWidth must be 1 or greater.";
                return false;
            }

            if (MinCurveLength < 1)
            {
                errorMessage = "MinCurveLength must be 1 or greater.";
                return false;
            }

            if (OutlierRemovalThreshold < 0)
            {
                errorMessage = "OutlierRemovalThreshold must be 0 or greater.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
