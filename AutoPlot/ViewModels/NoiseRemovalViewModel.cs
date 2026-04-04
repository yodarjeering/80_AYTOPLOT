using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace AutoPlot.ViewModels
{
    public partial class NoiseRemovalViewModel : ObservableObject
    {
        private readonly Stack<Mat> _undoStack = new();
        private readonly Stack<Mat> _redoStack = new();

        private readonly Mat _initialPlotArea;
        private readonly Mat _initialNoiseMask;

        private Mat _plotArea;
        private Mat _noiseMask;
        private readonly int _penSize = 10;

        [ObservableProperty]
        private BitmapSource? _previewImage;

        public IRelayCommand UndoCommand { get; }
        public IRelayCommand RedoCommand { get; }
        public IRelayCommand ResetCommand { get; }

        public bool IsConfirmed { get; private set; }

        public Mat? ResultPlotArea { get; private set; }

        public NoiseRemovalViewModel(Mat plotArea,Mat originalPlotArea)
        {
            _plotArea = plotArea.Clone();
            _noiseMask = Mat.Zeros(_plotArea.Size(), MatType.CV_8UC1);
            _initialPlotArea = originalPlotArea.Clone();
            _initialNoiseMask = _noiseMask.Clone();

            UpdatePreview();

            UndoCommand = new RelayCommand(OnUndo);
            RedoCommand = new RelayCommand(OnRedo);
            ResetCommand = new RelayCommand(OnReset);
        }

        private void UpdatePreview()
        {
            using var display = _plotArea.Clone();
            display.SetTo(new Scalar(0, 0, 255), _noiseMask);
            PreviewImage = BitmapSourceConverter.ToBitmapSource(display);
        }

        private void SaveStateForUndo()
        {
            _undoStack.Push(_noiseMask.Clone());
            ClearRedoStack();
        }

        private void ClearRedoStack()
        {
            while (_redoStack.Count > 0)
            {
                var mat = _redoStack.Pop();
                mat.Dispose();
            }
        }

        private void ClearUndoStack()
        {
            while (_undoStack.Count > 0)
            {
                var mat = _undoStack.Pop();
                mat.Dispose();
            }
        }

        public void BeginStroke()
        {
            SaveStateForUndo();
        }

        public void DrawMaskLine(System.Windows.Point p1, System.Windows.Point p2, double canvasW, double canvasH)
        {
            var m1 = CanvasPointToMatPoint(p1, canvasW, canvasH);
            var m2 = CanvasPointToMatPoint(p2, canvasW, canvasH);

            if (!IsInside(m1) || !IsInside(m2))
                return;

            Cv2.Line(_noiseMask, m1, m2, Scalar.White, _penSize);
            UpdatePreview();
        }

        public void Confirm()
        {
            ResultPlotArea = _plotArea.Clone();
            ResultPlotArea.SetTo(new Scalar(255, 255, 255), _noiseMask);
            IsConfirmed = true;
        }

        private OpenCvSharp.Point CanvasPointToMatPoint(System.Windows.Point canvasPt, double canvasW, double canvasH)
        {
            double imgW = _plotArea.Width;
            double imgH = _plotArea.Height;

            double scale = Math.Min(canvasW / imgW, canvasH / imgH);
            double dispW = imgW * scale;
            double dispH = imgH * scale;

            double offsetX = (canvasW - dispW) / 2;
            double offsetY = (canvasH - dispH) / 2;

            return new OpenCvSharp.Point(
                (int)((canvasPt.X - offsetX) / scale),
                (int)((canvasPt.Y - offsetY) / scale)
            );
        }

        private bool IsInside(OpenCvSharp.Point p)
        {
            return p.X >= 0 && p.Y >= 0 &&
                   p.X < _plotArea.Width && p.Y < _plotArea.Height;
        }

        private void OnUndo()
        {
            if (_undoStack.Count == 0)
                return;

            _redoStack.Push(_noiseMask.Clone());

            _noiseMask.Dispose();
            _noiseMask = _undoStack.Pop();

            UpdatePreview();
        }

        private void OnRedo()
        {
            if (_redoStack.Count == 0)
                return;

            _undoStack.Push(_noiseMask.Clone());

            _noiseMask.Dispose();
            _noiseMask = _redoStack.Pop();

            UpdatePreview();
        }

        private void OnReset()
        {
            _plotArea.Dispose();
            _plotArea = _initialPlotArea.Clone();

            _noiseMask.Dispose();
            _noiseMask = _initialNoiseMask.Clone();

            ClearUndoStack();
            ClearRedoStack();

            UpdatePreview();
        }
    }
}