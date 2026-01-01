using AutoPlot.Models;
using AutoPlot.Services;
using AutoPlot.Utils;
using AutoPlot.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AutoPlot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageProcessingService _service = new();

        // ===== Bindable =====
        [ObservableProperty] private string _imagePath = "";
        [ObservableProperty] private string _resultText = "";
        [ObservableProperty] private CurveData _curveData;

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
        public double CanvasWidth  { get; set; }
        public double CanvasHeight { get; set; }


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

        // ===== Commands =====
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand AxisCalibrationCommand { get; }
        public IRelayCommand ShowOriginalImageCommand { get; }
        public IRelayCommand NoiseRemovalCommand { get; }
        public IRelayCommand NoiseRemovalCompleteCommand { get; }

        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(OnLoadImage);
            AxisCalibrationCommand = new RelayCommand(OnAxisCalibration);
            ShowOriginalImageCommand = new RelayCommand(OnShowOriginalImage);
            NoiseRemovalCommand = new RelayCommand(OnNoiseRemoval);
            NoiseRemovalCompleteCommand = new RelayCommand(OnNoiseRemovalComplete);
        }

        // =========================================================
        // Load Image
        // =========================================================
        private async void OnLoadImage()
        {
            if (string.IsNullOrEmpty(ImagePath))
                return;

            ResultText = "処理中…";

            _originalBitmap = OpenCvUtils.LoadBitmap(ImagePath);
            InputBitmap = _originalBitmap;
            _displayState = DisplayState.Original;
            Mat inputImage = OpenCvUtils.BitmapImageToMat(_originalBitmap);
            
            var data = await Task.Run(() =>
                _service.Run(
                    inputImage,
                    0.5, 3,
                    1, 1000,
                    "linear", "log"
                ));

            CurveData = data;
            _roi = data.PlotRoi;

            ResultText = $"点数: {data.Points.Count}";

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

            var data = _service.Run(
                _workingImage,
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear"
            );

            CurveData = data;
            _roi = data.PlotRoi;

            // using var src = OpenCvUtils.BitmapImageToMat(InputBitmap);
            // _plotArea = new Mat(src, _roi).Clone();
            // ★ plotArea は「結果」として作る
            _plotArea?.Dispose();
            _plotArea = new Mat(_workingImage, _roi).Clone();

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
                Scalar.White,
                _penSize
            );

            UpdateNoiseOverlay();
        }

        private void OnNoiseRemovalComplete()
        {
            if (_noiseMask == null || _plotArea == null)
                return;

            _plotArea = _service.ApplyNoiseMask(_plotArea, _noiseMask);
            // ★ 基準画像を更新
            _workingImage?.Dispose();
            _workingImage = _plotArea.Clone();

            _noiseMask.Dispose();
            _noiseMask = null;

            _displayState = DisplayState.AxisCalibrated;
            UpdateDisplay();
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
            display.SetTo(new Scalar(0, 0, 255), _noiseMask);
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
    }
}
