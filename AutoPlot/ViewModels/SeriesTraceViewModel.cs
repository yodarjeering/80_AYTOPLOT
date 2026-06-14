using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AutoPlot.ViewModels
{
    public partial class SeriesTraceViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InstructionText))]
        private int _seriesCount = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InstructionText))]
        private int _currentSeriesIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InstructionText))]
        private bool _isTracingActive;

        [ObservableProperty]
        private BitmapSource _plotImage;

        public ObservableCollection<List<Point>> TracedSeries { get; } = new();

        private List<Point>? _currentSeries;

        public string InstructionText
        {
            get
            {
                if (CurrentSeriesIndex >= SeriesCount)
                    return $"Trace complete ({SeriesCount} / {SeriesCount}).";

                if (!IsTracingActive)
                    return "Enter series count, then click Start Trace.";

                return $"Trace series {CurrentSeriesIndex + 1} / {SeriesCount}";
            }
        }

        public SeriesTraceViewModel(BitmapSource plotImage)
        {
            PlotImage = plotImage;
        }

        [RelayCommand]
        private void StartTrace()
        {
            if (SeriesCount < 1)
                SeriesCount = 1;

            TracedSeries.Clear();
            CurrentSeriesIndex = 0;
            IsTracingActive = true;
            _currentSeries = null;
            OnPropertyChanged(nameof(CanTrace));
        }

        public bool CanTrace => IsTracingActive && CurrentSeriesIndex < SeriesCount;

        public void BeginSeries(Point point)
        {
            if (!CanTrace)
                return;

            _currentSeries = new List<Point> { point };
        }

        public void AddPoint(Point point)
        {
            if (!CanTrace || _currentSeries == null)
                return;

            _currentSeries.Add(point);
        }

        public void CompleteSeries()
        {
            if (_currentSeries == null)
                return;

            if (_currentSeries.Count > 1)
            {
                TracedSeries.Add(_currentSeries);
                CurrentSeriesIndex++;
            }

            _currentSeries = null;

            if (CurrentSeriesIndex >= SeriesCount)
                IsTracingActive = false;

            OnPropertyChanged(nameof(InstructionText));
            OnPropertyChanged(nameof(CanTrace));
        }
    }
}
