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

            Point canvasPoint = e.GetPosition(TraceCanvas);
            Point imagePoint = CanvasPointToImagePoint(canvasPoint);
            vm.BeginSeries(imagePoint);

            _currentLine = new Polyline
            {
                Stroke = GetSeriesBrush(vm.CurrentSeriesIndex),
                StrokeThickness = 10
            };
            _currentLine.Points.Add(canvasPoint);
            TraceCanvas.Children.Add(_currentLine);

            _isDrawing = true;
            TraceCanvas.CaptureMouse();
        }

        private void TraceCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentLine == null || DataContext is not SeriesTraceViewModel vm)
                return;

            Point canvasPoint = e.GetPosition(TraceCanvas);
            Point imagePoint = CanvasPointToImagePoint(canvasPoint);
            vm.AddPoint(imagePoint);
            _currentLine.Points.Add(canvasPoint);
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

        private void EditTraceButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(RedrawCompletedSeries);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (DataContext is SeriesTraceViewModel { IsConfirmed: true })
                    DialogResult = true;
            });
        }

        private void RedrawCompletedSeries()
        {
            TraceCanvas.Children.Clear();

            if (DataContext is not SeriesTraceViewModel vm)
                return;

            for (int i = 0; i < vm.TracedSeries.Count; i++)
            {
                var line = new Polyline
                {
                    Stroke = GetSeriesBrush(i),
                    StrokeThickness = 2
                };

                foreach (Point imagePoint in vm.TracedSeries[i])
                    line.Points.Add(ImagePointToCanvasPoint(imagePoint));

                TraceCanvas.Children.Add(line);
            }
        }

        private Point CanvasPointToImagePoint(Point canvasPoint)
        {
            if (DataContext is not SeriesTraceViewModel vm || vm.PlotImage == null)
                return canvasPoint;

            double imgW = vm.PlotImage.PixelWidth;
            double imgH = vm.PlotImage.PixelHeight;
            double scale = Math.Min(TraceCanvas.ActualWidth / imgW, TraceCanvas.ActualHeight / imgH);

            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                return canvasPoint;

            double dispW = imgW * scale;
            double dispH = imgH * scale;
            double offsetX = (TraceCanvas.ActualWidth - dispW) / 2;
            double offsetY = (TraceCanvas.ActualHeight - dispH) / 2;

            return new Point(
                Math.Clamp((canvasPoint.X - offsetX) / scale, 0, imgW - 1),
                Math.Clamp((canvasPoint.Y - offsetY) / scale, 0, imgH - 1)
            );
        }

        private Point ImagePointToCanvasPoint(Point imagePoint)
        {
            if (DataContext is not SeriesTraceViewModel vm || vm.PlotImage == null)
                return imagePoint;

            double imgW = vm.PlotImage.PixelWidth;
            double imgH = vm.PlotImage.PixelHeight;
            double scale = Math.Min(TraceCanvas.ActualWidth / imgW, TraceCanvas.ActualHeight / imgH);

            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                return imagePoint;

            double dispW = imgW * scale;
            double dispH = imgH * scale;
            double offsetX = (TraceCanvas.ActualWidth - dispW) / 2;
            double offsetY = (TraceCanvas.ActualHeight - dispH) / 2;

            return new Point(
                imagePoint.X * scale + offsetX,
                imagePoint.Y * scale + offsetY
            );
        }

        private Brush GetSeriesBrush(int seriesIndex)
        {
            return _seriesBrushes[seriesIndex % _seriesBrushes.Length];
        }
    }
}
