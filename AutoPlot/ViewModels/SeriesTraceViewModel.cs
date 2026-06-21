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
        private readonly Stack<List<Point>> _redoStack = new();

        private List<Point>? _currentSeries;
        public bool IsConfirmed { get; private set; }
        public List<List<Point>> ResultSeries { get; private set; } = new();

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
            ResetTrace();
            IsTracingActive = true;
        }

        [RelayCommand]
        private void ResetTrace()
        {
            if (SeriesCount < 1)
                SeriesCount = 1;

            TracedSeries.Clear();
            _redoStack.Clear();
            CurrentSeriesIndex = 0;
            IsTracingActive = true;
            _currentSeries = null;
            OnPropertyChanged(nameof(CanTrace));
        }

        [RelayCommand]
        private void Undo()
        {
            if (TracedSeries.Count == 0)
                return;

            var latest = TracedSeries[^1];
            TracedSeries.RemoveAt(TracedSeries.Count - 1);
            _redoStack.Push(latest);
            CurrentSeriesIndex = Math.Max(0, CurrentSeriesIndex - 1);
            IsTracingActive = true;
            OnPropertyChanged(nameof(InstructionText));
            OnPropertyChanged(nameof(CanTrace));
        }

        [RelayCommand]
        private void Redo()
        {
            if (_redoStack.Count == 0 || TracedSeries.Count >= SeriesCount)
                return;

            TracedSeries.Add(_redoStack.Pop());
            CurrentSeriesIndex++;

            if (CurrentSeriesIndex >= SeriesCount)
                IsTracingActive = false;

            OnPropertyChanged(nameof(InstructionText));
            OnPropertyChanged(nameof(CanTrace));
        }

        [RelayCommand]
        private void Ok()
        {
            ResultSeries = TracedSeries
                .Select(series => series.ToList())
                .ToList();
            IsConfirmed = true;
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
                _redoStack.Clear();
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
