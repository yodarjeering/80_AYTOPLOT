using AutoPlot.Models;
using AutoPlot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

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
            // 例: 先に Path チェック
            if (string.IsNullOrEmpty(ImagePath)) return;

            ResultText = "読み込み中…";

            // 非同期で処理
            await Task.Run(() =>
            {
                // TBD 直値注意
                var data = _service.Run(ImagePath,
                                        0.5, 3,
                                        1, 1000,
                                        "linear", "log");

                ResultText = string.Join("\n", data.Points.Select(p =>
                                $"X={p.X:F3}, Y={p.Y:F3}"));
            });
        }

    }
}
