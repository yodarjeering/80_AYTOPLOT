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


    }
}


