using AutoPlot.Models;
using AutoPlot.Services;
using AutoPlot.Utils;
using AutoPlot.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;



namespace AutoPlot.ViewModels
{
    
    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageProcessingService _service = new();

        [ObservableProperty]
        private string _imagePath = "";

        [ObservableProperty]
        private string _resultText = "";

        [ObservableProperty]
        private CurveData _curveData;

        private BitmapSource _inputBitmap;
        private BitmapSource _originalBitmap;
        private OpenCvSharp.Rect _roi; // CalculatePlotRoi で求めたやつ（OpenCvSharp.Rect）
        
        public ICommand AxisCalibrationCommand { get; }
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand ShowOriginalImageCommand { get; }

        private DisplayState _displayState = DisplayState.None;

        private AxisSettings _axisSettings = new AxisSettings
        {
            XMin = 0,
            XMax = 10,
            IsXLog = false,
            YMin = 0,
            YMax = 10,
            IsYLog = false
        };


        public BitmapSource InputBitmap
        {
            get => _inputBitmap;
            set => SetProperty(ref _inputBitmap, value);
        }

        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(OnLoadImage);
            AxisCalibrationCommand = new RelayCommand(OnAxisCalibration);
            ShowOriginalImageCommand = new RelayCommand(OnShowOriginalImage);
        }

        private async void OnLoadImage()
        {
            if (string.IsNullOrEmpty(ImagePath))
                return;

            ResultText = "処理中…";

            // ① 入力画像の表示（UIスレッド）
            _originalBitmap = OpenCvUtils.LoadBitmap(ImagePath);
            InputBitmap = _originalBitmap;

            _displayState = DisplayState.Original;

            // ② 重い処理だけバックグラウンド
            var data = await Task.Run(() =>
            {
                return _service.Run(
                    ImagePath,
                    0.5, 3,
                    1, 1000,
                    "linear", "log"
                );
            });

            // ③ 結果を UI に反映（ここは UIスレッド）
            CurveData = data;
            _roi = data.PlotRoi;

            ResultText = $"点数: {data.Points.Count}";
        }


        private void OnAxisCalibration()
        {
            bool hasInputImage = _inputBitmap != null;
            bool hasValidRoi = _roi.Width > 0 && _roi.Height > 0;

            if (_displayState == DisplayState.AxisCalibrated)
                return;

            if (!hasInputImage || !hasValidRoi)
                return;


            Mat src = OpenCvUtils.BitmapImageToMat(_inputBitmap);

            Mat highlighted =
                _service.CreateRoiHighlightImage(src, _roi);

            InputBitmap = MatToBitmapSource(highlighted);
            ResultText   = "軸キャリブレーション表示中";

            var vm = new AxisCalibrationDialogViewModel
            {
                XMin  = _axisSettings.XMin,
                XMax  = _axisSettings.XMax,
                IsXLog = _axisSettings.IsXLog,
                YMin  = _axisSettings.YMin,
                YMax  = _axisSettings.YMax,
                IsYLog = _axisSettings.IsYLog
            };


            var dialog = new AxisCalibrationDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            // 親Windowの右側に配置
            dialog.Left = dialog.Owner.Left + 0.6*dialog.Owner.Width; //
            dialog.Top  = dialog.Owner.Top + 50; // 上から少し下げる

            bool? result = dialog.ShowDialog();
            if (result != true)
                return;

            // ★ ここで値を確定
            ApplyAxisCalibration(vm);

        }

        private void ApplyAxisCalibration(AxisCalibrationDialogViewModel vm)
        {
            // 値の保持（再計算用）
            _axisSettings.XMin  = vm.XMin;
            _axisSettings.XMax  = vm.XMax;
            _axisSettings.IsXLog = vm.IsXLog;

            _axisSettings.YMin  = vm.YMin;
            _axisSettings.YMax  = vm.YMax;
            _axisSettings.IsYLog = vm.IsYLog;


            // 再処理
            var data = _service.Run(
                ImagePath,
                _axisSettings.XMin, _axisSettings.XMax,
                _axisSettings.YMin, _axisSettings.YMax,
                _axisSettings.IsXLog ? "log" : "linear",
                _axisSettings.IsYLog ? "log" : "linear"
            );

            CurveData = data;
            _roi = data.PlotRoi;

            _displayState = DisplayState.AxisCalibrated;
        }


        private BitmapSource MatToBitmapSource(Mat mat)
        {
            return BitmapSourceConverter.ToBitmapSource(mat);
        }

        private void OnShowOriginalImage()
        {
            if (_originalBitmap == null)
                return;

            InputBitmap = _originalBitmap;

            _displayState = DisplayState.Original;
            ResultText   = "原図表示";
        }



    }


}
