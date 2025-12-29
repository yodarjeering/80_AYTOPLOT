using System.Windows;
using AutoPlot.ViewModels;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoPlot.Views
{
    public partial class MainView : Window
    {
        private bool isDrawing = false;
        private Polyline currentLine;
        

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

        // 描画開始
        isDrawing = true;

        currentLine = new Polyline
        {
            Stroke = Brushes.Red,      // 線の色
            StrokeThickness = 2        // 太さ
        };

        DrawCanvas.Children.Add(currentLine);

        var pos = e.GetPosition(DrawCanvas);
        currentLine.Points.Add(pos);
    }

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDrawing) return;

        var pos = e.GetPosition(DrawCanvas);
        currentLine.Points.Add(pos);
    }

    private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isDrawing = false;
    }

    }
}


