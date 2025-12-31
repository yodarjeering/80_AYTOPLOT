using AutoPlot.Models;
using AutoPlot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Windows.Media.Imaging;
using AutoPlot.Utils;
using System.Windows;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Input;


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
            _displayState = DisplayState.AxisCalibrated;
            ResultText   = "軸キャリブレーション表示中";

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
