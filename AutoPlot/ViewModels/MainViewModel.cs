using AutoPlot.Models;
using AutoPlot.Services;
using AutoPlot.Utils;
using AutoPlot.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private BitmapSource _graphBitmap;
        
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

        // ===== Commands =====
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand AxisCalibrationCommand { get; }
        public IRelayCommand ShowOriginalImageCommand { get; }
        public IRelayCommand NoiseRemovalCommand { get; }
        public IRelayCommand NoiseRemovalCompleteCommand { get; }
        public IRelayCommand OnShowUpdateGraphCommand { get; }
        public IRelayCommand CopyCurveDataCommand { get; }


        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(OnLoadImage);
            AxisCalibrationCommand = new RelayCommand(OnAxisCalibration);
            ShowOriginalImageCommand = new RelayCommand(OnShowOriginalImage);
            NoiseRemovalCommand = new RelayCommand(OnNoiseRemoval);
            NoiseRemovalCompleteCommand = new RelayCommand(OnNoiseRemovalComplete);
            OnShowUpdateGraphCommand = new RelayCommand(OnShowUpdateGraph); //←追加
            CopyCurveDataCommand = new RelayCommand(OnCopyCurveData);

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

            _plotArea?.Dispose();
            _plotArea = new Mat(_workingImage, _roi).Clone();

            var data = _service.RunPlotArea(
                _plotArea,
                _roi,
                _workingImage.Size(),
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear"
            );

            CurveData = data; 
            _roi = data.PlotRoi;

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
            if (_plotArea == null || _workingImage == null || _noiseMask == null)
                return;

            // ① plotArea にノイズ反映（ここで初めて確定）
            _plotArea = _service.ApplyNoiseMask(_plotArea, _noiseMask);

            // ② workingImage に貼り戻す
            using var updated = _workingImage.Clone();
            using var roiMat = new Mat(updated, _roi);
            _plotArea.CopyTo(roiMat);

            _workingImage.Dispose();
            _workingImage = updated.Clone();

            // ③ 表示更新（workingImage を見る）
            InputBitmap = BitmapSourceConverter.ToBitmapSource(_workingImage);

            // ④ ここで初めてマスクを破棄
            _noiseMask.Dispose();
            _noiseMask = null;
            
            // ★ ここで plotArea を元に再処理
            CurveData = _service.RunPlotArea(
                _plotArea,
                _roi,
                _workingImage.Size(),
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear"
            );

            // TBD
            // var sb = new StringBuilder();

            // int showCount = Math.Min(100, CurveData.Points.Count);
            // for (int i = 0; i < showCount; i++)
            // {
            //     sb.AppendLine($"[{i}] X={CurveData.Points[i].X}, Y={CurveData.Points[i].Y}");
            // }

            // // TBD
            // Debug.WriteLine(sb.ToString());

            
            _displayState = DisplayState.AxisCalibrated;

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

        private void UpdateDisplayWithOverlay(CurveData data)
        {
            if (data == null || _workingImage == null)
                return;

            using var baseImg = _workingImage.Clone();

            if (baseImg.Channels() == 4)
                Cv2.CvtColor(baseImg, baseImg, ColorConversionCodes.BGRA2BGR);

            var overlay = data.OverlayGraphMat;
            if (overlay == null)
                return;

            // ★ サイズが違ったら即エラーにする
            if (overlay.Size() != baseImg.Size())
            {
                MessageBox.Show(
                    $"Overlay size mismatch\n" +
                    $"Base: {baseImg.Size()}\n" +
                    $"Overlay: {overlay.Size()}",
                    "UpdateDisplayWithOverlay ERROR"
                );
                return;
            }

            using var alpha = new Mat();
            Cv2.ExtractChannel(overlay, alpha, 3);

            using var overlayBgr = new Mat();
            Cv2.CvtColor(overlay, overlayBgr, ColorConversionCodes.BGRA2BGR);

            overlayBgr.CopyTo(baseImg, alpha);

            InputBitmap = BitmapSourceConverter.ToBitmapSource(baseImg);
            // 抽出したグラフを描画
            OnShowExtractedGraph(CurveData);
        }


        private void OnShowUpdateGraph(){
            _displayState = DisplayState.GraphPlot;
            UpdateDisplayWithOverlay(CurveData);
        }

        private void OnShowExtractedGraph(CurveData data)
        {
            var mat = OpenCvUtils.RenderGraphFromCurveData(
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
            if (data?.Points == null || data.Points.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var p in data.Points)
            {
                // TBD ここをスペースでなく 「,」区切りにする？
                sb.AppendLine($"{p.X:G6}\t{p.Y:G6}");
            }

            return sb.ToString();
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



    }
}

// TBD debug
        // MessageBox.Show(
        //     $"Overlay size mismatch\n" +
        //     $"base: {baseImg.Size()}\n" +
        //     $"overlay: {data.OverlayGraphMat.Size()}"
        // );