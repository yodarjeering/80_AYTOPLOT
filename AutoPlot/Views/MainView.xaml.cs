using System.Windows;
using AutoPlot.ViewModels;

namespace AutoPlot.Views
{
    public partial class MainView : Window
    {
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

    }
}


