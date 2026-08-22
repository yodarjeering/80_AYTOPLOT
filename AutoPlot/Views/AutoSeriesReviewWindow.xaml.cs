using AutoPlot.ViewModels;
using System.Windows;

namespace AutoPlot.Views
{
    public partial class AutoSeriesReviewWindow : Window
    {
        public AutoSeriesReviewWindow()
        {
            InitializeComponent();
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AutoSeriesReviewViewModel vm || vm.SelectedCount == 0)
            {
                MessageBox.Show(this, "少なくとも1つの系列を選択してください。", "自動検出結果");
                return;
            }

            DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
