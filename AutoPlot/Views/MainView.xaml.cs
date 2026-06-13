using System.Windows;
using AutoPlot.ViewModels;
using AutoPlot.Models;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoPlot.Views
{
    public partial class MainView : Window
    {
        private bool isDrawing = false;
        private Polyline currentLine;
        private Point _prevPoint;

        // 複数系列なぞり用
        private readonly List<List<Point>> _seriesTracePoints = new();
        private List<Point> _currentTracePoints = new();

        private bool _isSeriesTracing = false;
        private bool _isMouseDrawing = false;

        // 仮：まずは3系列固定。後でViewModelのSeriesCountに差し替える
        private int _seriesCount = 3;
        private int _currentSeriesIndex = 0;
        

        public MainView()
        {
            InitializeComponent();
        }

        private void ImagePathTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            // ファイルがドラッグされてたら許可
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ImagePathTextBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
                return;

            // 先頭のファイルだけ使う想定
            var path = files[0];

            // ViewModel に反映
            if (DataContext is MainViewModel vm)
            {
                vm.ImagePath = path;
            }
        }

        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isSeriesTracing)
            {
                _isMouseDrawing = true;
                _currentTracePoints = new List<Point>();

                Point p = e.GetPosition(DrawCanvas);
                _currentTracePoints.Add(p);

                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            var vm = DataContext as MainViewModel;
            if (vm == null)
                return;
            
            if (vm.DisplayState != DisplayState.NoiseRemoval) return;

            vm.CanvasWidth  = DrawCanvas.ActualWidth;
            vm.CanvasHeight = DrawCanvas.ActualHeight;


            // 描画開始
            isDrawing = true;
            _prevPoint = e.GetPosition(DrawCanvas);

            currentLine = new Polyline();

            var pos = e.GetPosition(DrawCanvas);
            currentLine.Points.Add(_prevPoint);
            DrawCanvas.Children.Add(currentLine);
        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (_isSeriesTracing && _isMouseDrawing)
            {
                Point p = e.GetPosition(DrawCanvas);
                _currentTracePoints.Add(p);

                DrawSeriesTraceLine(_currentTracePoints);

                return;
            }

            if (!isDrawing) return;

            var vm = DataContext as MainViewModel;
            if (vm == null)
                return;

            if (vm.DisplayState != DisplayState.NoiseRemoval) return;

            Point curr = e.GetPosition(DrawCanvas);
            currentLine.Points.Add(curr);

            // ★ 追加：Matマスクにも描く
            vm.DrawNoiseMaskFromCanvas(_prevPoint, curr);

            _prevPoint = curr;
                
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSeriesTracing)
            {
                _isMouseDrawing = false;

                if (_currentTracePoints.Count > 1)
                {
                    _seriesTracePoints.Add(new List<Point>(_currentTracePoints));
                }

                _currentSeriesIndex++;

                if (_currentSeriesIndex >= _seriesCount)
                {
                    _isSeriesTracing = false;
                    MessageBox.Show("すべての系列入力が完了しました。");
                }
                else
                {
                    MessageBox.Show($"系列{_currentSeriesIndex + 1}をなぞってください。");
                }

                return;
            }
            isDrawing = false;
            currentLine = null;
        }

        private void OnShowPathInputDialog(object sender, RoutedEventArgs e)
        {
            var dialog = new ImagePathDialog
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string selectedPath = dialog.ImagePath;
                if (DataContext is MainViewModel vm) 
                {   
                    vm.ImagePath = selectedPath;
                    vm.LoadImageCommand?.Execute(null);
                }
            }

        }
    
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Clipboard.ContainsImage())
                {
                    var bmp = Clipboard.GetImage();

                    if (DataContext is MainViewModel vm)
                    {
                        vm.LoadImageFromClipboard(bmp);
                    }

                    e.Handled = true;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DrawSeriesTraceLine(List<Point> points)
        {
            if (points.Count < 2) return;

            // 既存の線を消さず、最後の1区間だけ追加
            int i = points.Count - 1;

            var line = new Line
            {
                X1 = points[i - 1].X,
                Y1 = points[i - 1].Y,
                X2 = points[i].X,
                Y2 = points[i].Y,
                Stroke = Brushes.Red,
                StrokeThickness = 3
            };

            DrawCanvas.Children.Add(line);
        }

        private void StartSeriesTrace()
        {
            _seriesTracePoints.Clear();
            _currentTracePoints.Clear();

            _seriesCount = 3; // 後でユーザー入力値にする
            _currentSeriesIndex = 0;

            _isSeriesTracing = true;
            _isMouseDrawing = false;

            DrawCanvas.Children.Clear();

            MessageBox.Show("系列1をなぞってください。");
        }
    }
}


