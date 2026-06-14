using AutoPlot.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoPlot.Views
{
    public partial class SeriesTraceWindow : Window
    {
        private readonly Brush[] _seriesBrushes =
        {
            Brushes.Red,
            Brushes.Blue,
            Brushes.Green,
            Brushes.Orange,
            Brushes.Purple,
            Brushes.Brown,
            Brushes.DeepPink,
            Brushes.Teal
        };

        private bool _isDrawing;
        private Polyline? _currentLine;

        public SeriesTraceWindow()
        {
            InitializeComponent();
        }

        private void TraceCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SeriesTraceViewModel vm || !vm.CanTrace)
                return;

            Point point = e.GetPosition(TraceCanvas);
            vm.BeginSeries(point);

            _currentLine = new Polyline
            {
                Stroke = GetSeriesBrush(vm.CurrentSeriesIndex),
                StrokeThickness = 2
            };
            _currentLine.Points.Add(point);
            TraceCanvas.Children.Add(_currentLine);

            _isDrawing = true;
            TraceCanvas.CaptureMouse();
        }

        private void TraceCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentLine == null || DataContext is not SeriesTraceViewModel vm)
                return;

            Point point = e.GetPosition(TraceCanvas);
            vm.AddPoint(point);
            _currentLine.Points.Add(point);
        }

        private void TraceCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || DataContext is not SeriesTraceViewModel vm)
                return;

            _isDrawing = false;
            TraceCanvas.ReleaseMouseCapture();
            vm.CompleteSeries();
            _currentLine = null;
        }

        private void StartTraceButton_Click(object sender, RoutedEventArgs e)
        {
            TraceCanvas.Children.Clear();
            _currentLine = null;
            _isDrawing = false;
        }

        private Brush GetSeriesBrush(int seriesIndex)
        {
            return _seriesBrushes[seriesIndex % _seriesBrushes.Length];
        }
    }
}
