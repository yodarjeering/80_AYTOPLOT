using AutoPlot.Models;
using AutoPlot.Services;
using AutoPlot.Utils;
using AutoPlot.Views;
using AutoPlot.ImageProcessing.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MathNet.Numerics.Statistics;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows;
using System.Windows.Media.Imaging;
// debug用
using System.Text;
using System.Diagnostics;



namespace AutoPlot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageProcessingService _service = new();

        // ===== Bindable =====
        [ObservableProperty] private string _imagePath = "";
        [ObservableProperty] private string _resultText = "";
        [ObservableProperty] private CurveData _curveData;

        [ObservableProperty] private int _seriesCount = 1;
        [ObservableProperty] private int _currentSeriesIndex = 0;
        [ObservableProperty] private bool _isSeriesTraceMode = false;

        // ===== Image =====
        private BitmapSource _inputBitmap;
        private BitmapSource _originalBitmap;

        public BitmapSource InputBitmap
        {
            get => _inputBitmap;
            set => SetProperty(ref _inputBitmap, value);
        }

        // ===== ROI / Plot =====
        private OpenCvSharp.Rect _roi;
        private Mat _plotArea;
        private Mat _workingImage; //　キャリブレーション＆ノイズ除去の基準画像
        private Mat _originalPlotArea; // ノイズ除去前の plotArea を保持（ノイズ除去のやり直し用）
        private bool _hasNoiseRemovalApplied = false;
        public double CanvasWidth  { get; set; }
        public double CanvasHeight { get; set; }
        private BitmapSource _graphBitmap;
        private List<List<System.Windows.Point>> _rawTraceSeries = new();
        private List<List<System.Windows.Point>> _detectedPixelSeries = new();
        private List<List<ImagePoint>> _detectedSeries = new();

        public BitmapSource GraphBitmap
        {
            get => _graphBitmap;
            set => SetProperty(ref _graphBitmap, value);
        }


        // ===== Noise Removal =====
        private Mat _noiseMask;
        private int _penSize = 10;

        // ===== State =====
        private DisplayState _displayState = DisplayState.None;
        public DisplayState DisplayState => _displayState;

        // ===== Axis Settings =====
        private AxisSettings _axisSettings = new()
        {
            XMin = 0,
            XMax = 10,
            IsXLog = false,
            YMin = 0,
            YMax = 10,
            IsYLog = false
        };

        public ExtractionSettings ExtractionSettings { get; private set; } = new();

        // ===== Commands =====
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand AxisCalibrationCommand { get; }
        public IRelayCommand ShowOriginalImageCommand { get; }
        public IRelayCommand NoiseRemovalCommand { get; }
        public IRelayCommand NoiseRemovalCompleteCommand { get; }
        public IRelayCommand ExtractionSettingsCommand { get; }
        public IRelayCommand OnShowUpdateGraphCommand { get; }
        public IRelayCommand CopyCurveDataCommand { get; }
        //public IRelayCommand StartSeriesTraceCommand { get; }
        public IRelayCommand ShowNoiseRemovalWindowCommand { get; } // <- 14 追加
        public IRelayCommand ShowSeriesTraceWindowCommand { get; }


        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(OnLoadImage);
            AxisCalibrationCommand = new RelayCommand(OnAxisCalibration);
            ShowOriginalImageCommand = new RelayCommand(OnShowOriginalImage);
            NoiseRemovalCommand = new RelayCommand(OnNoiseRemoval);
            ShowNoiseRemovalWindowCommand = new RelayCommand(OnShowNoiseRemovalWindow); // <- 14追加
            ShowSeriesTraceWindowCommand = new RelayCommand(OnShowSeriesTraceWindow);
            ExtractionSettingsCommand = new RelayCommand(OnExtractionSettings);
            OnShowUpdateGraphCommand = new RelayCommand(OnShowUpdateGraph);
            CopyCurveDataCommand = new RelayCommand(OnCopyCurveData);
            //StartSeriesTraceCommand = new RelayCommand(OnStartSeriesTrace);

        }

        // =========================================================
        // Load Image
        // =========================================================
        private async void OnLoadImage()
        {
            if (string.IsNullOrEmpty(ImagePath))
                return;

            ResultText = "処理中…";
            _rawTraceSeries.Clear();
            _detectedPixelSeries.Clear();
            _detectedSeries.Clear();
            _hasNoiseRemovalApplied = false;
            _originalBitmap = OpenCvUtils.LoadBitmap(ImagePath);
            InputBitmap = _originalBitmap;
            _displayState = DisplayState.Original;
            Mat inputImage = OpenCvUtils.BitmapImageToMat(_originalBitmap);

            _roi = await Task.Run(() =>
                _service.RunRoi(inputImage));

            _workingImage?.Dispose();
            _workingImage = inputImage.Clone();
        }

        // =========================================================
        // Axis Calibration
        // =========================================================
        private void OnAxisCalibration()
        {
            if (_displayState == DisplayState.AxisCalibrated)
                return;

            if (_originalBitmap == null || _roi.Width <= 0 || _roi.Height <= 0)
                return;

            using var src = OpenCvUtils.BitmapImageToMat(_originalBitmap);
            using var highlighted = _service.CreateRoiHighlightImage(src, _roi);

            InputBitmap = BitmapSourceConverter.ToBitmapSource(highlighted);
            ResultText = "軸キャリブレーション表示中";

            var vm = new AxisCalibrationDialogViewModel
            {
                XMin = _axisSettings.XMin,
                XMax = _axisSettings.XMax,
                IsXLog = _axisSettings.IsXLog,
                YMin = _axisSettings.YMin,
                YMax = _axisSettings.YMax,
                IsYLog = _axisSettings.IsYLog
            };

            var dialog = new AxisCalibrationDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = Application.Current.MainWindow.Left +
                       Application.Current.MainWindow.Width * 0.6,
                Top = Application.Current.MainWindow.Top + 50
            };

            if (dialog.ShowDialog() != true)
                return;

            ApplyAxisCalibration(vm);
        }

        private void ApplyAxisCalibration(AxisCalibrationDialogViewModel vm)
        {
            _axisSettings.XMin = vm.XMin;
            _axisSettings.XMax = vm.XMax;
            _axisSettings.IsXLog = vm.IsXLog;
            _axisSettings.YMin = vm.YMin;
            _axisSettings.YMax = vm.YMax;
            _axisSettings.IsYLog = vm.IsYLog;
            _rawTraceSeries.Clear();
            _detectedPixelSeries.Clear();
            _detectedSeries.Clear();

            _plotArea?.Dispose();
            _plotArea = new Mat(_workingImage, _roi).Clone();
            _originalPlotArea = _plotArea.Clone(); // キャリブレーション後の plotArea を保存
            _hasNoiseRemovalApplied = false;

            CurveData = RunCurrentPlotExtraction();
            _roi = CurveData.PlotRoi;

            UpdateDisplay();
            _displayState = DisplayState.AxisCalibrated;
        }

        // =========================================================
        // Original Image
        // =========================================================
        private void OnShowOriginalImage()
        {
            if (_originalBitmap == null)
                return;

            InputBitmap = _originalBitmap;
            _displayState = DisplayState.Original;
            ResultText = "原図表示";
        }

        private void OnExtractionSettings()
        {
            var vm = new ExtractionSettingsDialogViewModel(ExtractionSettings);

            var dialog = new ExtractionSettingsDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true)
                return;

            ExtractionSettings = vm.ToSettings();
            RefreshExtractionWithCurrentSettings();
            ResultText = "Extraction settings updated";
        }

        // =========================================================
        // Noise Removal
        // =========================================================
        private void OnNoiseRemoval()
        {
            if (_displayState != DisplayState.AxisCalibrated || _plotArea == null)
                return;

            _noiseMask = Mat.Zeros(_plotArea.Size(), MatType.CV_8UC1);
            _displayState = DisplayState.NoiseRemoval;
        }

        public void DrawNoiseMaskFromCanvas(System.Windows.Point prevCanvasPt, System.Windows.Point currCanvasPt)
        {
            if (_displayState != DisplayState.NoiseRemoval)
                return;

            if (_noiseMask == null || _plotArea == null)
                return;

            var p1 = CanvasPointToRoi(prevCanvasPt);
            var p2 = CanvasPointToRoi(currCanvasPt);

            if (!IsInsideMat(p1) || !IsInsideMat(p2))
                return;

            Cv2.Line(
                _noiseMask,
                new OpenCvSharp.Point((int)p1.X, (int)p1.Y),
                new OpenCvSharp.Point((int)p2.X, (int)p2.Y),
                new Scalar(255),
                _penSize,
                LineTypes.AntiAlias
            );

            UpdateNoiseOverlay();
        }

        // =========================================================
        // Display Helpers
        // =========================================================
        private void UpdateDisplay()
        {
            if (_plotArea == null)
                return;

            InputBitmap = BitmapSourceConverter.ToBitmapSource(_plotArea);
        }

        private void UpdateNoiseOverlay()
        {
            using var display = _plotArea.Clone();
            display.SetTo(PlotColors.NoiseMaskScalar, _noiseMask);
            InputBitmap = BitmapSourceConverter.ToBitmapSource(display);
        }

        // =========================================================
        // Coordinate Utils
        // =========================================================
        private System.Windows.Point CanvasPointToRoi(System.Windows.Point canvasPt)
        {
            double imgW = _plotArea.Width;
            double imgH = _plotArea.Height;

            double cvsW = CanvasWidth;
            double cvsH = CanvasHeight;

            // Uniform の倍率
            double scale = Math.Min(cvsW / imgW, cvsH / imgH);

            // 表示画像サイズ
            double dispW = imgW * scale;
            double dispH = imgH * scale;

            // 余白
            double offsetX = (cvsW - dispW) / 2;
            double offsetY = (cvsH - dispH) / 2;

            return new System.Windows.Point(
                (canvasPt.X - offsetX) / scale,
                (canvasPt.Y - offsetY) / scale
            );
        }


        private bool IsInsideMat(System.Windows.Point p)
        {
            return p.X >= 0 && p.Y >= 0 &&
                   p.X < _plotArea.Width &&
                   p.Y < _plotArea.Height;
        }

        private void UpdateDisplayWithOverlay(CurveData data)
        {
            if (data == null || _workingImage == null)
                return;

            using var baseImg = _workingImage.Clone();

            if (baseImg.Channels() == 4)
                Cv2.CvtColor(baseImg, baseImg, ColorConversionCodes.BGRA2BGR);

            if (_detectedPixelSeries.Count > 0)
            {
                DrawDetectedPixelSeriesOnImage(baseImg);
            }
            else if (data.OverlayGraphMat != null &&
                     data.OverlayGraphMat.Size() == baseImg.Size() &&
                     data.OverlayGraphMat.Channels() == 4)
            {
                ApplyOverlayMat(baseImg, data.OverlayGraphMat);
            }
            else
            {
                DrawCurveDataOnImage(baseImg, data);
            }

            InputBitmap = BitmapSourceConverter.ToBitmapSource(baseImg);
            // 抽出したグラフを描画
            OnShowExtractedGraph(CurveData);
        }

        private static void ApplyOverlayMat(Mat baseImg, Mat overlay)
        {
            using var alpha = new Mat();
            Cv2.ExtractChannel(overlay, alpha, 3);

            using var overlayBgr = new Mat();
            Cv2.CvtColor(overlay, overlayBgr, ColorConversionCodes.BGRA2BGR);

            overlayBgr.CopyTo(baseImg, alpha);
        }

        private void DrawDetectedPixelSeriesOnImage(Mat baseImg)
        {
            for (int seriesIndex = 0; seriesIndex < _detectedPixelSeries.Count; seriesIndex++)
            {
                var series = _detectedPixelSeries[seriesIndex];
                for (int i = 1; i < series.Count; i++)
                {
                    Cv2.Line(
                        baseImg,
                        ToWorkingImagePoint(series[i - 1]),
                        ToWorkingImagePoint(series[i]),
                        PlotColors.GetSeriesScalar(seriesIndex),
                        2,
                        LineTypes.AntiAlias);
                }
            }
        }

        private void DrawCurveDataOnImage(Mat baseImg, CurveData data)
        {
            if (data?.Points == null || data.Points.Count < 2 || _plotArea == null)
                return;

            for (int i = 1; i < data.Points.Count; i++)
            {
                var p1 = RealPointToWorkingImagePoint(data.Points[i - 1]);
                var p2 = RealPointToWorkingImagePoint(data.Points[i]);

                if (p1 == null || p2 == null)
                    continue;

                Cv2.Line(
                    baseImg,
                    p1.Value,
                    p2.Value,
                    PlotColors.GetSeriesScalar(0),
                    2,
                    LineTypes.AntiAlias);
            }
        }

        private OpenCvSharp.Point ToWorkingImagePoint(System.Windows.Point roiPoint)
        {
            return new OpenCvSharp.Point(
                _roi.X + Math.Clamp((int)Math.Round(roiPoint.X), 0, _roi.Width - 1),
                _roi.Y + Math.Clamp((int)Math.Round(roiPoint.Y), 0, _roi.Height - 1));
        }

        private OpenCvSharp.Point? RealPointToWorkingImagePoint(ImagePoint point)
        {
            double x = RealToPixel(
                point.X,
                _axisSettings.XMin,
                _axisSettings.XMax,
                _roi.Width,
                _axisSettings.IsXLog);

            double y = RealToPixel(
                point.Y,
                _axisSettings.YMin,
                _axisSettings.YMax,
                _roi.Height,
                _axisSettings.IsYLog,
                true);

            if (double.IsNaN(x) || double.IsNaN(y))
                return null;

            return new OpenCvSharp.Point(
                _roi.X + Math.Clamp((int)Math.Round(x), 0, _roi.Width - 1),
                _roi.Y + Math.Clamp((int)Math.Round(y), 0, _roi.Height - 1));
        }

        private static double RealToPixel(double value, double min, double max, int pixelLength, bool isLog, bool invert = false)
        {
            if (pixelLength <= 1)
                return double.NaN;

            if (isLog)
            {
                if (value <= 0 || min <= 0 || max <= 0)
                    return double.NaN;

                value = Math.Log10(value);
                min = Math.Log10(min);
                max = Math.Log10(max);
            }

            if (Math.Abs(max - min) < 1e-12)
                return double.NaN;

            double t = (value - min) / (max - min);
            if (invert)
                t = 1 - t;

            return t * (pixelLength - 1);
        }


        private void OnShowUpdateGraph(){
            _displayState = DisplayState.GraphPlot;
            UpdateDisplayWithOverlay(CurveData);
        }

        private void OnShowSeriesTraceWindow()
        {
            if (_plotArea == null)
            {
                MessageBox.Show("先に画像読込と軸設定を完了してください。");
                return;
            }

            var vm = new SeriesTraceViewModel(BitmapSourceConverter.ToBitmapSource(_plotArea));

            var window = new SeriesTraceWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            bool? result = window.ShowDialog();
            if (result == true && vm.IsConfirmed)
            {
                _rawTraceSeries = vm.ResultSeries.Select(series => series.ToList()).ToList();
                _detectedSeries = DetectSeriesFromTraceGuides(_rawTraceSeries);

                if (_detectedSeries.Count == 0)
                {
                    ResultText = "No curve pixels were found near the traced guide(s).";
                    MessageBox.Show(
                        "No curve pixels were found near the traced guide(s). Please trace closer to the curve or adjust noise removal.",
                        "Trace Series Detection");
                    return;
                }

                if (_detectedSeries.Count < _rawTraceSeries.Count)
                {
                    ResultText = $"Detected {_detectedSeries.Count} of {_rawTraceSeries.Count} traced series.";
                    MessageBox.Show(
                        $"Detected {_detectedSeries.Count} of {_rawTraceSeries.Count} traced series. Series without nearby curve pixels were skipped instead of using raw trace points.",
                        "Trace Series Detection");
                    return;
                }

                ResultText = $"{_detectedSeries.Count} detected series from trace guide(s)";
            }
        }

        // 14追加
        private void OnShowNoiseRemovalWindow()
        {
            if (_plotArea == null || _workingImage == null)
            {
                MessageBox.Show("先に画像読込と軸設定を完了してください。");
                return;
            }

            var vm = new NoiseRemovalViewModel(_plotArea,_originalPlotArea);

            var window = new NoiseRemovalWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            bool? result = window.ShowDialog();
            if (result != true || !vm.IsConfirmed || vm.ResultPlotArea == null)
                return;

            // 旧 OnNoiseRemovalComplete の役割をここで引き継ぐ
            Mat plotAreaForGrid = _plotArea.Clone();   // マスク適用前
            _plotArea.Dispose();
            _plotArea = vm.ResultPlotArea.Clone();     // マスク適用後
            Mat plotAreaForAnalysis = _plotArea.Clone();

            using var updated = _workingImage.Clone();
            using var roiMat = new Mat(updated, _roi);
            _plotArea.CopyTo(roiMat);

            _workingImage.Dispose();
            _workingImage = updated.Clone();

            InputBitmap = BitmapSourceConverter.ToBitmapSource(_workingImage);

            CurveData = _service.RunPlotArea(
                plotAreaForGrid,
                plotAreaForAnalysis,
                _roi,
                _workingImage.Size(),
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear",
                ExtractionSettings
            );

            _hasNoiseRemovalApplied = true;

            if (_rawTraceSeries.Count > 0)
            {
                _detectedSeries = DetectSeriesFromTraceGuides(_rawTraceSeries);
                ResultText = _detectedSeries.Count > 0
                    ? $"{_detectedSeries.Count} detected series from trace guide(s) after noise removal"
                    : "No curve pixels were found near the traced guide(s) after noise removal.";
            }
            else
            {
                _detectedPixelSeries.Clear();
                _detectedSeries.Clear();
            }

            _displayState = DisplayState.AxisCalibrated;
    }

        private CurveData RunCurrentPlotExtraction()
        {
            Mat plotAreaForGrid = _hasNoiseRemovalApplied && _originalPlotArea != null
                ? _originalPlotArea
                : _plotArea;

            Mat? plotAreaForAnalysis = _hasNoiseRemovalApplied
                ? _plotArea
                : null;

            return _service.RunPlotArea(
                plotAreaForGrid,
                plotAreaForAnalysis,
                _roi,
                _workingImage.Size(),
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear",
                ExtractionSettings
            );
        }

        private void RefreshExtractionWithCurrentSettings()
        {
            if (_plotArea == null || _workingImage == null || _roi.Width <= 0 || _roi.Height <= 0)
                return;

            CurveData = RunCurrentPlotExtraction();

            if (_rawTraceSeries.Count > 0)
                _detectedSeries = DetectSeriesFromTraceGuides(_rawTraceSeries);

            if (_displayState == DisplayState.GraphPlot)
                UpdateDisplayWithOverlay(CurveData);
        }

        private void OnShowExtractedGraph(CurveData data)
        {
            var mat = _detectedSeries.Count > 0
                ? OpenCvUtils.RenderGraphFromSeries(
                    _detectedSeries,
                    600, 400,
                    _axisSettings.XMin,
                    _axisSettings.XMax,
                    _axisSettings.YMin,
                    _axisSettings.YMax,
                    _axisSettings.IsXLog,
                    _axisSettings.IsYLog)
                : OpenCvUtils.RenderGraphFromCurveData(
                    data,
                    600, 400,
                    _axisSettings.XMin,
                    _axisSettings.XMax,
                    _axisSettings.YMin,
                    _axisSettings.YMax,
                    _axisSettings.IsXLog,
                    _axisSettings.IsYLog
                );

            GraphBitmap = BitmapSourceConverter.ToBitmapSource(mat);
        }

        private string BuildCurveDataText(CurveData data)
        {
            if (_detectedSeries.Count > 0)
                return BuildDetectedSeriesText(_detectedSeries);

            if (data?.Points == null || data.Points.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var p in data.Points)
            {
                sb.AppendLine($"{p.X,14:F6}\t{p.Y,14:F6}");
            }

            return sb.ToString();
        }

        private List<List<ImagePoint>> DetectSeriesFromTraceGuides(List<List<System.Windows.Point>> rawTraceSeries)
        {
            var detectedSeries = new List<List<ImagePoint>>();
            _detectedPixelSeries.Clear();

            if (_plotArea == null || rawTraceSeries.Count == 0)
                return detectedSeries;

            using var plotBgr = new Mat();
            if (_plotArea.Channels() == 1)
                Cv2.CvtColor(_plotArea, plotBgr, ColorConversionCodes.GRAY2BGR);
            else if (_plotArea.Channels() == 4)
                Cv2.CvtColor(_plotArea, plotBgr, ColorConversionCodes.BGRA2BGR);
            else
                _plotArea.CopyTo(plotBgr);

            using var gray = new Mat();
            using var hsv = new Mat();
            Cv2.CvtColor(plotBgr, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(plotBgr, hsv, ColorConversionCodes.BGR2HSV);

            foreach (var rawSeries in rawTraceSeries)
            {
                var detectedPixels = DetectPixelSeriesNearGuide(rawSeries, gray, hsv, plotBgr.Width, plotBgr.Height, ExtractionSettings);
                if (detectedPixels.Count == 0)
                    continue;

                _detectedPixelSeries.Add(detectedPixels);
                detectedSeries.Add(ConvertPixelSeriesToRealPoints(detectedPixels, plotBgr.Width, plotBgr.Height));
            }

            return detectedSeries;
        }

        private static List<System.Windows.Point> DetectPixelSeriesNearGuide(
            List<System.Windows.Point> guideSeries,
            Mat gray,
            Mat hsv,
            int imageWidth,
            int imageHeight,
            ExtractionSettings settings)
        {
            const int minColumnCandidates = 1;

            var detectedPixels = new List<System.Windows.Point>();
            if (guideSeries.Count < 2)
                return detectedPixels;

            //using var traceMask = Mat.Zeros(imageHeight, imageWidth, MatType.CV_8UC1);
            using var traceMask = new Mat(imageHeight, imageWidth, MatType.CV_8UC1, Scalar.Black);
            for (int i = 1; i < guideSeries.Count; i++)
            {
                Cv2.Line(
                    traceMask,
                    ToCvPoint(guideSeries[i - 1], imageWidth, imageHeight),
                    ToCvPoint(guideSeries[i], imageWidth, imageHeight),
                    new Scalar(255),
                    settings.TraceSearchBandWidth,
                    LineTypes.AntiAlias);
            }

            var guideByX = BuildGuideYByX(guideSeries, imageWidth);

            for (int x = 0; x < imageWidth; x++)
            {
                if (!guideByX.TryGetValue(x, out double guideY))
                    continue;

                var candidates = new List<int>();
                for (int y = 0; y < imageHeight; y++)
                {
                    if (traceMask.Get<byte>(y, x) == 0)
                        continue;

                    byte grayValue = gray.Get<byte>(y, x);
                    Vec3b hsvValue = hsv.Get<Vec3b>(y, x);
                    bool isDarkInk = grayValue < settings.CurveThreshold;
                    bool isColoredInk = hsvValue.Item0 > 5 && hsvValue.Item0 < 175 && hsvValue.Item1 > 45 && hsvValue.Item2 < 250;

                    if (isDarkInk || isColoredInk)
                        candidates.Add(y);
                }

                if (candidates.Count < minColumnCandidates)
                    continue;

                int bestY = candidates
                    .OrderBy(y => Math.Abs(y - guideY))
                    .First();

                var localCluster = candidates
                    .Where(y => Math.Abs(y - bestY) <= 2)
                    .ToList();

                double detectedY = localCluster.Count > 0 ? localCluster.Average() : bestY;
                detectedPixels.Add(new System.Windows.Point(x, detectedY));
            }

            detectedPixels = RemoveOutlierPoints(
                detectedPixels,
                settings.MovingAverageWindow,
                settings.OutlierRemovalThreshold);
            detectedPixels = ApplyMovingAverageToPoints(
                detectedPixels,
                settings.MovingAverageWindow);

            return detectedPixels;
        }

        private static List<System.Windows.Point> ApplyMovingAverageToPoints(
            List<System.Windows.Point> points,
            int window)
        {
            if (points.Count == 0 || window <= 1)
                return points;

            int normalizedWindow = Math.Max(1, window);
            int leftRadius = (normalizedWindow - 1) / 2;
            int rightRadius = normalizedWindow / 2;
            var smoothed = new List<System.Windows.Point>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                int start = Math.Max(0, i - leftRadius);
                int end = Math.Min(points.Count - 1, i + rightRadius);
                double y = points
                    .Skip(start)
                    .Take(end - start + 1)
                    .Average(p => p.Y);

                smoothed.Add(new System.Windows.Point(points[i].X, y));
            }

            return smoothed;
        }

        private static List<System.Windows.Point> RemoveOutlierPoints(
            List<System.Windows.Point> points,
            int window,
            int threshold)
        {
            if (points.Count == 0 || threshold <= 0)
                return points;

            var filtered = points.OrderBy(p => p.X).ToList();

            for (int pass = 0; pass < 2; pass++)
            {
                var next = new List<System.Windows.Point>(filtered.Count);
                int normalizedWindow = Math.Max(5, window);
                int leftRadius = (normalizedWindow - 1) / 2;
                int rightRadius = normalizedWindow / 2;

                for (int i = 0; i < filtered.Count; i++)
                {
                    if (!TryPredictYFromNeighborPoints(filtered, i, leftRadius, rightRadius, out double expectedY) ||
                        Math.Abs(filtered[i].Y - expectedY) <= threshold)
                    {
                        next.Add(filtered[i]);
                    }
                }

                if (next.Count == filtered.Count)
                    return filtered;

                filtered = next;
            }

            return filtered;
        }

        private static bool TryPredictYFromNeighborPoints(
            List<System.Windows.Point> points,
            int targetIndex,
            int leftRadius,
            int rightRadius,
            out double expectedY)
        {
            int start = Math.Max(0, targetIndex - leftRadius);
            int end = Math.Min(points.Count - 1, targetIndex + rightRadius);
            var neighbors = new List<System.Windows.Point>();

            for (int i = start; i <= end; i++)
            {
                if (i != targetIndex)
                    neighbors.Add(points[i]);
            }

            if (neighbors.Count < 2)
            {
                expectedY = 0;
                return false;
            }

            var target = points[targetIndex];
            System.Windows.Point left = default;
            System.Windows.Point right = default;
            bool hasLeft = false;
            bool hasRight = false;

            for (int i = neighbors.Count - 1; i >= 0; i--)
            {
                if (neighbors[i].X < target.X)
                {
                    left = neighbors[i];
                    hasLeft = true;
                    break;
                }
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                if (neighbors[i].X > target.X)
                {
                    right = neighbors[i];
                    hasRight = true;
                    break;
                }
            }

            if (hasLeft && hasRight && Math.Abs(right.X - left.X) > 1e-9)
            {
                double t = (target.X - left.X) / (right.X - left.X);
                expectedY = left.Y + t * (right.Y - left.Y);
                return true;
            }

            expectedY = Statistics.Median(neighbors.Select(p => p.Y));
            return true;
        }

        private static Dictionary<int, double> BuildGuideYByX(List<System.Windows.Point> guideSeries, int imageWidth)
        {
            var guideByX = new Dictionary<int, double>();

            for (int i = 1; i < guideSeries.Count; i++)
            {
                var p1 = guideSeries[i - 1];
                var p2 = guideSeries[i];
                int startX = Math.Clamp((int)Math.Round(Math.Min(p1.X, p2.X)), 0, imageWidth - 1);
                int endX = Math.Clamp((int)Math.Round(Math.Max(p1.X, p2.X)), 0, imageWidth - 1);

                if (startX == endX)
                {
                    guideByX[startX] = (p1.Y + p2.Y) / 2.0;
                    continue;
                }

                for (int x = startX; x <= endX; x++)
                {
                    double t = (x - p1.X) / (p2.X - p1.X);
                    if (double.IsNaN(t) || double.IsInfinity(t))
                        continue;

                    guideByX[x] = p1.Y + t * (p2.Y - p1.Y);
                }
            }

            return guideByX;
        }

        private List<ImagePoint> ConvertPixelSeriesToRealPoints(
            List<System.Windows.Point> detectedPixels,
            int imageWidth,
            int imageHeight)
        {
            double[] xPx = detectedPixels.Select(p => p.X).ToArray();
            double[] yPx = detectedPixels.Select(p => p.Y).ToArray();

            double[] xReal = PixelConverter.PxToReal(
                xPx, 0, imageWidth,
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.IsXLog ? "log" : "linear");

            double[] yReal = PixelConverter.PxToReal(
                yPx, 0, imageHeight,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsYLog ? "log" : "linear",
                true);

            var realSeries = new List<ImagePoint>();
            for (int i = 0; i < xReal.Length; i++)
            {
                realSeries.Add(new ImagePoint
                {
                    X = xReal[i],
                    Y = yReal[i]
                });
            }

            return realSeries;
        }

        private static OpenCvSharp.Point ToCvPoint(System.Windows.Point point, int imageWidth, int imageHeight)
        {
            return new OpenCvSharp.Point(
                Math.Clamp((int)Math.Round(point.X), 0, imageWidth - 1),
                Math.Clamp((int)Math.Round(point.Y), 0, imageHeight - 1));
        }

        private string BuildDetectedSeriesText(List<List<ImagePoint>> tracedSeries)
        {
            var sb = new StringBuilder();
            sb.Append("X");
            for (int i = 0; i < tracedSeries.Count; i++)
                sb.Append($"\tY{i + 1}");
            sb.AppendLine();

            var xValues = tracedSeries
                .SelectMany(series => series.Select(point => point.X))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (double x in xValues)
            {
                sb.Append($"{x,14:F6}");

                foreach (var series in tracedSeries)
                {
                    double? y = InterpolateY(series, x);
                    sb.Append('\t');
                    if (y.HasValue)
                        sb.Append($"{y.Value,14:F6}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static double? InterpolateY(List<ImagePoint> series, double x)
        {
            if (series.Count == 0)
                return null;

            var ordered = series.OrderBy(point => point.X).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (Math.Abs(ordered[i].X - x) < 1e-9)
                    return ordered[i].Y;
            }

            for (int i = 1; i < ordered.Count; i++)
            {
                var left = ordered[i - 1];
                var right = ordered[i];

                if (x < left.X || x > right.X || Math.Abs(right.X - left.X) < 1e-12)
                    continue;

                double t = (x - left.X) / (right.X - left.X);
                return left.Y + t * (right.Y - left.Y);
            }

            return null;
        }

        private void OnCopyCurveData()
        {
            if (CurveData == null)
                return;

            string text = BuildCurveDataText(CurveData);

            var vm = new CurveDataCopyDialogViewModel(text);

            var dialog = new CurveDataCopyDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
        }

        public async void LoadImageFromClipboard(BitmapSource bitmap)
        {
            if (bitmap == null)
                return;

            // 表示更新
            _originalBitmap = bitmap;
            InputBitmap = _originalBitmap;

            // 解析用に Mat 化（必要なら）
            using var mat = BitmapSourceConverter.ToMat(bitmap);

            _displayState = DisplayState.Original;
            _rawTraceSeries.Clear();
            _detectedPixelSeries.Clear();
            _detectedSeries.Clear();
            _hasNoiseRemovalApplied = false;
            Mat inputImage = OpenCvUtils.BitmapImageToMat(InputBitmap);

            _roi = await Task.Run(() =>
                _service.RunRoi(inputImage));

            _workingImage?.Dispose();
            _workingImage = inputImage.Clone();
        }
    }
}

// TBD debug
        // MessageBox.Show(
        //     $"Overlay size mismatch\n" +
        //     $"base: {baseImg.Size()}\n" +
        //     $"overlay: {data.OverlayGraphMat.Size()}"
        // );
