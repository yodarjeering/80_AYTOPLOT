using AutoPlot.Models;
using AutoPlot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Windows.Media.Imaging;
using AutoPlot.Utils;

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

        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(OnLoadImage);
        }

        public IRelayCommand LoadImageCommand { get; }

        private async void OnLoadImage()
    {
        if (string.IsNullOrEmpty(ImagePath)) return;

        ResultText = "画像読み込み中…";

        try
        {
            // ① 画像を ViewModel の InputBitmap にセット
            InputBitmap = OpenCvUtils.LoadBitmap(ImagePath);
        }
        catch (Exception ex)
        {
            ResultText = $"画像読み込みに失敗: {ex.Message}";
            return;
        }

        // ② 解析処理（重い処理）
        await Task.Run(() =>
        {
            var data = _service.Run(ImagePath,
                                    0.5, 3,
                                    1, 1000,
                                    "linear", "log");

            ResultText = string.Join("\n", data.Points.Select(p =>
                            $"X={p.X:F3}, Y={p.Y:F3}"));
        });
    }

        private BitmapImage _inputBitmap;
        public BitmapImage InputBitmap
        {
            get => _inputBitmap;
            set => SetProperty(ref _inputBitmap, value);
        }
    }


}
