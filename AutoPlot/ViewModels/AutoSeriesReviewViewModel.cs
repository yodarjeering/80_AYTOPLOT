using AutoPlot.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace AutoPlot.ViewModels
{
    public partial class AutoSeriesReviewViewModel : ObservableObject, IDisposable
    {
        private readonly Mat _plotArea;

        [ObservableProperty]
        private BitmapSource? _previewImage;

        public ObservableCollection<AutoSeriesCandidate> Candidates { get; } = new();
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand ClearAllCommand { get; }
        public bool IsConfirmed { get; private set; }

        public int SelectedCount => Candidates.Count(candidate => candidate.IsSelected);
        public string SelectionSummary => $"採用する系列: {SelectedCount} / {Candidates.Count}";

        public AutoSeriesReviewViewModel(Mat plotArea, List<List<WpfPoint>> detectedSeries)
        {
            _plotArea = plotArea.Clone();

            for (int i = 0; i < detectedSeries.Count; i++)
            {
                var candidate = new AutoSeriesCandidate(i, detectedSeries[i]);
                candidate.PropertyChanged += Candidate_PropertyChanged;
                Candidates.Add(candidate);
            }

            SelectAllCommand = new RelayCommand(() => SetAllSelections(true));
            ClearAllCommand = new RelayCommand(() => SetAllSelections(false));
            UpdatePreview();
        }

        public List<List<WpfPoint>> ConfirmSelection()
        {
            IsConfirmed = true;
            return Candidates
                .Where(candidate => candidate.IsSelected)
                .Select(candidate => candidate.Points.ToList())
                .ToList();
        }

        private void Candidate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AutoSeriesCandidate.IsSelected))
                return;

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectionSummary));
            UpdatePreview();
        }

        private void SetAllSelections(bool isSelected)
        {
            foreach (AutoSeriesCandidate candidate in Candidates)
                candidate.IsSelected = isSelected;
        }

        private void UpdatePreview()
        {
            using var display = _plotArea.Clone();
            if (display.Channels() == 1)
                Cv2.CvtColor(display, display, ColorConversionCodes.GRAY2BGR);
            else if (display.Channels() == 4)
                Cv2.CvtColor(display, display, ColorConversionCodes.BGRA2BGR);

            foreach (AutoSeriesCandidate candidate in Candidates)
            {
                Scalar colour = candidate.IsSelected
                    ? PlotColors.GetSeriesScalar(candidate.Index)
                    : new Scalar(150, 150, 150);
                int thickness = candidate.IsSelected ? 2 : 1;

                for (int i = 1; i < candidate.Points.Count; i++)
                {
                    Cv2.Line(
                        display,
                        ToCvPoint(candidate.Points[i - 1]),
                        ToCvPoint(candidate.Points[i]),
                        colour,
                        thickness,
                        LineTypes.AntiAlias);
                }
            }

            PreviewImage = BitmapSourceConverter.ToBitmapSource(display);
        }

        private OpenCvSharp.Point ToCvPoint(WpfPoint point)
        {
            return new OpenCvSharp.Point(
                Math.Clamp((int)Math.Round(point.X), 0, _plotArea.Width - 1),
                Math.Clamp((int)Math.Round(point.Y), 0, _plotArea.Height - 1));
        }

        public void Dispose()
        {
            foreach (AutoSeriesCandidate candidate in Candidates)
                candidate.PropertyChanged -= Candidate_PropertyChanged;
            _plotArea.Dispose();
        }
    }

    public partial class AutoSeriesCandidate : ObservableObject
    {
        public int Index { get; }
        public string Name => $"系列 {Index + 1}";
        public IReadOnlyList<WpfPoint> Points { get; }
        public string Details => $"{Points.Count} 点";

        [ObservableProperty]
        private bool _isSelected = true;

        public AutoSeriesCandidate(int index, IReadOnlyList<WpfPoint> points)
        {
            Index = index;
            Points = points;
        }
    }
}
